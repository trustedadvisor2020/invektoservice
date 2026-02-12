"""
UI Test Runner - Execute approved test cases from a test-spec.

Uses Python Playwright sync API (headless Chromium).

CLI: python runner.py <test-spec-path>
"""

import sys
import json
from pathlib import Path
from datetime import datetime, timezone

from playwright.sync_api import sync_playwright, Page

from config import PAGE_LOAD_TIMEOUT, ACTION_TIMEOUT, HEADLESS, VIEWPORT


class UITestRunner:
    def __init__(self, spec_path: Path, basic_auth: str | None = None):
        self.spec_path = spec_path
        self.spec: dict = json.loads(spec_path.read_text(encoding="utf-8"))
        self.output_dir = spec_path.parent
        self.screenshots_dir = self.output_dir / "screenshots"
        self.screenshots_dir.mkdir(parents=True, exist_ok=True)
        self.basic_auth = basic_auth
        self.results: list[dict] = []

    def run(self) -> dict:
        """Execute all approved test cases and return report dict."""
        approved_cases = [tc for tc in self.spec.get("test_cases", []) if tc.get("approved", False)]

        if not approved_cases:
            return self._empty_report("No approved test cases to run")

        with sync_playwright() as p:
            browser = p.chromium.launch(headless=HEADLESS)
            ctx_opts = {"viewport": VIEWPORT, "ignore_https_errors": True}
            if self.basic_auth and ":" in self.basic_auth:
                parts = self.basic_auth.split(":", 1)
                ctx_opts["http_credentials"] = {"username": parts[0], "password": parts[1]}
            context = browser.new_context(**ctx_opts)
            page = context.new_page()

            # Error tracking per-test
            self._console_errors: list[str] = []
            self._network_failures: list[str] = []
            self._js_exceptions: list[str] = []

            page.on("console", self._on_console)
            page.on("response", self._on_response)
            page.on("pageerror", self._on_pageerror)

            try:
                # Navigate to target
                page.goto(self.spec["target_url"], wait_until="networkidle", timeout=PAGE_LOAD_TIMEOUT)

                # Handle auth if spec was scanned with auth
                # (session should persist from scan, but just in case)

                for tc in approved_cases:
                    result = self._run_test_case(page, tc)
                    self.results.append(result)

                report = self._build_report()

                # Write report JSON
                report_path = self.output_dir / "report.json"
                report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

                # Print summary
                print(json.dumps({
                    "status": "ok",
                    "total": report["summary"]["total"],
                    "passed": report["summary"]["passed"],
                    "failed": report["summary"]["failed"],
                    "errors": report["summary"]["errors"],
                    "report_path": str(report_path),
                }, indent=2))

                return report

            except Exception as e:
                print(json.dumps({"status": "error", "message": str(e)}))
                return self._empty_report(str(e))
            finally:
                browser.close()

    def _run_test_case(self, page: Page, tc: dict) -> dict:
        """Execute a single test case."""
        tc_id = tc["id"]
        elem_data = tc["element"]
        selector = elem_data["selector"]

        # Clear error collectors
        self._console_errors.clear()
        self._network_failures.clear()
        self._js_exceptions.clear()

        result = {
            "test_id": tc_id,
            "element_id": elem_data["id"],
            "element_type": elem_data["type"],
            "element_text": elem_data.get("text", ""),
            "test_type": tc["test_type"],
            "status": "pending",
            "errors": [],
            "screenshots": [],
        }

        try:
            # Navigate to correct page if needed
            target_page = elem_data.get("page", "")
            if target_page and page.url != target_page:
                try:
                    page.goto(target_page, wait_until="networkidle", timeout=PAGE_LOAD_TIMEOUT)
                except Exception as nav_err:
                    result["status"] = "error"
                    result["errors"].append(f"Navigation failed: {nav_err}")
                    return result

            # Find element
            element = page.query_selector(selector)
            if not element:
                # Try text-based fallback
                text = elem_data.get("text", "")
                if text:
                    element = page.query_selector(f":text('{text}')")

            if not element:
                result["status"] = "failed"
                result["errors"].append(f"Element not found: {selector}")
                self._take_screenshot(page, f"{tc_id}-not-found")
                result["screenshots"].append(f"{tc_id}-not-found.png")
                return result

            # Before screenshot
            self._take_screenshot(page, f"{tc_id}-before")
            result["screenshots"].append(f"{tc_id}-before.png")

            # Execute actions
            for action in tc.get("actions", ["click"]):
                self._execute_action(page, element, action)
                page.wait_for_timeout(500)

            # After screenshot
            self._take_screenshot(page, f"{tc_id}-after")
            result["screenshots"].append(f"{tc_id}-after.png")

            # Check expectations
            expected = tc.get("expected", {})
            all_pass = True

            if expected.get("no_console_errors") and self._console_errors:
                result["errors"].append(f"Console errors: {self._console_errors[:3]}")
                all_pass = False

            if expected.get("no_network_failures") and self._network_failures:
                result["errors"].append(f"Network failures: {self._network_failures[:3]}")
                all_pass = False

            if expected.get("no_js_exceptions") and self._js_exceptions:
                result["errors"].append(f"JS exceptions: {self._js_exceptions[:3]}")
                all_pass = False

            result["status"] = "passed" if all_pass else "failed"

        except Exception as e:
            result["status"] = "error"
            result["errors"].append(f"Execution error: {str(e)}")
            self._take_screenshot(page, f"{tc_id}-error")
            result["screenshots"].append(f"{tc_id}-error.png")

        return result

    def _execute_action(self, page: Page, element, action: str):
        """Execute a single action on an element."""
        try:
            if action == "click":
                element.click(timeout=ACTION_TIMEOUT)

            elif action.startswith("fill:"):
                value = action.split(":", 1)[1]
                element.fill(value)

            elif action == "blur":
                element.evaluate("el => el.blur()")

            elif action == "submit":
                element.press("Enter")

            elif action == "select_first":
                # Select first non-empty option
                options = element.query_selector_all("option")
                if len(options) > 1:
                    value = options[1].get_attribute("value")
                    if value:
                        element.select_option(value=value)

            elif action == "fill_fields":
                # Fill all visible inputs in a form
                inputs = element.query_selector_all("input:not([type='hidden']), textarea")
                for inp in inputs:
                    input_type = inp.get_attribute("type") or "text"
                    if input_type in ("text", "email", "tel", "search", "url"):
                        inp.fill("test-value")
                    elif input_type == "number":
                        inp.fill("123")
                    elif input_type == "password":
                        inp.fill("test-password")

        except Exception as e:
            raise RuntimeError(f"Action '{action}' failed: {e}")

    # --- Listeners ---

    def _on_console(self, msg):
        if msg.type == "error":
            self._console_errors.append(msg.text[:300])

    def _on_response(self, response):
        if response.status >= 400:
            self._network_failures.append(f"{response.status} {response.url[:150]}")

    def _on_pageerror(self, error):
        self._js_exceptions.append(str(error)[:300])

    # --- Screenshot ---

    def _take_screenshot(self, page: Page, name: str):
        path = self.screenshots_dir / f"test-{name}.png"
        try:
            page.screenshot(path=str(path))
        except Exception:
            pass

    # --- Report ---

    def _build_report(self) -> dict:
        passed = sum(1 for r in self.results if r["status"] == "passed")
        failed = sum(1 for r in self.results if r["status"] == "failed")
        errors = sum(1 for r in self.results if r["status"] == "error")
        total = len(self.results)

        findings = []
        for r in self.results:
            if r["status"] != "passed":
                findings.append({
                    "severity": "error" if r["status"] == "error" else "warning",
                    "test_id": r["test_id"],
                    "element": r["element_id"],
                    "element_text": r.get("element_text", ""),
                    "issue": r["errors"][0] if r["errors"] else "Unknown",
                    "screenshots": r["screenshots"],
                })

        return {
            "target_url": self.spec["target_url"],
            "run_timestamp": datetime.now(timezone.utc).isoformat(),
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "errors": errors,
                "pass_rate": f"{passed / total * 100:.0f}%" if total > 0 else "0%",
            },
            "results": self.results,
            "findings": findings,
        }

    def _empty_report(self, message: str) -> dict:
        return {
            "target_url": self.spec.get("target_url", "unknown"),
            "run_timestamp": datetime.now(timezone.utc).isoformat(),
            "summary": {"total": 0, "passed": 0, "failed": 0, "errors": 0, "pass_rate": "0%"},
            "results": [],
            "findings": [],
            "error": message,
        }


# --- CLI ---
if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python runner.py <test-spec-path>")
        sys.exit(1)

    spec_file = Path(sys.argv[1])
    if not spec_file.exists():
        print(f"Error: {spec_file} not found")
        sys.exit(1)

    ba = None
    for i, arg in enumerate(sys.argv):
        if arg == "--basic-auth" and i + 1 < len(sys.argv):
            ba = sys.argv[i + 1]

    runner = UITestRunner(spec_file, basic_auth=ba)
    runner.run()

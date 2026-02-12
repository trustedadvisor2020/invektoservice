"""
UI Scanner - Crawl a localhost UI, discover interactive elements, generate test-spec.

Uses Python Playwright sync API (headless Chromium).

CLI: python scanner.py <url> <output-dir> [--auth tenant_id:api_key]
"""

import sys
import json
import hashlib
from pathlib import Path
from datetime import datetime, timezone

from playwright.sync_api import sync_playwright, Page, ElementHandle

from config import PAGE_LOAD_TIMEOUT, ACTION_TIMEOUT, HEADLESS, VIEWPORT, SCREENSHOT_DELAY


class UIScanner:
    def __init__(self, target_url: str, output_dir: Path, auth: str | None = None, basic_auth: str | None = None):
        self.target_url = target_url
        self.output_dir = output_dir
        self.screenshots_dir = output_dir / "screenshots"
        self.screenshots_dir.mkdir(parents=True, exist_ok=True)
        self.auth = auth  # "tenant_id:api_key" for form-based login
        self.basic_auth = basic_auth  # "username:password" for HTTP Basic Auth

        self.console_errors: list[dict] = []
        self.network_failures: list[dict] = []
        self.discovered_elements: list[dict] = []
        self._screenshot_counter = 0

    def scan(self) -> dict:
        """Main scan: open browser, discover elements, return test-spec dict."""
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=HEADLESS)

            # Build context options
            ctx_opts = {
                "viewport": VIEWPORT,
                "ignore_https_errors": True,
            }
            if self.basic_auth and ":" in self.basic_auth:
                parts = self.basic_auth.split(":", 1)
                ctx_opts["http_credentials"] = {"username": parts[0], "password": parts[1]}

            context = browser.new_context(**ctx_opts)
            page = context.new_page()

            # Attach listeners
            page.on("console", self._on_console)
            page.on("response", self._on_response)

            try:
                # Navigate
                page.goto(self.target_url, wait_until="networkidle", timeout=PAGE_LOAD_TIMEOUT)
                self._screenshot(page, "initial")

                # Handle auth redirect (form-based login)
                if self._is_login_page(page):
                    if self.auth or self.basic_auth:
                        # Use auth or basic_auth for form login
                        creds = self.auth or self.basic_auth
                        self._do_login(page, creds)
                        page.wait_for_load_state("networkidle")
                        self._screenshot(page, "after-login")
                    else:
                        spec = self._error_spec("Login page detected but no --auth provided")
                        spec_path = self.output_dir / "test-spec.json"
                        spec_path.write_text(json.dumps(spec, indent=2, ensure_ascii=False), encoding="utf-8")
                        print(json.dumps({"status": "error", "message": spec["error"]}))
                        return spec

                # Discover all pages reachable from current page
                pages_scanned = self._discover_pages(page)

                # Build test-spec
                spec = self._build_spec(pages_scanned)

                # Write to file
                spec_path = self.output_dir / "test-spec.json"
                spec_path.write_text(json.dumps(spec, indent=2, ensure_ascii=False), encoding="utf-8")

                # Write console errors log
                if self.console_errors:
                    log_path = self.output_dir / "console-errors.log"
                    log_path.write_text(
                        "\n".join(f"[{e.get('type','error')}] {e['text']}" for e in self.console_errors),
                        encoding="utf-8",
                    )

                print(json.dumps({
                    "status": "ok",
                    "elements": len(self.discovered_elements),
                    "console_errors": len(self.console_errors),
                    "network_failures": len(self.network_failures),
                    "pages_scanned": pages_scanned,
                    "spec_path": str(spec_path),
                }, indent=2))

                return spec

            except Exception as e:
                print(json.dumps({"status": "error", "message": str(e)}))
                return self._error_spec(str(e))
            finally:
                browser.close()

    # --- Page discovery ---

    def _discover_pages(self, page: Page) -> int:
        """Discover elements on current page and navigate to internal links."""
        visited = set()
        to_visit = [page.url]
        pages_scanned = 0

        while to_visit:
            url = to_visit.pop(0)
            if url in visited:
                continue
            visited.add(url)

            # Navigate if not already there
            if page.url != url:
                try:
                    page.goto(url, wait_until="networkidle", timeout=PAGE_LOAD_TIMEOUT)
                except Exception:
                    continue

            pages_scanned += 1
            current_path = page.url

            # Discover elements on this page
            self._discover_buttons(page, current_path)
            self._discover_links(page, current_path)
            self._discover_inputs(page, current_path)
            self._discover_forms(page, current_path)

            # Find internal navigation links for multi-page scan
            nav_links = self._find_internal_links(page)
            for link in nav_links:
                if link not in visited:
                    to_visit.append(link)

            # Limit to prevent infinite crawl
            if pages_scanned >= 10:
                break

        return pages_scanned

    def _find_internal_links(self, page: Page) -> list[str]:
        """Find internal navigation links on current page."""
        links = []
        try:
            elements = page.query_selector_all("a[href]")
            base_origin = page.evaluate("() => window.location.origin")
            for el in elements:
                href = el.get_attribute("href")
                if not href:
                    continue
                # Resolve relative URLs
                full_url = page.evaluate(f"(href) => new URL(href, window.location.href).href", href)
                # Only internal links
                if full_url.startswith(base_origin) and "#" not in href:
                    links.append(full_url)
        except Exception:
            pass
        return links

    # --- Element discovery ---

    def _discover_buttons(self, page: Page, current_path: str):
        """Discover all button-like elements."""
        selectors = "button, [role='button'], input[type='button'], input[type='submit']"
        try:
            elements = page.query_selector_all(selectors)
            for el in elements:
                data = self._extract_element(el, "button", current_path)
                if data:
                    self.discovered_elements.append(data)
        except Exception:
            pass

    def _discover_links(self, page: Page, current_path: str):
        """Discover clickable links (internal only)."""
        try:
            elements = page.query_selector_all("a[href]")
            for el in elements:
                href = el.get_attribute("href")
                if href and not href.startswith(("http://", "https://", "#", "mailto:", "tel:")):
                    data = self._extract_element(el, "link", current_path)
                    if data:
                        data["href"] = href
                        self.discovered_elements.append(data)
        except Exception:
            pass

    def _discover_inputs(self, page: Page, current_path: str):
        """Discover input elements."""
        try:
            elements = page.query_selector_all("input:not([type='hidden']), textarea, select")
            for el in elements:
                data = self._extract_element(el, "input", current_path)
                if data:
                    data["input_type"] = el.get_attribute("type") or "text"
                    data["placeholder"] = el.get_attribute("placeholder") or ""
                    self.discovered_elements.append(data)
        except Exception:
            pass

    def _discover_forms(self, page: Page, current_path: str):
        """Discover form elements."""
        try:
            elements = page.query_selector_all("form")
            for el in elements:
                data = self._extract_element(el, "form", current_path)
                if data:
                    # Count fields
                    fields = el.query_selector_all("input:not([type='hidden']), textarea, select")
                    data["field_count"] = len(fields)
                    data["action"] = el.get_attribute("action") or ""
                    data["method"] = el.get_attribute("method") or "GET"
                    self.discovered_elements.append(data)
        except Exception:
            pass

    # --- Element extraction ---

    def _extract_element(self, el: ElementHandle, el_type: str, page_url: str) -> dict | None:
        """Extract metadata from a DOM element."""
        try:
            box = el.bounding_box()
            if not box or box["width"] < 1 or box["height"] < 1:
                return None  # Not visible

            # Get text content
            if el_type == "input":
                text = el.get_attribute("placeholder") or el.get_attribute("name") or ""
            else:
                try:
                    text = el.inner_text()
                except Exception:
                    text = ""
            text = text.strip()[:120]

            if not text and el_type in ("button", "link"):
                # Try aria-label or title
                text = el.get_attribute("aria-label") or el.get_attribute("title") or ""
                text = text.strip()[:120]

            # Generate stable selector
            selector = self._build_selector(el)

            # Generate unique ID
            raw_id = el.get_attribute("id")
            if raw_id:
                elem_id = raw_id
            else:
                hash_input = f"{el_type}-{text}-{selector}"
                elem_id = f"{el_type}-{hashlib.md5(hash_input.encode()).hexdigest()[:8]}"

            return {
                "id": elem_id,
                "type": el_type,
                "text": text,
                "selector": selector,
                "page": page_url,
                "position": {
                    "x": round(box["x"]),
                    "y": round(box["y"]),
                    "w": round(box["width"]),
                    "h": round(box["height"]),
                },
            }
        except Exception:
            return None

    def _build_selector(self, el: ElementHandle) -> str:
        """Build a stable CSS/text selector for Playwright."""
        # Priority: id > data-testid > aria-label > text > tag
        el_id = el.get_attribute("id")
        if el_id:
            return f"#{el_id}"

        testid = el.get_attribute("data-testid")
        if testid:
            return f"[data-testid='{testid}']"

        aria = el.get_attribute("aria-label")
        if aria:
            return f"[aria-label='{aria}']"

        try:
            text = el.inner_text().strip()[:50]
            if text:
                tag = el.evaluate("el => el.tagName.toLowerCase()")
                return f"{tag}:has-text('{text}')"
        except Exception:
            pass

        # Fallback: tag + nth
        try:
            tag = el.evaluate("el => el.tagName.toLowerCase()")
            return tag
        except Exception:
            return "*"

    # --- Auth handling ---

    def _is_login_page(self, page: Page) -> bool:
        """Detect if current page is a login form."""
        url = page.url.lower()
        if "/login" in url or "/signin" in url:
            return True
        # Check for password input
        pwd = page.query_selector("input[type='password']")
        return pwd is not None

    def _do_login(self, page: Page, creds: str | None = None):
        """Attempt to fill and submit login form."""
        credentials = creds or self.auth
        if not credentials or ":" not in credentials:
            return

        parts = credentials.split(":", 1)
        username = parts[0]
        password = parts[1]

        # Fill username field (first text input or named username/tenant)
        username_input = (
            page.query_selector("input[name='username']")
            or page.query_selector("input[name='tenant_id']")
            or page.query_selector("input[name='tenantId']")
            or page.query_selector("input[type='text']")
            or page.query_selector("input[type='number']")
        )
        if username_input:
            username_input.fill(username)

        # Fill password field
        key_input = (
            page.query_selector("input[name='password']")
            or page.query_selector("input[name='api_key']")
            or page.query_selector("input[name='apiKey']")
            or page.query_selector("input[type='password']")
        )
        if key_input:
            key_input.fill(password)

        # Submit
        submit = (
            page.query_selector("button[type='submit']")
            or page.query_selector("input[type='submit']")
            or page.query_selector("button:has-text('Login')")
            or page.query_selector("button:has-text('Giriş')")
        )
        if submit:
            submit.click()
            page.wait_for_load_state("networkidle")

    # --- Listeners ---

    def _on_console(self, msg):
        if msg.type in ("error", "warning"):
            self.console_errors.append({
                "type": msg.type,
                "text": msg.text[:500],
            })

    def _on_response(self, response):
        if response.status >= 400:
            self.network_failures.append({
                "url": response.url[:200],
                "status": response.status,
            })

    # --- Screenshot ---

    def _screenshot(self, page: Page, name: str):
        self._screenshot_counter += 1
        path = self.screenshots_dir / f"scan-{name}-{self._screenshot_counter}.png"
        try:
            page.screenshot(path=str(path), full_page=True)
        except Exception:
            pass

    # --- Spec builders ---

    def _build_spec(self, pages_scanned: int) -> dict:
        """Build the test-spec dict from discovered data."""
        test_cases = []
        seen_ids = set()

        for elem in self.discovered_elements:
            # Deduplicate by id
            if elem["id"] in seen_ids:
                continue
            seen_ids.add(elem["id"])

            tc = {
                "id": f"tc-{elem['id']}",
                "element": elem,
                "test_type": self._infer_test_type(elem),
                "actions": self._infer_actions(elem),
                "expected": {
                    "no_console_errors": True,
                    "no_network_failures": True,
                    "no_js_exceptions": True,
                },
                "approved": False,
            }
            test_cases.append(tc)

        return {
            "target_url": self.target_url,
            "scan_timestamp": datetime.now(timezone.utc).isoformat(),
            "pages_scanned": pages_scanned,
            "total_elements": len(test_cases),
            "console_errors": self.console_errors[:20],  # Truncate
            "network_failures": self.network_failures[:20],
            "test_cases": test_cases,
            "approved": False,
        }

    def _infer_test_type(self, elem: dict) -> str:
        if elem["type"] == "button":
            return "click_and_verify"
        elif elem["type"] == "link":
            return "navigation"
        elif elem["type"] == "input":
            return "input_and_validate"
        elif elem["type"] == "form":
            return "form_submission"
        return "click_and_verify"

    def _infer_actions(self, elem: dict) -> list[str]:
        if elem["type"] == "button":
            return ["click"]
        elif elem["type"] == "link":
            return ["click"]
        elif elem["type"] == "input":
            input_type = elem.get("input_type", "text")
            if input_type in ("text", "email", "tel", "search", "url"):
                return ["fill:test-value", "blur"]
            elif input_type == "number":
                return ["fill:123", "blur"]
            elif input_type == "checkbox":
                return ["click"]
            elif input_type == "select":
                return ["select_first"]
            return ["click"]
        elif elem["type"] == "form":
            return ["fill_fields", "submit"]
        return ["click"]

    def _error_spec(self, message: str) -> dict:
        return {
            "target_url": self.target_url,
            "scan_timestamp": datetime.now(timezone.utc).isoformat(),
            "pages_scanned": 0,
            "total_elements": 0,
            "console_errors": self.console_errors[:20],
            "network_failures": self.network_failures[:20],
            "test_cases": [],
            "approved": False,
            "error": message,
        }


# --- CLI ---
if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python scanner.py <url> <output-dir> [--auth tenant_id:api_key]")
        sys.exit(1)

    url = sys.argv[1]
    out_dir = Path(sys.argv[2])
    out_dir.mkdir(parents=True, exist_ok=True)

    auth = None
    basic_auth = None
    for i, arg in enumerate(sys.argv):
        if arg == "--auth" and i + 1 < len(sys.argv):
            auth = sys.argv[i + 1]
        if arg == "--basic-auth" and i + 1 < len(sys.argv):
            basic_auth = sys.argv[i + 1]

    scanner = UIScanner(url, out_dir, auth=auth, basic_auth=basic_auth)
    scanner.scan()

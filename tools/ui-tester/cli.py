"""
CLI entry point for ui-tester commands.
Avoids PowerShell quoting issues by accepting simple string arguments.

Usage:
  python cli.py gen-dir <target-name>
  python cli.py check <url>
  python cli.py start <service-name>
  python cli.py scan <url> <output-dir> [--auth tenant:key] [--basic-auth user:pass]
  python cli.py run <spec-path>
  python cli.py report <report-json-path>
"""

import sys


def main():
    if len(sys.argv) < 2:
        print("Usage: python cli.py <command> [args...]")
        print("Commands: gen-dir, check, start, scan, run, report")
        sys.exit(1)

    cmd = sys.argv[1]

    if cmd == "gen-dir":
        from config import generate_run_dir
        target = sys.argv[2] if len(sys.argv) > 2 else "unknown"
        print(str(generate_run_dir(target)))

    elif cmd == "check":
        from service_manager import ensure_ready
        url = sys.argv[2]
        ok, msg = ensure_ready(url)
        print(f"{'OK' if ok else 'FAIL'}: {msg}")
        sys.exit(0 if ok else 1)

    elif cmd == "start":
        from service_manager import start_service
        svc = sys.argv[2]
        ok, msg = start_service(svc)
        print(f"{'OK' if ok else 'FAIL'}: {msg}")
        sys.exit(0 if ok else 1)

    elif cmd == "scan":
        from scanner import UIScanner
        from pathlib import Path
        url = sys.argv[2]
        out_dir = Path(sys.argv[3])
        auth = None
        basic_auth = None
        for i, arg in enumerate(sys.argv):
            if arg == "--auth" and i + 1 < len(sys.argv):
                auth = sys.argv[i + 1]
            if arg == "--basic-auth" and i + 1 < len(sys.argv):
                basic_auth = sys.argv[i + 1]
        out_dir.mkdir(parents=True, exist_ok=True)
        scanner = UIScanner(url, out_dir, auth=auth, basic_auth=basic_auth)
        scanner.scan()

    elif cmd == "run":
        from runner import UITestRunner
        from pathlib import Path
        spec_path = Path(sys.argv[2])
        ba = None
        for i, arg in enumerate(sys.argv):
            if arg == "--basic-auth" and i + 1 < len(sys.argv):
                ba = sys.argv[i + 1]
        runner = UITestRunner(spec_path, basic_auth=ba)
        runner.run()

    elif cmd == "report":
        from reporter import generate_html
        from pathlib import Path
        import json
        report_path = Path(sys.argv[2])
        report = json.loads(report_path.read_text(encoding="utf-8"))
        html_path = generate_html(report, report_path.parent)
        print(f"HTML report: {html_path}")

    else:
        print(f"Unknown command: {cmd}")
        sys.exit(1)


if __name__ == "__main__":
    main()

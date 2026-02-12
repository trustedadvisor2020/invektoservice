"""
Report Generator - Convert test results to HTML and JSON reports.

CLI: python reporter.py <report-json-path>
"""

import sys
import json
from pathlib import Path
from datetime import datetime


def generate_html(report: dict, output_dir: Path) -> Path:
    """Generate an HTML report with embedded CSS and screenshot references."""
    html_path = output_dir / "report.html"

    target = report.get("target_url", "unknown")
    ts = report.get("run_timestamp", "")
    summary = report.get("summary", {})
    results = report.get("results", [])
    findings = report.get("findings", [])
    error = report.get("error")

    results_html = "\n".join(_render_result(r) for r in results)
    findings_html = "\n".join(_render_finding(f) for f in findings)

    error_banner = ""
    if error:
        error_banner = f'<div class="error-banner">Error: {_esc(error)}</div>'

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>UI Test Report - {_esc(target)}</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f0f2f5; color: #1a1a2e; padding: 24px; }}
  .container {{ max-width: 1100px; margin: 0 auto; }}
  h1 {{ font-size: 24px; margin-bottom: 4px; }}
  .meta {{ color: #666; font-size: 13px; margin-bottom: 20px; }}
  .summary {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 24px; }}
  .stat {{ background: #fff; border-radius: 8px; padding: 18px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }}
  .stat .val {{ font-size: 36px; font-weight: 700; }}
  .stat .lbl {{ font-size: 12px; color: #888; margin-top: 4px; text-transform: uppercase; letter-spacing: 0.5px; }}
  .passed {{ color: #16a34a; }}
  .failed {{ color: #dc2626; }}
  .error {{ color: #d97706; }}
  .neutral {{ color: #4b5563; }}
  .error-banner {{ background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; padding: 12px 16px; border-radius: 8px; margin-bottom: 20px; }}
  h2 {{ font-size: 18px; margin: 24px 0 12px; }}
  .card {{ background: #fff; border-radius: 8px; padding: 16px; margin-bottom: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); border-left: 4px solid #e5e7eb; }}
  .card.passed {{ border-left-color: #16a34a; }}
  .card.failed {{ border-left-color: #dc2626; }}
  .card.error {{ border-left-color: #d97706; }}
  .card-header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }}
  .card-header h3 {{ font-size: 14px; }}
  .badge {{ font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 4px; text-transform: uppercase; }}
  .badge.passed {{ background: #dcfce7; color: #166534; }}
  .badge.failed {{ background: #fee2e2; color: #991b1b; }}
  .badge.error {{ background: #fef3c7; color: #92400e; }}
  .card-body {{ font-size: 13px; color: #555; }}
  .card-body .errors {{ background: #fef2f2; padding: 8px 12px; border-radius: 4px; margin-top: 8px; }}
  .card-body .errors li {{ color: #991b1b; margin: 4px 0; font-size: 12px; }}
  .screenshots {{ display: flex; gap: 8px; margin-top: 10px; flex-wrap: wrap; }}
  .screenshots img {{ max-width: 280px; border: 1px solid #e5e7eb; border-radius: 4px; cursor: pointer; }}
  .screenshots img:hover {{ border-color: #3b82f6; }}
  .finding {{ background: #fff; border-radius: 8px; padding: 14px 16px; margin-bottom: 10px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }}
  .finding.warning {{ border-left: 4px solid #dc2626; }}
  .finding.error {{ border-left: 4px solid #d97706; }}
  .finding strong {{ font-size: 12px; text-transform: uppercase; }}
  .finding p {{ font-size: 13px; margin-top: 4px; color: #555; }}
  .empty {{ color: #999; font-style: italic; padding: 20px; text-align: center; }}
</style>
</head>
<body>
<div class="container">
  <h1>UI Test Report</h1>
  <div class="meta">
    <strong>Target:</strong> {_esc(target)} &nbsp;|&nbsp;
    <strong>Time:</strong> {_esc(ts)} &nbsp;|&nbsp;
    <strong>Pass rate:</strong> {summary.get('pass_rate', '0%')}
  </div>

  {error_banner}

  <div class="summary">
    <div class="stat"><div class="val neutral">{summary.get('total', 0)}</div><div class="lbl">Total</div></div>
    <div class="stat"><div class="val passed">{summary.get('passed', 0)}</div><div class="lbl">Passed</div></div>
    <div class="stat"><div class="val failed">{summary.get('failed', 0)}</div><div class="lbl">Failed</div></div>
    <div class="stat"><div class="val error">{summary.get('errors', 0)}</div><div class="lbl">Errors</div></div>
  </div>

  <h2>Test Results</h2>
  {results_html if results_html.strip() else '<div class="empty">No test results</div>'}

  <h2>Findings</h2>
  {findings_html if findings_html.strip() else '<div class="empty">No issues found</div>'}
</div>
</body>
</html>"""

    html_path.write_text(html, encoding="utf-8")
    return html_path


def _render_result(r: dict) -> str:
    status = r.get("status", "pending")
    tc_id = r.get("test_id", "unknown")
    elem_text = r.get("element_text", "")
    elem_type = r.get("element_type", "")

    errors_html = ""
    if r.get("errors"):
        items = "".join(f"<li>{_esc(e)}</li>" for e in r["errors"][:5])
        errors_html = f'<div class="errors"><ul>{items}</ul></div>'

    screenshots_html = ""
    if r.get("screenshots"):
        imgs = "".join(
            f'<img src="screenshots/test-{_esc(s)}" alt="{_esc(s)}" loading="lazy">'
            for s in r["screenshots"]
        )
        screenshots_html = f'<div class="screenshots">{imgs}</div>'

    return f"""<div class="card {status}">
  <div class="card-header">
    <h3>{_esc(tc_id)} <small>({_esc(elem_type)}: {_esc(elem_text[:60])})</small></h3>
    <span class="badge {status}">{status}</span>
  </div>
  <div class="card-body">
    {errors_html}
    {screenshots_html}
  </div>
</div>"""


def _render_finding(f: dict) -> str:
    severity = f.get("severity", "warning")
    element = f.get("element", "")
    issue = f.get("issue", "")
    elem_text = f.get("element_text", "")

    return f"""<div class="finding {severity}">
  <strong>{_esc(severity)}</strong> - {_esc(element)} ({_esc(elem_text[:40])})
  <p>{_esc(issue)}</p>
</div>"""


def _esc(text: str) -> str:
    """Basic HTML escaping."""
    return (
        str(text)
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


# --- CLI ---
if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python reporter.py <report-json-path>")
        sys.exit(1)

    report_file = Path(sys.argv[1])
    if not report_file.exists():
        print(f"Error: {report_file} not found")
        sys.exit(1)

    report = json.loads(report_file.read_text(encoding="utf-8"))
    output_dir = report_file.parent

    html_path = generate_html(report, output_dir)
    print(f"HTML report: {html_path}")

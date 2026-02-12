"""
Configuration for UI Tester
"""

from pathlib import Path
from datetime import datetime

# Paths
PROJECT_ROOT = Path("c:/CRMs/InvektoServices")
OUTPUT_BASE = PROJECT_ROOT / "temp" / "ui-test"

# Timeouts (milliseconds)
PAGE_LOAD_TIMEOUT = 30000
ACTION_TIMEOUT = 5000
SCREENSHOT_DELAY = 500
SERVICE_START_TIMEOUT = 30  # seconds

# Browser settings
HEADLESS = True
VIEWPORT = {"width": 1920, "height": 1080}

# Known targets (shorthand -> full URL)
KNOWN_TARGETS = {
    "flow-builder": "http://localhost:3002/flow-builder/",
    "flow-builder-prod": "http://localhost:5000/flow-builder/",
    "dashboard": "http://localhost:5000/ops",
    "backend": "http://localhost:5000/",
}

# Service registry: how to check and start each service
SERVICE_REGISTRY = {
    "backend": {
        "port": 5000,
        "health_path": "/health",
        "start_cmd": "dotnet run --project src/Invekto.Backend/Invekto.Backend.csproj",
        "cwd": str(PROJECT_ROOT),
    },
    "flow-builder-dev": {
        "port": 3002,
        "health_path": "/flow-builder/",
        "start_cmd": "npm run dev",
        "cwd": str(PROJECT_ROOT / "src" / "Invekto.Backend" / "FlowBuilder"),
    },
    "automation": {
        "port": 7108,
        "health_path": "/health",
        "start_cmd": "dotnet run --project src/Invekto.Automation/Invekto.Automation.csproj",
        "cwd": str(PROJECT_ROOT),
    },
}

# URL pattern -> service mapping
URL_SERVICE_MAP = [
    ("localhost:3002", "flow-builder-dev"),
    ("localhost:5000", "backend"),
    ("localhost:7108", "automation"),
]


def resolve_target(target: str) -> str:
    """Resolve shorthand target to full URL."""
    return KNOWN_TARGETS.get(target, target)


def generate_run_dir(target: str) -> Path:
    """Generate a timestamped run directory."""
    ts = datetime.now().strftime("%Y%m%d-%H%M%S")
    safe_name = target.replace("http://", "").replace("https://", "").replace("/", "_").replace(":", "-").rstrip("_")
    run_dir = OUTPUT_BASE / f"{ts}-{safe_name}"
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / "screenshots").mkdir(exist_ok=True)
    return run_dir

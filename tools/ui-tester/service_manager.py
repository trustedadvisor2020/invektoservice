"""
Service Health Checker & Auto-Starter

Checks if target services are running, starts them if needed.
CLI: python service_manager.py check <url>
     python service_manager.py start <service-name>
"""

import sys
import time
import subprocess
import requests
from urllib.parse import urlparse

from config import SERVICE_REGISTRY, URL_SERVICE_MAP, SERVICE_START_TIMEOUT


def identify_service(url: str) -> str | None:
    """Identify which service a URL belongs to."""
    for pattern, service_name in URL_SERVICE_MAP:
        if pattern in url:
            return service_name
    return None


def check_health(service_name: str) -> tuple[bool, str]:
    """Check if a service is healthy. Returns (is_healthy, message)."""
    service = SERVICE_REGISTRY.get(service_name)
    if not service:
        return False, f"Unknown service: {service_name}"

    url = f"http://localhost:{service['port']}{service['health_path']}"
    try:
        resp = requests.get(url, timeout=5)
        if resp.status_code < 400:
            return True, f"{service_name} is running (port {service['port']})"
        return False, f"{service_name} responded with {resp.status_code}"
    except requests.ConnectionError:
        return False, f"{service_name} not reachable (port {service['port']})"
    except requests.Timeout:
        return False, f"{service_name} timeout (port {service['port']})"


def start_service(service_name: str) -> tuple[bool, str]:
    """Start a service and wait for it to become healthy."""
    service = SERVICE_REGISTRY.get(service_name)
    if not service:
        return False, f"Unknown service: {service_name}"

    if not service.get("start_cmd"):
        return False, f"{service_name} has no start command defined"

    # Already running?
    healthy, _ = check_health(service_name)
    if healthy:
        return True, f"{service_name} is already running"

    cmd = service["start_cmd"]
    cwd = service.get("cwd")

    print(f"Starting {service_name}: {cmd}")
    try:
        # Start process in background (detached)
        subprocess.Popen(
            f'powershell -NoProfile -Command "cd \'{cwd}\'; {cmd}"',
            shell=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
        )
    except Exception as e:
        return False, f"Failed to start {service_name}: {e}"

    # Poll for health
    for i in range(SERVICE_START_TIMEOUT):
        time.sleep(1)
        healthy, msg = check_health(service_name)
        if healthy:
            return True, f"{service_name} started successfully ({i+1}s)"

    return False, f"{service_name} started but health check failed after {SERVICE_START_TIMEOUT}s"


def ensure_ready(url: str) -> tuple[bool, str]:
    """Ensure the service behind a URL is running. Auto-start if needed."""
    service_name = identify_service(url)
    if not service_name:
        # Unknown service - just try to reach the URL directly
        try:
            resp = requests.get(url, timeout=5)
            if resp.status_code < 400:
                return True, f"Target reachable: {url}"
            return False, f"Target responded with {resp.status_code}: {url}"
        except Exception:
            return False, f"Target not reachable: {url}"

    healthy, msg = check_health(service_name)
    if healthy:
        return True, msg

    # Try to auto-start
    print(f"Service not running, attempting auto-start...")
    return start_service(service_name)


# --- CLI ---
if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage:")
        print("  python service_manager.py check <url>")
        print("  python service_manager.py start <service-name>")
        print(f"\nKnown services: {', '.join(SERVICE_REGISTRY.keys())}")
        sys.exit(1)

    action = sys.argv[1]
    target = sys.argv[2]

    if action == "check":
        ok, msg = ensure_ready(target)
        print(f"{'OK' if ok else 'FAIL'}: {msg}")
        sys.exit(0 if ok else 1)

    elif action == "start":
        ok, msg = start_service(target)
        print(f"{'OK' if ok else 'FAIL'}: {msg}")
        sys.exit(0 if ok else 1)

    else:
        print(f"Unknown action: {action}")
        sys.exit(1)

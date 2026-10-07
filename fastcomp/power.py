"""Detect whether the machine is on AC power (True), battery (False) or unknown (None)."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Optional


def on_ac(sysroot: Path = Path("/")) -> Optional[bool]:
    if sys.platform.startswith("linux"):
        supply = sysroot / "sys/class/power_supply"
        if not supply.is_dir():
            return None
        has_battery = False
        for d in supply.iterdir():
            try:
                kind = (d / "type").read_text().strip()
                if kind == "Mains" and (d / "online").read_text().strip() == "1":
                    return True
                has_battery |= kind == "Battery"
            except OSError:
                continue
        return False if has_battery else True  # desktops: no battery -> treat as AC
    if sys.platform == "win32":
        import ctypes

        class S(ctypes.Structure):
            _fields_ = [("ac", ctypes.c_ubyte), ("flag", ctypes.c_ubyte), ("pct", ctypes.c_ubyte),
                        ("saver", ctypes.c_ubyte), ("life", ctypes.c_ulong), ("full", ctypes.c_ulong)]
        s = S()
        if ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(s)):
            return {0: False, 1: True}.get(s.ac)
        return None
    if sys.platform == "darwin":
        try:
            out = subprocess.run(["pmset", "-g", "batt"], capture_output=True, text=True).stdout
            return "AC Power" in out
        except OSError:
            return None
    return None

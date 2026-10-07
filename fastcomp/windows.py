"""Windows settings: power plan + gaming/background overhead. No visual effects are touched."""
from __future__ import annotations

import re
import subprocess
from typing import List, Optional

from .core import Setting

HIGH_PERF = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
BALANCED = "381b4222-f694-41f0-9685-ff5bb260df2e"


def _run(args: List[str]) -> str:
    p = subprocess.run(args, capture_output=True, text=True)
    if p.returncode != 0:
        raise RuntimeError((p.stderr or p.stdout).strip() or f"{args[0]} failed")
    return p.stdout


def _active_scheme() -> Optional[str]:
    m = re.search(r"GUID:\s*([0-9a-fA-F-]{36})", _run(["powercfg", "/getactivescheme"]))
    return m.group(1).lower() if m else None


def _set_scheme(guid: Optional[str]) -> None:
    if guid:
        _run(["powercfg", "/setactive", guid])


def _reg(key: str, name: str, desc: str, target: int, admin: bool = False) -> Setting:
    def read() -> Optional[str]:
        p = subprocess.run(["reg", "query", key, "/v", name], capture_output=True, text=True)
        m = re.search(r"REG_DWORD\s+0x([0-9a-fA-F]+)", p.stdout)
        return str(int(m.group(1), 16)) if m else None

    def write(v: Optional[str]) -> None:
        if v is None:
            subprocess.run(["reg", "delete", key, "/v", name, "/f"], capture_output=True)
        else:
            _run(["reg", "add", key, "/v", name, "/t", "REG_DWORD", "/d", v, "/f"])

    return Setting(f"{key}\\{name}", desc, read, write, str(target), admin)


def settings(profile: str) -> List[Setting]:
    ac = profile == "performance"
    out = [Setting("powercfg:scheme", "Active power plan", _active_scheme, _set_scheme,
                   HIGH_PERF if ac else BALANCED, admin=False)]
    out += [
        _reg(r"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled",
             "Windows Game Mode (prioritises the game)", 1),
        _reg(r"HKCU\System\GameConfigStore", "GameDVR_Enabled",
             "Background game capture overhead off", 0),
        _reg(r"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled",
             "Background app capture off", 0),
        _reg(r"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode",
             "Hardware-accelerated GPU scheduling (reboot needed)", 2, admin=True),
    ]
    if not ac:
        out.append(_reg(r"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                        "GlobalUserDisabled", "Stop UWP apps running in background", 1))
    return out

"""macOS settings via pmset."""
from __future__ import annotations

import re
import subprocess
from typing import List, Optional

from .core import Setting


def _low_power() -> Optional[str]:
    out = subprocess.run(["pmset", "-g", "custom"], capture_output=True, text=True).stdout
    m = re.search(r"Battery Power:.*?lowpowermode\s+(\d)", out, re.S)
    return m.group(1) if m else None


def _set_low_power(v: Optional[str]) -> None:
    if v is not None:
        p = subprocess.run(["sudo", "-n", "pmset", "-b", "lowpowermode", v], capture_output=True, text=True)
        if p.returncode:
            raise RuntimeError(p.stderr.strip() or "pmset failed")


def settings(profile: str) -> List[Setting]:
    if profile == "performance":
        return []  # macOS already runs full speed on AC; nothing to change
    return [Setting("pmset:battery:lowpowermode", "Low Power Mode on battery",
                    _low_power, _set_low_power, "1", admin=True)]

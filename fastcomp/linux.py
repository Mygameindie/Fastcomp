"""Linux settings, all via /sys (root needed to write). Nothing here lowers visuals or capacity."""
from __future__ import annotations

from pathlib import Path
from typing import List, Optional

from .core import Setting


def _file_setting(path: Path, desc: str, target: str, root: Path) -> Setting:
    def read() -> Optional[str]:
        return path.read_text().strip()

    def write(v: Optional[str]) -> None:
        if v is not None:
            path.write_text(v)

    return Setting(str(path.relative_to(root)), desc, read, write, target, admin=True)


def settings(profile: str, root: Path = Path("/")) -> List[Setting]:
    ac = profile == "performance"
    out: List[Setting] = []

    # CPU frequency policy: Energy Performance Preference if available, else governor.
    for pol in sorted((root / "sys/devices/system/cpu/cpufreq").glob("policy*")):
        epp = pol / "energy_performance_preference"
        gov = pol / "scaling_governor"
        if epp.exists():
            out.append(_file_setting(epp, "CPU energy/performance bias",
                                     "performance" if ac else "balance_power", root))
        elif gov.exists():
            avail = (pol / "scaling_available_governors")
            names = avail.read_text().split() if avail.exists() else []
            order = ["performance", "schedutil"] if ac else ["schedutil", "ondemand", "powersave"]
            pick = next((g for g in order if g in names), None)
            if pick:
                out.append(_file_setting(gov, "CPU frequency governor", pick, root))

    if not ac:  # these save power but are pointless/slightly costly on wall power
        hda = root / "sys/module/snd_hda_intel/parameters/power_save"
        if hda.exists():
            out.append(_file_setting(hda, "Audio codec idle power-down", "1", root))
        for h in sorted((root / "sys/class/scsi_host").glob("host*/link_power_management_policy")):
            out.append(_file_setting(h, "SATA link power management", "med_power_with_dipm", root))
    return out

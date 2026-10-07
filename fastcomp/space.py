"""Reclaim disk space from regenerable junk only (temp files, caches, trash).

Never touches documents, downloads, or app data. Files newer than MIN_AGE_DAYS are kept
so running programs aren't disturbed. `scan` is read-only; `clean` deletes.
"""
from __future__ import annotations

import os
import sys
import time
from pathlib import Path
from typing import Dict, List, Tuple

MIN_AGE_DAYS = 7


def targets(home: Path = Path.home(), env=os.environ) -> Dict[str, Path]:
    if sys.platform == "win32":
        local = Path(env.get("LOCALAPPDATA", home / "AppData/Local"))
        return {
            "User temp files": Path(env.get("TEMP", local / "Temp")),
            "Thumbnail cache": local / "Microsoft/Windows/Explorer",
            "Crash dumps": local / "CrashDumps",
            "pip cache": local / "pip/Cache",
        }
    if sys.platform == "darwin":
        return {"pip cache": home / "Library/Caches/pip", "Trash": home / ".Trash"}
    return {
        "Trash": home / ".local/share/Trash/files",
        "Thumbnail cache": home / ".cache/thumbnails",
        "pip cache": home / ".cache/pip",
        "User temp files": Path("/tmp"),
    }


def _stale_files(root: Path, now: float, min_age_days: float) -> List[Tuple[Path, int]]:
    out = []
    if not root.is_dir():
        return out
    cutoff = now - min_age_days * 86400
    for dirpath, _dirs, files in os.walk(root, followlinks=False):
        for f in files:
            p = Path(dirpath, f)
            try:
                st = p.lstat()
                # only files we own, so shared /tmp is never touched
                if st.st_mtime < cutoff and (os.name == "nt" or st.st_uid == os.getuid()):
                    out.append((p, st.st_size))
            except OSError:
                continue
    return out


def scan(tgts: Dict[str, Path], min_age_days: float = MIN_AGE_DAYS) -> Dict[str, List[Tuple[Path, int]]]:
    now = time.time()
    return {name: _stale_files(p, now, min_age_days) for name, p in tgts.items()}


def clean(found: Dict[str, List[Tuple[Path, int]]]) -> int:
    freed = 0
    for files in found.values():
        for p, size in files:
            try:
                p.unlink()
                freed += size
            except OSError:
                pass
    return freed


def human(n: float) -> str:
    for u in ("B", "KB", "MB", "GB"):
        if n < 1024:
            return f"{n:.1f} {u}"
        n /= 1024
    return f"{n:.1f} TB"

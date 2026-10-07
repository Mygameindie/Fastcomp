"""Platform-independent engine: settings, backup state, apply/restore."""
from __future__ import annotations

import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional


@dataclass
class Setting:
    id: str                                   # stable key used for backup/restore
    description: str
    read: Callable[[], Optional[str]]         # current value (None = unset)
    write: Callable[[Optional[str]], None]    # write value (None = remove/unset)
    target: str                               # desired value for the chosen profile
    admin: bool = False                       # needs root/Administrator


@dataclass
class Result:
    setting: Setting
    status: str          # "would-change" | "changed" | "ok" | "failed" | "restored"
    before: Optional[str] = None
    detail: str = ""


def state_path() -> Path:
    override = os.environ.get("FASTCOMP_STATE")
    return Path(override) if override else Path.home() / ".fastcomp" / "state.json"


def load_state() -> Dict[str, Optional[str]]:
    try:
        return json.loads(state_path().read_text()).get("originals", {})
    except (OSError, ValueError):
        return {}


def save_state(originals: Dict[str, Optional[str]]) -> None:
    p = state_path()
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps({"originals": originals}, indent=2))


def apply(settings: List[Setting], dry_run: bool) -> List[Result]:
    originals = load_state()
    results: List[Result] = []
    for s in settings:
        try:
            cur = s.read()
        except Exception as e:  # unreadable -> skip, never guess
            results.append(Result(s, "failed", None, f"cannot read: {e}"))
            continue
        if cur == s.target:
            results.append(Result(s, "ok", cur))
            continue
        if dry_run:
            results.append(Result(s, "would-change", cur))
            continue
        try:
            s.write(s.target)
        except Exception as e:
            hint = " (try running as root/Administrator)" if s.admin else ""
            results.append(Result(s, "failed", cur, f"{e}{hint}"))
            continue
        originals.setdefault(s.id, cur)  # keep the *first* original across re-applies
        save_state(originals)
        results.append(Result(s, "changed", cur))
    return results


def restore(settings: List[Setting]) -> List[Result]:
    originals = load_state()
    by_id = {s.id: s for s in settings}
    results: List[Result] = []
    for sid, orig in list(originals.items()):
        s = by_id.get(sid)
        if s is None:
            results.append(Result(Setting(sid, sid, lambda: None, lambda v: None, ""),
                                  "failed", orig, "setting no longer available"))
            continue
        try:
            s.write(orig)
        except Exception as e:
            results.append(Result(s, "failed", orig, str(e)))
            continue
        del originals[sid]
        save_state(originals)
        results.append(Result(s, "restored", orig))
    return results

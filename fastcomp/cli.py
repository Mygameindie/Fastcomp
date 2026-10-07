from __future__ import annotations

import argparse
import sys
from typing import List

from . import core, power


def platform_settings(profile: str) -> List[core.Setting]:
    if sys.platform.startswith("linux"):
        from . import linux
        return linux.settings(profile)
    if sys.platform == "win32":
        from . import windows
        return windows.settings(profile)
    if sys.platform == "darwin":
        from . import macos
        return macos.settings(profile)
    return []


def resolve_profile(p: str) -> str:
    if p != "auto":
        return p
    return "performance" if power.on_ac() is not False else "battery"


def show(results: List[core.Result]) -> None:
    for r in results:
        print(f"[{r.status:12}] {r.setting.description}: {r.before!r} -> {r.setting.target!r}"
              + (f"  ({r.detail})" if r.detail else ""))
    if not results:
        print("Nothing to do on this platform/profile.")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="fastcomp", description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name, help_ in (("plan", "show what would change (changes nothing)"),
                        ("apply", "apply settings (backs up originals first)"),
                        ("restore", "put every changed setting back")):
        sp = sub.add_parser(name, help=help_)
        if name != "restore":
            sp.add_argument("--profile", choices=["auto", "performance", "battery"], default="auto")
    args = ap.parse_args(argv)

    if args.cmd == "restore":
        show(core.restore(platform_settings("battery") + platform_settings("performance")))
        return 0
    profile = resolve_profile(args.profile)
    print(f"Profile: {profile}")
    results = core.apply(platform_settings(profile), dry_run=args.cmd == "plan")
    show(results)
    return 1 if any(r.status == "failed" for r in results) else 0


if __name__ == "__main__":
    sys.exit(main())

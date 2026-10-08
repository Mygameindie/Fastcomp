# Fastcomp

A small, dependency-free tool that applies **reversible** system settings to cut
background overhead and wasted power, on Windows, Linux and macOS. It never lowers
resolution, graphics quality, CPU/GPU capacity or any other resource.

## Install
**No Python needed:** download the file for your OS from the
[Releases page](https://github.com/mygameindie/fastcomp/releases)
(`fastcomp-windows.exe`, `fastcomp-macos` or `fastcomp-linux`) and run it from a terminal,
e.g. `fastcomp-windows.exe plan`. On macOS/Linux, first run `chmod +x fastcomp-*`.
Unsigned builds: Windows SmartScreen may warn ("More info" → "Run anyway"), and on macOS
right-click → Open the first time.

**With Python 3.9+:**
```
pip install git+https://github.com/mygameindie/fastcomp
```

## Use
```
fastcomp plan      # dry run: shows what would change, changes nothing
fastcomp apply     # backs up the current values, then applies them
fastcomp restore   # puts everything back exactly as it was
```
`--profile auto|performance|battery` (default `auto`: AC power → `performance`, battery → `battery`).
Run `apply`/`restore` as Administrator (Windows) or root (Linux) for the settings that need it;
anything that can't be changed is reported, not hidden.

## Free up disk space
```
fastcomp space         # dry run: shows how much is reclaimable
fastcomp space --yes   # deletes it
```
Only deletes temp files, caches and trash older than 7 days that you own. It can't create
new capacity, it recovers space already wasted; the amount varies per PC (10 GB is common
on long-used machines, not guaranteed). Documents and downloads are never touched.

## What it changes
| OS | performance (plugged in) | battery |
|----|--------------------------|---------|
| Windows | High-performance power plan, Game Mode on, background game-capture off, hardware GPU scheduling | Balanced plan, same overhead cuts, UWP background apps off |
| Linux | CPU energy/performance bias → `performance` (or governor fallback) | bias → `balance_power`, audio codec + SATA link power saving |
| macOS | nothing (already full speed) | Low Power Mode on battery |

## What it can't do
- **120 FPS is not something software can guarantee.** Frame rate is set by your GPU/CPU,
  the game and its settings, and your monitor's refresh rate. Fastcomp removes OS-level
  overhead so the hardware you have is fully used; it can't make weak hardware faster.
- Higher performance and lower energy pull in opposite directions, so the tool picks
  per power source instead of promising both at once.
- Gains are typically small (a few percent, mostly better frame-time consistency).

## Develop
```
pip install pytest . && pytest
```
Releases: pushing a tag like `v0.1.0` makes `.github/workflows/build.yml` build the three
standalone executables with PyInstaller and attach them to a GitHub release.
Every change goes through `core.apply`, which records the first original value of each
setting in `~/.fastcomp/state.json` so `restore` is always exact.

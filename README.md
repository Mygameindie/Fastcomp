# Fastcomp

A small, dependency-free tool that applies **reversible** system settings to cut
background overhead and wasted power, on Windows, Linux and macOS. It never lowers
resolution, graphics quality, CPU/GPU capacity or any other resource.

## Install
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
pip install pytest && pytest
```
Every change goes through `core.apply`, which records the first original value of each
setting in `~/.fastcomp/state.json` so `restore` is always exact.

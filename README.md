# PC Tune-Up

A small, free and honest tool that helps a slow Windows PC. No install needed: download `PCTuneUp.exe` and run it. Works on Windows 10 and 11.

**[⬇ Download the latest version](https://github.com/Mygameindie/Fastcomp/releases/latest)**

## What it does

| Tab | What you get |
|---|---|
| **Overview** | Live CPU, memory and disk use, with suggestions for your PC |
| **Clean Up** | Finds temp files, browser caches, crash dumps, the Windows Update cache and the Recycle Bin. Shows the size first and always asks before deleting |
| **Startup** | Turn apps that launch with Windows on or off (the same switch as Settings › Apps › Startup, fully reversible) |
| **Processes** | See what uses the most memory and close it |
| **Tweaks** | Power plan, visual effects, drive optimization, Windows Update, virus scan |

## What it does not do

No "RAM boosters", registry cleaners or other tricks that don't actually make a PC faster. No ads, no internet access, no data collected.

## First run: "Windows protected your PC"

The app isn't code-signed, so Windows SmartScreen may warn you the first time. Click **More info → Run anyway**.

## Administrator

It runs without admin rights. To also clean Windows temp files and the update cache, or change "all users" startup apps, click **Restart as administrator** in the Clean Up tab.

## Build from source

Double-click `build.bat`. It uses the C# compiler that ships with Windows (.NET Framework 4.x), so there's nothing to download. The app is written to `dist\PCTuneUp.exe`.

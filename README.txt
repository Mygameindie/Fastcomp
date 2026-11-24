PC Tune-Up 1.0
==============

A small, free and honest tool that helps a slow Windows PC.
No install needed - just run PCTuneUp.exe. Works on Windows 10 and 11.

WHAT IT DOES
  Overview   - Live CPU, memory and disk use, with tips for your PC.
  Clean Up   - Finds and removes temp files, browser caches, crash dumps,
               the Windows Update cache and the Recycle Bin. Shows the size
               first and always asks before deleting anything.
  Startup    - Lists apps that open when Windows starts. Turn them on/off
               (same switch as Settings > Apps > Startup, fully reversible).
  Processes  - Shows what is using the most memory and lets you close it.
  Tweaks     - Power plan, visual effects, drive optimization, updates,
               virus scan.

WHAT IT DOES NOT DO
  No "RAM boosters", registry cleaners or other tricks that do not
  actually make a PC faster. No ads, no internet access, no data collected.

FIRST RUN: "Windows protected your PC"
  This app is not code-signed (signing certificates cost money), so
  Windows SmartScreen may warn you the first time.
  Click "More info" -> "Run anyway".

ADMINISTRATOR
  The app runs without admin rights. To also clean Windows temp files and
  the update cache, or to change "all users" startup apps, click
  "Restart as administrator" in the Clean Up tab.

SOURCE
  The full source code is in the src folder. Build with build.bat
  (uses the C# compiler that comes with Windows - nothing to download).

---
name: fix-errors
description: Read the latest 7 Days to Die log file, find errors and warnings related to the 7nes mod, and fix the code so the game will load correctly.
disable-model-invocation: true
user-invocable: true
allowed-tools: Bash, Read, Edit, Write, Glob, Grep, Agent
argument-hint:
---

# Fix 7nes Mod Errors and Warnings from Game Logs

## Step 1: Find and read the latest log file

Find the most recent game output log in `C:\Users\Richard\AppData\Roaming\7DaysToDie\logs\`. The log files are named `output_log_client__YYYY-MM-DD__HH-MM-SS.txt`, so sorting by name descending gives the latest. Use Bash:

```
ls -1r "C:/Users/Richard/AppData/Roaming/7DaysToDie/logs/"output_log_client__* | head -1
```

This filters for only game output logs (ignoring `launcher.log` etc.) and returns the newest one by filename timestamp. Then read the full log file contents.

## Step 2: Extract relevant errors and warnings

Search the log for:
- Any lines containing `7nes` (case-insensitive)
- Any `Exception`, `Error`, `NullReference`, `TypeLoadException`, `MissingMethodException`, or `ReflectionTypeLoadException` lines that appear near 7nes references
- Any `Failed to load` or `Could not load` messages related to mods
- Any XML parsing errors related to Config/ patches
- Stack traces following any of the above
- Any `WRN` (warning) lines that appear AFTER the 7nes mod starts loading (i.e. after the `Trying to load from folder: '7nes'` line). These warnings may not mention 7nes directly but can be triggered by the mod's initialization. Compare against warnings that appear in logs without the mod to distinguish mod-caused warnings from baseline game warnings.

Collect ALL error and warning context — include surrounding lines to understand the full chain.

## Step 3: Diagnose and fix

Based on the errors found:

1. Identify the root cause of each error
2. Read the relevant source files in the project (src/Core/*.cs, src/Integration/*.cs, Config/*.xml)
3. Apply fixes to resolve the errors
4. Explain what was wrong and what you changed

## Step 4: Rebuild and deploy

After applying fixes, rebuild the mod and deploy it to the game folder by running:

```
powershell -ExecutionPolicy Bypass -File deploy.ps1
```

If the build succeeds, report that the fix has been deployed and is ready to test. If it fails, diagnose and fix the build error, then retry.

## Important notes

- The mod's source files are in the current working directory (7nes project root)
- C# files are in `src/Core/` and `src/Integration/`
- XML config patches are in `Config/`
- If there are no 7nes-related errors or warnings in the log, report that clearly — and skip the rebuild step
- Warnings that only appear when 7nes is loaded are considered 7nes-related, even if they don't mention 7nes directly (e.g. Harmony, Discord RTC, or Unity warnings triggered by mod initialization failures)

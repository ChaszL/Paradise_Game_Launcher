# Paradise Launcher — Setup Guide

Thanks for trying out Paradise Launcher! This guide covers everything you need to get it running.

## Requirements

- Windows 10/11 (64-bit)
- .NET 10.0 Desktop Runtime (if not using the self-contained build)
- Internet connection (only needed the first time each game's box art is fetched)

## Installing from the Mega Link

1. Open the Mega cloud link you were given.
2. Click **Download** to save the zip file to your PC.
3. Extract the zip anywhere you like (e.g. `C:\Games\ParadiseLauncher`).
4. Open the extracted folder and run `ParadiseLauncher.exe`.

## No Accounts Needed

Paradise Launcher doesn't require you to sign up, log in, or link any account to use it — just extract and run. Box art is fetched automatically using a bundled SteamGridDB key, so you don't need to create your own API key either. (This bundled key is for testing convenience — a future release will let you plug in your own free key from Settings instead.)

## First Launch

1. Open the launcher — since no game folder is set yet, your library will look empty. That's expected.
2. Click the **Settings** icon/button in the nav bar.
3. Find **Game Library Path**, click **Browse**, and select the single folder that contains your game shortcuts (see **How It Finds Your Games** below for what that folder should look like).
4. Click **Save**. The launcher will offer to restart — say yes.
5. After it reopens, it will scan that folder, pull down cover art for each game, and try to auto-detect install status and category.

If you ever move your shortcuts to a new folder, just repeat these steps with the new path.

## Settings — What Each Option Does

Open **Settings** from the nav bar to access these:

- **Game Library Path** — The single folder the launcher scans for game shortcuts or game subfolders. Everything you want to appear in your library needs to live inside this one folder.
- **Library Banner Size** — Controls the width/height of the cover art shown in your main game grid. Pick from the preset size options in the dropdown.
- **Recents Banner Size** — Controls the width/height of the smaller cover art shown in the Recently Played strip. Also a preset dropdown.
- **Background Color** — Sets the main window background color. Use the color picker (drag the saturation/value box and hue slider, or type a hex code directly) to choose any color.
- **Navbar Color** — Sets the color of the top navigation bar and the Settings panel background, using the same color picker.
- **Category Manager** — Lets you add custom categories beyond the built-in list, and assign categories to individual games from a dropdown on each game card.

Changes are saved to `settings.json` next to the exe when you click **Save**. Some changes (like Game Library Path) apply after the app restarts, which it will offer to do automatically.

## How It Finds Your Games

Paradise Launcher is **shortcut-based**: it doesn't scan your whole PC for games. Instead, it looks inside the **one folder** you set as your Game Library Path and reads what's in it. That folder needs to contain either:

- **Shortcuts** (`.lnk` or `.url` files) that point to your games — the normal setup, e.g. shortcuts to your Steam/Epic/Ubisoft/GOG games all collected in one place, or
- **Subfolders**, one per game, if you're not using shortcuts.

Either way, everything has to live in that **single folder** — the launcher won't recursively search your whole drive or multiple separate folders. If your shortcuts are scattered across different locations, the easiest approach is to create one folder (e.g. `C:\Games\Shortcuts`) and copy or create shortcuts to all your games there.

Once a shortcut is loaded, the launcher also tries to detect whether that game is actually installed (checking the shortcut's target, common install locations, and records from Epic/Ubisoft/GOG/Microsoft Store) so it can show an accurate installed/not-installed status.

## Box Art (SteamGridDB) & Custom Art

Paradise Launcher automatically fetches cover art for your games from SteamGridDB the first time each game loads, and caches it locally in an `Assets` folder next to the exe so it only has to fetch once per game.

**Want different art?** You can override the auto-fetched image at any time:

1. Find or download the artwork you want (for example, browsing [steamgriddb.com](https://www.steamgriddb.com) yourself for a different style/cover).
2. Rename the image file to exactly match the game's name as it appears in your shortcut, followed by `.jpg` — for example `Portal 2.jpg`.
3. Drop it into the `Assets` folder next to `ParadiseLauncher.exe`, replacing the existing file if there is one.
4. Restart the launcher (or refresh your library) to see the new art.

Since the launcher always checks the `Assets` folder first before hitting the SteamGridDB API, any image you place there manually will be used instead of fetching a new one.

## Hardware Monitor

CPU, GPU, and RAM gauges in the nav bar update automatically every 2 seconds using LibreHardwareMonitor. No setup required — if your hardware sensors aren't accessible on your machine, the gauges will simply stay idle rather than crash the app.

## Troubleshooting

| Issue | Likely Cause |
|---|---|
| No games show up | You haven't set a Game Library Path yet — go to Settings and browse to your shortcuts folder |
| Some games show up but others don't | Not all shortcuts are in the same folder — move/copy them all into the one folder you set in Settings |
| Box art missing | No internet connection on first load, or the game name didn't match anything on SteamGridDB — try adding art manually via the Assets folder |
| Custom art not showing | File name doesn't exactly match the game's shortcut name, or it's not saved as `.jpg` |
| Hardware gauges stuck at 0% | Sensor access unavailable on this machine (common in VMs or on some laptops) |
| "Installed" badge wrong | Detection is heuristic-based (folder/name matching); manually launching once may help it re-detect |

## Feedback & Extra Notes

This is a testing build. If something looks off, a game isn't detected correctly, or art isn't matching up, let Chasz know:
- The game name/shortcut you were testing
- Which launcher (Steam, Epic, Ubisoft, GOG, Xbox/Game Pass) it's installed through, if any
- Your folder structure (single folder of shortcuts vs. subfolders)

That context makes it much easier to reproduce and fix.
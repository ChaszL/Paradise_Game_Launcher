# Paradise Launcher Overhaul Plan

Approach: frontend → backend. Build each screen with fake data first, then connect the real services.
Architecture: WPF + MVVM (CommunityToolkit.Mvvm). Work happens on the `overhaul` branch.

## Main window

**Top bar:** logo, small CPU/RAM/GPU gauges, search bar, category filter, settings button.
- The small gauges only appear once the stats band has scrolled out of view.

**Stats band:**
- Every drive that has games on it, showing whole-drive used / free space.
- Big CPU/RAM/GPU gauges.
- Game count, broken down by category.

**Recents row:**
- No scrolling. Shows as many recent games as fit the window width at the current banner size (smaller banners mean more games).

**Library grid:**
- All games from `games.json`, in alphabetical order, filtered by search and category.
- Left click launches the game.
- Right click lets you change the category (nothing else).
- If a game is uninstalled or not found, it shows an "Uninstalled / Not found" tag so the user knows to rescan.

## Settings window (separate window)

- Color picker for: logo, navbar, CPU, GPU, RAM, usage background, games background.
- Find games button, which opens Game Finder.
- Banner sizes.
- Shortcut folder.
- API keys. These are stored in `.env` or a secrets file, never in `settings.json`.
- Category manager.

## Game Finder window (separate window)

- Scans for **games only**, not other apps.
- Shows a list with checkboxes so the user picks which games to add. Games already added come pre-ticked, and new ones are marked.
- Has a **manual add** option: browse to an `.exe`.
- The selected games are saved to `games.json`.

## First launch

1. No `games.json` exists, so the app creates it.
2. Game Finder opens automatically.
3. The user picks games, which are saved.
4. SteamGridDB starts fetching art right away.

After that, startup just reads the JSON. The app does not scan the PC every launch.

## Data files (`%AppData%\ParadiseLauncher\`)

| File | Contents |
|---|---|
| `settings.json` | Colors, banner sizes, shortcut folder |
| `games.json` | Selected games, their categories, art paths, install status |
| `recents.json` | Game IDs and last-played timestamps |

Each file has a `"version"` field.

## Categories

- A game can belong to **multiple** categories. `games.json` stores a list per game, e.g. `"categories": ["Shooter", "Multiplayer"]`.
- The category filter shows every game that has the selected category.
- The right-click menu shows every category as a checkbox (tick or untick).
- The game count in the stats band counts a game under each of its categories, so the per-category numbers can add up to more than the total.

## Stages

1. Project structure, MVVM toolkit, main window layout with fake data
2. JSON storage in AppData
3. Game Finder window (moving the existing detection code over) and manual add
4. Connecting the library and recents to real data, plus SteamGrid art with a disk cache
5. Stats band: drive space, game counts, gauges and the scroll behavior
6. Settings window (color picker, sizes, folder, keys, category manager)
7. Polish: right-click category menu, not-found tags, error handling, cleanup

# OneDeck Keyboard Shortcuts & Hotkeys

> Generated from codebase on 2026-06-14. Lists all currently active and commented-out keyboard / mouse shortcuts.

## Gameplay & Phase Controls

These shortcuts drive the main game loop.

| Shortcut | Context | Action |
|----------|---------|--------|
| `Space` or `Mouse Left Click` | Combat reveal phase | Reveal the next card from the deck |
| `Space` or `Mouse Left Click` | Combat effect phase | Trigger the revealed card's effect |
| `Space` or `Mouse Left Click` | Combat finished | Clear the board and continue to the result phase |
| `Space` or `Mouse Left Click` | Result phase | Exit result phase and enter shop phase |
| `Space` | Shop phase | Exit shop phase and start combat |
| `Esc` | Any phase | Quit the application / stop Play mode in the Editor |

## Debug / Development Hotkeys

These shortcuts are intended for development, testing, and data export. They are active in Play mode.

### Deck Saving & Loading (`DeckSaver`)

| Shortcut | Action |
|----------|--------|
| `Ctrl + S` | Save the current player deck to JSON |
| `Ctrl + L` | Load a saved deck into the enemy deck |
| `Ctrl + W` | Wipe / clear all saved decks |
| `Ctrl + D` | Print saved deck statistics to the console |

### Enemy Deck Recording (`EnemyDeckRecorder`)

| Shortcut | Action |
|----------|--------|
| `F12` | Record the current enemy deck (key is configurable on the component) |

### Shop Statistics (`ShopStatsManager`)

| Shortcut | Action |
|----------|--------|
| `Ctrl + Shift + P` | Print shop statistics report |
| `Ctrl + Shift + E` | Export shop statistics to CSV |
| `Ctrl + Shift + R` | Reset shop statistics data |

### Card Win Rate Tracking (`CardWinRateTracker`)

| Shortcut | Action |
|----------|--------|
| `Ctrl + Shift + P` | Print win-rate report to the console |
| `Ctrl + Shift + E` | Export win-rate data to CSV |
| `Ctrl + Shift + C` | Clear all win-rate tracking data |

## Currently Disabled / Commented Shortcuts

The following keys are wired in `ShopManager.cs` but the code is commented out, so they have no effect:

| Shortcut | Intended Action |
|----------|-----------------|
| `S` | Toggle sell / buy mode |
| `R` | Reroll the shop |
| `1` - `6` | Buy or sell the shop card at the corresponding slot |

## Input System Asset Note

The project contains a default Unity Input System asset at `Assets/InputSystem_Actions.inputactions` with bindings for WASD, arrow keys, `Space`, and number keys. However, the current gameplay code uses the legacy `Input.GetKeyDown(KeyCode.XXX)` API, so those Input System bindings are not actively driving the game.

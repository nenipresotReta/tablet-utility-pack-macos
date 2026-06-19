# OTD macOS Companion

Personal macOS extensions for OpenTabletDriver 0.6.7, built around a Wacom PTK-670 and a Goodnotes workflow. Internal `TabletUtilityPack.*` type names are retained for compatibility with existing OTD settings.

## Added Features

| Binding or filter | Function |
| --- | --- |
| `Cycle Keys` | Cycles through any number of keys, with independent state per binding |
| `Smart Monitor Cycle` | Cycles the absolute pen mapping across live macOS displays |
| `Smart Monitor Cycle Filter` | Applies current display geometry and resolution to pen output |
| `Hold Pen Drag Scroll` | Converts absolute pen movement to pixel scrolling while held |
| `Pen Drag Scroll Filter` | Tracks post-transform pen movement for drag scrolling |
| `Mac Native Hotkey` | Sends native macOS modifier and key combinations |
| `Mac Space Left/Right` | Switches macOS Spaces |
| `Mac Mission Control` | Opens Mission Control |
| `Mac App Windows` | Opens the current application's window view |
| `Toggle Crosshair` | Toggles a click-through crosshair that follows the pen cursor |

## Default Goodnotes Layout

### Pen

| Position | Binding |
| --- | --- |
| Tip | Tip |
| Eraser | Eraser |
| Pen Button 1 | `Cycle Keys: P+E` |
| Pen Button 2 | `Hold Pen Drag Scroll` |
| Pen Button 3 | Right click |

### Auxiliary Buttons

| Position | Binding |
| --- | --- |
| Aux 1 | `Up` |
| Aux 2 | `Right` |
| Aux 3 | `Down` |
| Aux 4 | `Left` |
| Aux 5 | `Smart Monitor Cycle: LeftToRight, Forward` |
| Aux 6 | `P` |
| Aux 7 | `N` |
| Aux 8 | `D` |
| Aux 9 | `S` |
| Aux 10 | `Toggle Crosshair` |

### Wheels

| Position | Direction | Binding |
| --- | --- | --- |
| Wheel 1 | Clockwise | `Mac Native Hotkey: LeftMeta+LeftShift+=` |
| Wheel 1 | Counter-clockwise | `Mac Native Hotkey: LeftMeta+-` |
| Wheel 2 | Clockwise | `Down` |
| Wheel 2 | Counter-clockwise | `Up` |

The Command-plus shortcut uses `LeftShift+=` because `+` is the hotkey separator.

### Enabled Filters

```text
Smart Monitor Cycle Filter
Pen Drag Scroll Filter
```

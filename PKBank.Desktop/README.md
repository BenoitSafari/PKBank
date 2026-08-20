# PKBank.Desktop

Native cross-platform UI for PKHeX, built with [Avalonia](https://avaloniaui.net/). Runs natively on Linux (no Wine), and also on Windows/macOS.

This project lives alongside `PKHeX.WinForms` and never modifies it, so rebasing this branch onto upstream `master` stays conflict-free. It only references `PKHeX.Core` (all game logic, formats, and legality come from there).

## Run

```sh
dotnet run --project PKBank.Desktop
```

Optionally pass a save file path to open it on startup:

```sh
dotnet run --project PKBank.Desktop -- /path/to/main
```

## Current features

- Open any save file supported by PKHeX.Core (auto-detected), or create a blank save (Gen 1 → Legends: Z-A)
- Box navigation and party view with the real box sprites (shiny variants included)
- Slot context menu (right-click): View / Set (write editor content into the slot) / Delete
- Drag & drop: move a Pokémon between slots (swap when the target is occupied), or drop onto a file manager folder to export it as a .pk* file
  - Known limitation: on KDE Plasma (Wayland session), the drag cursor stays "forbidden" for XWayland apps even though the drop works — KDE bug 496243, not fixable app-side; the ghost sprite is the reliable drag indicator
- Rich hover preview on slots: ball icon, ability, level, IVs/EVs, nature, legality verdict, encounter and PID type
- Ball sprites shown in the ball selector (closed and dropdown) and hover preview
- Pokémon editor: species, form, nickname, level, nature, ability, held item, ball, language, gender, shiny, moves, IVs/EVs with live computed stats
- Stats panel parity with WinForms: color-graded base stats and BST, per-stat IV/EV caps, IV/EV column totals with legality grading (510 cap), randomize buttons (click random · Ctrl max · Alt clear)
- Live legality check with full report
- QR code window (button next to the Pokémon name), same payload as PKHeX Windows
- Export the modified save (File → Save As…)

## Architecture

| Piece | Role |
|---|---|
| `Sprites/SpriteService` | Loads box sprites from the PNGs shared with `PKHeX.Drawing.PokeSprite` (embedded at build time, cached `Bitmap`s). The resource-name logic (`SpriteName.cs`) is compile-linked from that project — single source of truth, no Windows dependency. |
| `ViewModels/MainWindowViewModel` | Save lifecycle, box/party slots, slot selection, export. |
| `ViewModels/PokemonEditorViewModel` | Wraps a working `PKM` copy; every mutation refreshes sprite, stats and legality. `Apply` writes back to the slot. |
| `Views/` | XAML views (compiled bindings, Fluent theme). |

## Roadmap (parity with WinForms, incrementally)

- [x] Drag & drop between slots and export to the filesystem (import from .pk* files still to do)
- [ ] Showdown set import/export
- [ ] Met location / origin editor tab
- [ ] OT/handler, memories, ribbons/marks tabs
- [ ] Trainer info editor (name, TID/SID, money, badges)
- [ ] Inventory editor
- [ ] Mystery Gift import
- [ ] Batch editor
- [ ] Localization (reuse PKHeX translation files)

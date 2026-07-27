# UiKit (vendored)

These files are a **copy**. The authoring version lives in a separate repository
(`Dalamud_UI_Framework`) that is not published.

## Which copy to edit

**Edit the authoring copy, never these files.**

`Logoria.csproj` has a `SyncVendoredUiKit` target that runs before every build. If
the authoring checkout is sitting next to this repo, it copies the current source
and the three shared textures in. So:

- Change the kit in its own repo, build Logoria, and the change appears here and
  in `git status` ready to commit.
- Change a file in this folder directly and the next build overwrites it. The sync
  is one-directional on purpose: two editable copies is how they drift.

On a machine without the authoring checkout the condition is false, the target
does nothing, and these committed files build as-is. That is what makes this
repository clone-and-build on its own.

## Why vendored rather than a submodule or a package

A submodule would require the kit repository to be published, or a credentialed
checkout in CI, for a dependency that is only ever consumed by this one plugin.
A NuGet package would mean a shared DLL and version skew between plugins.

Copying the source is the boring option and it has the property that matters most
here: **nothing else has to exist for this repo to build.**

## Files

| File | Contents |
| --- | --- |
| `UiPalette.cs` | Every colour, metric and effect strength, as a swappable record |
| `UiTheme.cs` | Applies the palette to ImGui's global style; `StyleScope` |
| `UiThemes.cs` | The ten named presets |
| `Ui.cs` | Drawn components, depth effects, textures, fonts |
| `UiAnim.cs` | Per-widget animation state for an immediate-mode UI |
| `ThemedWindow.cs` | `Window` base that themes the window frame |

The three textures that belong to the kit rather than to Logoria are vendored by
the same sync and land in `assets\`: `noise.png`, `shadow.png` and
`glowing_border.png`.

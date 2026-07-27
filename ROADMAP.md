# Logoria roadmap

## Next up

Ordered by impact per unit of effort. The first two are the ones that will change
how the plugin *feels*, and neither needs any new assets.

### ~~1. Icons in the nav rail~~: DONE

FontAwesome via `UiBuilder.FontIcon`, handed to the kit each frame as
`Ui.IconFont` so the kit does not reach for Dalamud services itself. `Ui.NavItem`
takes an optional glyph and centres it in a fixed column, since FontAwesome glyphs
vary in width and ragged icons would undo the alignment they are meant to provide.

Note `ImFontPtr` has no measure method in these bindings: push the font, call
`ImGui.CalcTextSize`, pop.

### ~~2. Motion~~: DONE

`UiAnim` in the kit: one float per key, eased toward whatever target the caller
passes, so callers stay stateless. Wired into nav hover fades, the rail's progress
bar, and a slow pulse on the ready-to-synthesise pill. Settings has an on/off and a
speed multiplier.

Two details that matter:

* **Framerate independence.** The step is an exponential decay against delta time,
  so an animation takes the same wall-clock time at 30fps and 144fps. A linear step
  would run at whatever speed the machine happens to render.
* **Stale entries are swept.** Without it the store grows for every widget ever
  drawn, including rows scrolled out of a long list and never seen again.

Still unanimated, if wanted later: the nav selection sliding between rows, which
needs group-level state rather than per-widget state.

### ~~3. Mneme icons in recipes~~: DONE

Drawn in the dex table, the floating tracker, the farming plan and every recipe
tooltip. `UIHelpers.DrawGameIcon` reserves the space whether or not the texture has
loaded, so a slow load cannot shift the layout around it.

### ~~4. Monospace figures~~: DONE

`Ui.TextMono` via `UiBuilder.FontMono`, used for every `have/need` pair and the
registered count. Falls back to the normal font when no mono face is supplied.

### ~~5. Smoother curves~~: DONE

`CircleTessellationMaxError` lowered from ImGui's 0.30 to 0.12.

This one has **no PushStyleVar**, so it must be assigned on the shared style and
put back afterwards. `StyleScope` captures the old value and restores it on
dispose; leaving it changed would alter how every other plugin's rounded corners
tessellate.

### ~~6. Custom font~~: DONE (needs a font file)

`FontService` loads the first `.ttf`/`.otf` from the plugin's `assets/fonts/`
folder and every window pushes it in `PreDraw`, alongside the theme, so the title
bar and sizing use it too.

**No font ships**, so this currently does nothing: with no file present no handle
is created and no atlas rebuild is triggered. Drop Inter, Figtree or Rubik into
`assets/fonts/` and it lights up. Settings reports which font is in use.

Size is configurable but needs a reload, since the atlas is built once at startup
rather than per frame.

## Assets

The finished PNGs are **committed** and are what the build copies, so a clean
clone runs without regenerating anything.

`assets/generate_assets.py` and its input renders (`assets/sources/`, or wherever
`LOGORIA_ART` points) are **not committed**. They are authoring tools rather than
build inputs, and keeping them out means the repo carries the artwork rather than
the machinery that made it. The consequence to know: regenerating an asset needs
the local copy of that script. The reasoning behind each asset is written down
below, so the knowledge does not leave with it.

Logoria's own artwork, in `assets/`:

| Asset | Status | Use |
| --- | --- | --- |
| `logo.png` (256) | **not shipped** | README and plugin listing. Derived from `Logoria_Logo_Transparent.png` |
| `logo_mark.png` (64) | **wired** | Crystal in the nav rail header |
| `banner.jpg` (1024x256) | **wired** | Header strip on the dex page, centre-cropped and faded into the panel |
| `watermark.png` (256) | **wired** | Faint crystal behind an empty farm list |
| `nav_highlight.png` (64, white) | **dropped** | Superseded by the procedural nav selection, below. Now in `assets/Not_Used/` |

The kit's own textures. Nothing about them is Logoria-specific: grain is grain,
and a gaussian 9-slice shadow is the same shadow for every plugin. They are
authored in the UI kit repo and **vendored** into `assets\` by the
`SyncVendoredUiKit` build target, alongside the kit source in `UiKit\`.

| Asset | Use |
| --- | --- |
| `noise.png` (128, alpha 6-10) | Tiled grain over every surface, "Film grain" slider |
| `shadow.png` (256, gaussian) | 9-sliced, replaces the stacked-rect fallback |
| `glowing_border.png` (64, white) | 9-sliced selection ring in the collection log |

Sprites are **white on purpose**. ImGui tints by multiplying, so a coloured source
can only darken or shift hue: tinting a cyan frame gold comes out green. White
carries the shape in its alpha and the caller supplies the theme's accent.
`generate_assets.py` runs `whiten_sprite` last over every hand-made sprite, so this
holds no matter what colours a create function used.

### Nav selection: procedural beats the sprite

`nav_highlight.png` was built, wired, looked at in game and then **dropped**. A
sprite has to bake its fill, inner glow, gloss and rim at one fixed resolution, so
at nav row height it softened, and its corner radius could not follow the theme's
`Rounding`. The procedural path (opaque gradient, 1px accent rect, bevel, gloss,
accent bar down the left edge) stays crisp at any row height and tracks the palette
exactly, which is the whole point of the palette being swappable.

`Ui.NavHighlightTexture` is still supported by the kit for plugins that want a
sprite; Logoria simply sets it to null. The file stays in `assets\` and is still
generated, it is just not shipped and not loaded.

The generation lessons are worth keeping even though the sprite is not. Two PIL
gotchas, both hit while building it:

* **`Image.paste(im, box, mask)` replaces pixels including alpha.** Pasting a
  mostly-transparent glow layer through a solid pill mask punched the base fill
  back out and left the sprite hollow. Use `alpha_composite` to stack translucent
  layers, and clip by multiplying into the layer's own alpha channel.
* **A 9-sliced sprite needs a flat centre.** The middle is what stretches, so
  variation there smears. `nav_highlight.png` holds its centre third to a spread
  of 20, which is safely flat.

`Logoria.csproj` lists shipped assets **explicitly** rather than globbing with
exclusions, so a new source render dropped into `assets\` cannot quietly end up in
the package. Everything listed there is loaded by `AssetService`.

`Services/AssetService.cs` loads them through `ITextureProvider` and hands the
handles to the kit each frame. Textures load asynchronously, so every effect keeps
its procedural fallback and the kit stays asset-free for other plugins.

### Logo source: solved

`Logoria_Logo_Transparent.png` has a genuine alpha channel, so nothing needs
extracting and there is no chequer residue. `generate_assets.py` prefers it
outright and only falls back to `unmix_glow` for sources that bake a chequer or a
solid background into the pixels.

Two earlier sources are kept as alternates in `assets/Not_Used/`: `logo_glow.png`
(luminance-as-alpha from the black-background render, heavier glow, cyan wordmark)
and the originals. Neither ships, since real alpha beats derived alpha: luminance
alpha leaves the whole logo partly translucent, so the panel shows through the
crystal body.

Always judge a logo composited over the panel colour, never on a white preview.
The wordmark is white and vanishes against white, which looks exactly like a
broken alpha channel and is not.

## Feature backlog

- ~~**Map pins for farming targets.**~~ DONE. Coordinates for 20 NMs and the sprite
  spawns were researched from the community wikis; `Data/EurekaLocations.cs` holds
  them with a confidence per entry, and the farming plan shows a Map button per
  place. `MapLinkPayload` takes **map** coordinates directly, so no map-to-world
  conversion is needed despite `OpenMapWithMapLink` also offering a world overload.
  Zone ids read from the game: Anemos 732/414, Pagos 763/467, Pyros 795/484,
  Hydatos 827/515.

  Two things to keep in mind if this data is ever revised. The wikis are community
  maintained and several of these enemies spawn in more than one place, so entries
  are presented as "should be accurate", never as fact. And Eureka's adaptation
  mechanic **levels a sprite up in place**: there is no separate Lv.46 Thunderstorm
  Sprite location, it is the Lv.41 spawn under different weather. Anything not
  taken from an explicit wiki Locations row is marked Approximate and shown with a
  `~` in the UI.
- **Held-actions array.** `Configuration.HeldActionsNumberArray` defaults to -1.
  Once found via the diagnostics scan, the dex records everything you hold rather
  than only what is slotted. **Low value now**: Drake's log sync gives all 56
  registration states outright, so this only adds "actions you are carrying but
  have not slotted", which nothing in the UI needs. It is the last open question
  diagnostics was built to answer.

- ~~**Job affinity is pre-Dawntrail.**~~ DONE and verified against the game's own
  `Action.ClassJobCategory` for all 56 actions.

  The bundled list was wrong in two ways beyond the missing modern jobs: **Wisdom
  of the Platebearer** omitted SCH and **Dispel L** omitted DNC. Nowhere did it
  claim a job the game does not, so the sheet is strictly better and now wins.

  Three findings shaped the summariser:

  * **The 56 actions span two id ranges**, 12958-13007 and 14476-14481. A
    contiguous sweep from 12958 runs into Ashes to Ashes and friends, which are
    unrelated actions carrying no ClassJobCategory.
  * **`IsUniversal` was broken.** The generated database writes the sentinel
    `"all"` for the seventeen universal actions rather than 21 entries, so a
    `Count >= 16` test scored them as one job and the fallback path reported
    "Unknown".
  * **Only one category is partial:** 141, Stealth L, usable by everything except
    NIN (which has Hide already). Naming all five roles for it read as "All Roles"
    while being wrong for the one job most likely to try it, so a role is now only
    named when every job in it is present, and near-total categories render as
    "All Roles except NIN".
- **Custom font.** `FontService` is written and wired; it needs a TTF dropped into
  `assets/fonts/`. Inter, Figtree or Rubik. Check the licence permits
  redistribution before shipping one.

## The UI kit is vendored, not referenced

`UiKit\` holds a copy of the shared kit's source, and `assets\noise.png`,
`shadow.png` and `glowing_border.png` are its textures. Both are committed, so
**this repository clones and builds with nothing else present.** The authoring
repo is not published and does not need to be.

`Logoria.csproj`'s `SyncVendoredUiKit` target refreshes the copy before every
build, but only when the authoring checkout exists next to this one. On any other
machine the condition is false and the committed copy is used as-is.

Two properties worth preserving:

* **The sync is automatic, not a script to remember.** The failure mode of a
  manual sync is committing a stale copy and shipping code that does not match the
  source it was written in. Running at build time means a kit change lands in this
  repo's `git status` the next time Logoria is built.
* **It is one-directional.** Edits belong in the authoring copy. Anything changed
  directly in `UiKit\` is overwritten on the next build, which is what stops two
  editable copies from drifting.

Verified 2026-07-26: renaming the authoring checkout away and doing a clean
`dotnet build -c Release` succeeds with no warnings.

## Before public release

Nothing here is code. It is everything a plugin needs that a working plugin does
not.

- **Version control.** There is no `.git` at all. `.gitignore` exists and is
  correct; the repo has simply never been initialised. Remote goes under the
  `BoujeeBecky` account.
- **`README.md`.** Does not exist. `logo.png` is in `assets\` and unshipped
  specifically to be the README image.
- **`pluginmaster.json`.** Missing, so there is no repo URL to install from. The
  plugin's own `Logoria.json` manifest is **already handled**: DalamudPackager
  generates it from the csproj metadata and produces `latest.zip` on every Release
  build. `<Tags>` is already correctly semicolon separated.
- **`LICENSE`.** Not chosen.
- **Release flow.** Deathroll Manager's GitHub Actions workflow is the model:
  tag `vX.Y.Z`, Actions builds and attaches `latest.zip`.
- **In-game verification.** None of the recent work has been seen running: the
  map pin buttons, the job affinity summaries, the ellipsised dex rows, vanilla
  mode, the procedural nav selection, hidden diagnostics, the Help page, and the
  Eureka content director fix. The director fix in particular can only be checked by being in
  Eureka, and the alternative it guards against is Occult Crescent.

## Security review

Audited before release. The good news first, because it shapes everything else:

* **No network access.** No `HttpClient`, no sockets, nothing phones home. All
  data is either compiled in or read from the local game client.
* **No chat sending.** `ProcessChatBoxEntry` is never called. The only chat output
  is `IChatGui.Print`, which is client-side text nobody else sees. The
  ChatSender reference file in `Dalamud Dev Info - GIT IGNORE\` is **not compiled**
  (`DefaultItemExcludes`), so nothing can call it by accident.
* **No process launching, no shell, no reflection over user input.**
* **File writes are one file, in one place.** Diagnostics reports go to
  `GetPluginConfigDirectory()` with a generated timestamp name. No user-supplied
  path ever reaches the filesystem.
* **String AtkValues are never dereferenced.** They are reported by type name
  only, which keeps text out of reports and avoids a null-pointer read.

Two things were found and fixed:

* **Auto-fill fired UI callbacks from the render thread.** The Fill button is drawn
  in `Draw()`, but `FireCallback` drives the game's UI, which lives on the main
  thread. Between resolving the addon pointer and firing, the game thread could
  tear that addon down: a client crash, not a catchable exception. Fill is now
  queued via `RequestAutoFill` and executed in `ProcessPending` from
  `IFramework.Update`. Only the newest request survives, so clicking Fill on three
  actions quickly loads the last one rather than replaying all three into a
  three-slot array.
* **`IntArray[0]` was read before checking `Size`.** A zero-length number array is
  legal, and that was a one-int read past the end.

A third was found on a second pass, and it is the interesting one:

* **The configurable addon name was a confused-deputy hole.**
  `Configuration.ManipulatorAddonName` exists so a patch that renames the window
  can be fixed without a plugin update. But `TryAutoFill` sends UI callbacks to
  whatever it names, so any other value turned the Fill button into "fire callback
  32 with args 0-2 into some other game window". Nobody sets that on purpose. The
  realistic path is social: a config file is a text file, and "paste this to fix
  your plugin" is advice people follow.

  Fixed with a closed list of the three real windows
  (`ManipulatorService.KnownAddons`), enforced in three places: `Migrate` resets an
  unknown value at load, the `AddonName` property falls back on every read so a
  hand-edited config never reaches a call site, and `TryAutoFill` re-checks
  immediately before firing. Diagnostics still *lists* every addon it sees, since
  that is the point of the tab, but "Use this" only appears for a name on the list.

  **Tradeoff, stated plainly:** if a patch renames the manipulator window, config
  can no longer route around it and the allowlist needs a one-line update. That is
  the right trade for the one field that decides where callbacks land, and
  diagnostics can still discover and report the new name.

And a fourth, which was over-collection rather than a vulnerability:

* **The lifecycle listeners were registered globally.** `RegisterListener` was
  called with no addon filter, so `PostSetup` fired for every window in the game
  and recorded the name of each one, and `PreReceiveEvent` fired for every UI
  interaction anywhere. The event handler discarded non-Eureka names before
  storing, but the setup handler kept everything, and the callback ran for all of
  it either way.

  Dalamud has scoped overloads, `RegisterListener(AddonEvent, IEnumerable<string>,
  handler)`, so capture is now bound to four names: the three manipulator panels
  and `EurekaMagiaActionNotebook`. In the normal case the callbacks are never
  invoked for anything else, so opening a retainer, an FC chest or a trade window
  is not merely unrecorded, it is unseen.

  Breadth is still available as **wide scan**, off by default, sitting next to the
  capture buttons rather than buried in settings, and labelled with what it does.
  It exists for one job: a patch renamed a window and we need its new name. Reports
  say when it was on.

  Two details that matter if this is revisited. Unregistering must use the **same
  overload** that registered, since scoped and global are separate subscriptions
  and the wrong one leaves the other live; `listenersAreWide` records which was
  used. And the storage-side name filter was kept even though scoped listeners make
  it redundant, so the rule about what gets recorded does not depend on how the
  subscription happened to be bound.

Standing properties worth preserving:

* **The plugin cannot consume materials.** Auto-fill loads the Astral Array and
  stops; Extract Mneme is never sent. This is the safety property that matters
  most to a player and it should stay a hard rule, not a setting.
* **Every native read is bounds-checked**, and the ones that walk a run copy into
  a managed array first (`LogosLogReader`, `EurekaStateProbe.ReadCurrent` uses the
  struct's own bounded span). Worst case is a wrong number, not a crash.
* **Diagnostics reports say what they contain** before you press Copy, since they
  are meant to be pasted to someone else.
* **The array scanner is a filter, not a dump.** It walks every UI number array
  but only *reports* one if it already contains Logos item or action ids or
  matches the Logos stride layout, and then only its first 24 integers. Arrays
  belonging to the rest of the game are read, scored, and discarded. Keep that
  filter if the scanner is ever extended: it is what stops a debugging aid from
  becoming a general memory-dumping tool.
* **No code-execution surface.** No `Assembly.Load`, `Activator.CreateInstance`,
  `BinaryFormatter`, `TypeNameHandling`, `Marshal` interop or dynamic compilation
  anywhere in the plugin. The config deserialises into one known type through
  Dalamud's own loader.
* **Nothing external can trigger anything.** No IPC provider is registered, no
  chat command is parsed for arguments, and there is no listener of any kind. Every
  action starts with a click or a slash command from the person at the keyboard.

Not a vulnerability, but the honest risk statement: this is a third-party plugin
that reads game memory and sends UI callbacks, which is against the game's terms
of service regardless of how carefully it is written. That is a risk the user
accepts by running Dalamud at all, and nothing here increases it: no automation
of gameplay, no packet sending, no input injection.

## Known limitations

- **No backdrop blur.** CSS `backdrop-filter: blur()` cannot be reproduced. It
  requires sampling the framebuffer behind an element, and ImGui only appends
  geometry to a draw list. Glass mode compensates with tint, a bright hairline
  edge and sheen; what sits behind stays sharp.
- **Auto-fill stops short of synthesising.** Deliberate. The trigger for Extract
  Mneme was never identified and does not need to be: leaving the final click to
  the player means the plugin can never consume materials.
- ~~**Table column widths do not persist.**~~ DONE. `NoSavedSettings` is off and
  drags are remembered. The original problem was real, so it is handled rather
  than ignored: ImGui's saved widths outrank `TableSetupColumn`, so stale ones
  from an older layout silently squeezed the new columns. The table id now carries
  a layout version *and* `Configuration.TableLayoutEpoch`, so a layout change or
  the new Settings, Layout, "Reset table column widths" button starts from fresh
  defaults instead of inheriting them. **Bump `_v2` in the id whenever the columns
  change.**
- ~~**Nav selection does not slide.**~~ DONE, in the kit as
  `Ui.BeginNavGroup` / `Ui.EndNavGroup`. Used by the main rail and the Help
  section list.

  Two things that had to be got right. The highlight is drawn by
  `BeginNavGroup`, not by the selected item, because it must sit behind every
  row's text and an immediate-mode list cannot paint under something it already
  drew; it therefore uses the rectangle recorded on the previous frame, which is
  invisible at any real framerate. And that rectangle is stored as an **offset
  from the group origin**, never as screen coordinates, or the highlight detaches
  the moment the window moves or the list scrolls.

  `NavItem` gained `slide: false` for the rail's window toggles. More than one of
  those can be on at once, and a single sliding highlight cannot be in two places;
  they keep the static treatment, which is also more honest, since "this window is
  open" is a different statement from "you are here".

## Diagnostics: development build only

**Nothing in the plugin's normal operation uses capture.** Verified by grep:
`Diagnostics`, `CallbackCapture` and `StateProbe` are referenced from exactly one
file, `UI/DiagnosticsWindow.cs`. The features read what they need directly, none
of it through capture:

| Feature | Source |
| --- | --- |
| Mneme stock | number array 137, read directly |
| Registered actions | `EurekaMagiaActionNotebook` AtkValues, read directly |
| Equipped actions | `DutyActionManager`, read directly |
| Auto-fill | *sends* callbacks, never observes them |

Capture was a discovery tool. Discovery is finished: the addon names, array 137's
layout and callback ids 14 and 32 are all confirmed and in code.

So it is **compiled out of Release**, not hidden at runtime. `Logoria.csproj`
defines `LOGORIA_DIAG` for Debug only and `<Compile Remove>`s the four files in
every other configuration, so the shipped assembly has no addon listeners, no
`FireCallback` hook, no memory probe and no window for any of it.

This reverses an earlier decision in this file. The old argument was that a
Release-build user hitting a post-patch breakage would want diagnostics available.
That is real but weak: the fix ships as a plugin update either way, and it is not
worth shipping a global UI hook to everyone for it.

**Debug and Release are not interchangeable.** Debug is the dev plugin, Release is
what goes in `latest.zip`.

### Proving it

`Tools\VerifyRelease` reads the built assembly's **metadata only**, through
`MetadataLoadContext`, so it never executes plugin code:

```
dotnet run --project Tools\VerifyRelease -- bin\Release\Logoria.dll
```

It asserts the seven capture types are absent and that `System.Net.Http`,
`System.Net.Sockets` and `System.Net.Primitives` are not referenced. That last
part matters more than reading the source and finding no `HttpClient`: an
assembly cannot open a socket without referencing something that can.

Run it against `bin\Debug\Logoria.dll` too. That must **fail**, listing all seven
types. A verifier that passes both builds is broken.

Confirmed 2026-07-26: Release has 72 types and references only Dalamud,
Dalamud.Bindings.ImGui, FFXIVClientStructs, Lumina, Lumina.Excel and six
`System.*` core assemblies.

### Why not a separate diagnostics plugin

Considered and rejected before the compile-out approach. The whole interaction is
"scan, see a candidate, press Use this, and it is saved", and the values land in
Logoria's own `Configuration`. A second plugin cannot write another plugin's
config, so a split version could only print numbers to copy back by hand. It would
also need its own copy of `LogosDatabase` and `MnemeDatabase` to score arrays.
Compiling out of Release achieves the same goal with none of that.

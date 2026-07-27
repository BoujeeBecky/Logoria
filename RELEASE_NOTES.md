# Initial Release - a collection log for Eureka Logos Actions

Eureka has 56 Logos Actions and the game gives you almost no help tracking them.
Logoria knows which ones you have registered, which ones you could synthesise
right now with the mnemes in your bag, and where to farm what you are missing.

## Fills itself in

- **Syncs from Drake's Logos Action Log.** Open his log once and your whole dex
  fills in. No ticking boxes, no materials spent. The game already keeps that
  record, which is how armour augmentation knows you have all 56.
- **Records what you slot**, permanently, even after you unslot it.
- **Per character**, and it says so on screen rather than letting an empty dex
  look like lost data.

## Tells you what you can make now

- **Live mneme stock** read from the Logos Manipulator while you are standing at
  it.
- **READY** highlighting for anything you hold the materials for but have never
  registered. That is the whole point of the dex.
- **Recipes** show the combination you can actually make, or the cheapest one if
  you cannot make any, with have/needed counts.

## Plans the farming

- **One shopping list** across everything you are working toward, grouped by the
  logogram that yields each mneme, so one trip covers several actions.
- **Map pins** per location, with a confidence marker. Coordinates are community
  sourced, so they should be accurate rather than are accurate, and anything
  approximate says why.
- **Floating tracker** you can pin on screen while you farm.

## Auto-fill that cannot cost you anything

**Fill** clears the Astral Array and loads a combination into it. It deliberately
stops there. It never presses Extract Mneme, so the plugin cannot consume your
materials by accident or by bug.

## Looks how you want

Ten themes, applied live, with individual controls for gradient, bevel, shine,
film grain, animation and domed shading. **Vanilla mode** strips all of it to
plain ImGui for anyone who would rather spend the frames on the game.

## No network, no surprises

Logoria has **no network access of any kind**, never sends chat, and watches only
the four Eureka windows it needs. The released build is compiled without the
development diagnostics tooling rather than merely disabling it.

Both claims are checkable on the shipped DLL:

```
dotnet run --project Tools\VerifyRelease -- <path-to>\Logoria.dll
```

It reads assembly metadata only and never executes plugin code.

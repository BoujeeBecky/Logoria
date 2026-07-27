# Logoria data layer

`LogosDatabase.g.cs` and `MnemeDatabase.g.cs` are **generated**. Do not hand-edit them.

## The three tiers

Eureka's Logos system has three distinct item tiers, and conflating them is the
single easiest way to get this wrong:

| Tier | Item / row ids | Examples |
| --- | --- | --- |
| **Logogram** (unidentified) | 24007-24014, 24809 | Conceptual, Offensive, Mitigative, Inimical |
| **Mneme** (deciphered) | 24015-24038, 24810-24813 | Wisdom of the Aetherweaver, Protect L, Cure L |
| **Logos Action** | 12958-13007, 14476-14481 | the 56 dex entries |

Recipes consume **mnemes**, never raw logograms. Deciphered mnemes also do not
live in your normal bags, which is why `InventoryManager.GetInventoryItemCount`
reports zero for all of them; see `Services/MnemeInventoryService.cs`.

## Mneme category vs. mneme source

These are two different things and conflating them gives wrong farming advice.

A mneme's **category** (Offensive, Protective, Curative, Tactical, Inimical,
Mitigative) comes from the game's own `EurekaMagiciteItemType` sheet. The
**logogram that yields it** comes from the tracker dataset's JSON:API
`relationships.logogram` link.

They often disagree. Wisdom of the Aetherweaver is category *Offensive* but drops
from the **Conceptual** logogram, because Conceptual, Fundamental and Obscure are
mixed grab-bags. Only the six specialised logograms hold category-matching mnemes.
Use `MnemeDatabase.SourceOf(itemId)` for "where do I farm this", never the category.

## What is generated vs. what is live

Only facts that cannot be read at runtime are baked into the generated files:
action row id, icon id, job list, effect tags, and the recipe tables.

Names and descriptions are **not** baked in. They are read live from the `Action`
and `ActionTransient` sheets via `Services/GameTextService.cs`, so the dex follows
the client's language for free. `FallbackName` is only used if that lookup fails.

## Regenerating

Two inputs are merged:

1. **Recipes** from the public tracker at <https://ffxiv-eureka.com/logograms>.
   The site is an Ember app that ships its dataset inside its JS bundle as
   JSON:API records (`{id, type:"logos-action", attributes:{..., combinations}}`),
   keyed by site-internal 1-based indices.
2. **Real game ids** from your own FFXIV install, read with Lumina: the `Action`
   sheet (row id + icon, matched by name), the `Item` sheet (mneme item ids), and
   `EurekaMagiciteItem` (the authoritative 28-mneme whitelist).

The tooling lives in `Tools/`:

```
node Tools/extract-recipes.js      # bundle -> eureka_extracted.json
dotnet run --project Tools/GameDataMerge   # + game sheets -> logoria_db.json
node Tools/codegen.js              # -> Data/*.g.cs
```

`Tools/extract-recipes.js` expects the current bundle URL, which is content
hashed and changes when the site deploys. Re-read the `<script src>` list from
<https://ffxiv-eureka.com/logograms> if the download 404s.

### Validation

The merge step fails loudly rather than silently dropping entries. A good run
reports:

```
resolved actions: 56/56  (missing 0)
resolved mnemes : 28/28
icon range: 64601 - 64656, zero icons: 0
total recipes: 115
distinct mneme itemIds used: 28
```

## Credit

Recipe combinations are sourced from <https://ffxiv-eureka.com/logograms>.
`apetih/LogogramHelper` covers similar ground and was useful for confirming the
manipulator's UI number-array layout; its recipe data was independently
cross-checked against ours and agrees.

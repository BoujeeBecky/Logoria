using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

class Program
{
    /// <summary>Finds the game's sqpack folder from XIVLauncher, then common install paths.</summary>
    static string ResolveSqpack()
    {
        var fromEnv = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        if (!string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv)) return fromEnv;

        var launcherConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "launcherConfigV3.json");

        if (File.Exists(launcherConfig))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(launcherConfig));
                if (doc.RootElement.TryGetProperty("GamePath", out var gamePath))
                {
                    var candidate = Path.Combine(gamePath.GetString()!, "game", "sqpack");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { /* fall through to the guesses below */ }
        }

        foreach (var root in new[]
                 {
                     @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn",
                     @"C:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn",
                 })
        {
            var candidate = Path.Combine(root, "game", "sqpack");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the FFXIV sqpack folder. Set FFXIV_SQPACK to it and re-run.");
    }

    static void Main()
    {
        // Reads eureka_extracted.json from, and writes logoria_db.json to, the
        // Tools\ directory (two levels up from bin\<cfg>\<tfm>).
        var scratch = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sqpack = ResolveSqpack();
        Console.WriteLine($"[info] game data: {sqpack}");
        var data = new GameData(sqpack);

        // ---- Actions via raw sheet ----
        // The typed Lumina Action struct fails to load against current game
        // versions, so read raw columns instead. Column 0 is Name, column 8 is Icon
        // (verified: row 12958 = "Wisdom of the Aetherweaver", icon 64601).
        var rawActions = data.Excel.GetSheet<RawRow>(Lumina.Data.Language.English, "Action");
        Console.WriteLine($"[info] raw Action rows: {rawActions.Count}");

        var actionByName = new Dictionary<string, (uint Id, ushort Icon, ushort Cjc)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rawActions)
        {
            string n;
            try { n = row.ReadString(0).ExtractText(); } catch { continue; }
            if (string.IsNullOrEmpty(n)) continue;
            ushort icon = 0, cjc = 0;
            try { icon = row.ReadUInt16(8); } catch { }
            if (!actionByName.ContainsKey(n)) actionByName[n] = (row.RowId, icon, cjc);
        }

        // ---- Items by name ----
        var items = data.GetExcelSheet<Item>(Lumina.Data.Language.English);
        var itemByName = new Dictionary<string, (uint Id, ushort Icon)>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
        {
            var n = it.Name.ExtractText();
            if (!string.IsNullOrEmpty(n) && !itemByName.ContainsKey(n))
                itemByName[n] = (it.RowId, it.Icon);
        }

        // ---- EurekaMagiaAction: the Logos Action log's own 1-56 index ----
        // Column 0 is the Action row id. The in-game Logos Action Log is indexed by
        // THIS sheet's row id, not by the Action id, so the dex needs the mapping.
        var magiaByActionId = new Dictionary<uint, uint>();
        var magiaSheet = data.Excel.GetSheet<RawRow>(Lumina.Data.Language.None, "EurekaMagiaAction");
        foreach (var row in magiaSheet)
        {
            if (row.RowId == 0) continue;
            try
            {
                var actionId = (uint)row.ReadInt32(0);
                if (actionId != 0) magiaByActionId[actionId] = row.RowId;
            }
            catch { }
        }
        Console.WriteLine($"[info] EurekaMagiaAction entries: {magiaByActionId.Count}");

        // ---- The mneme whitelist straight from EurekaMagiciteItem ----
        var magicite = data.GetExcelSheet<EurekaMagiciteItem>();
        var mnemeItemIds = new HashSet<uint>();
        foreach (var r in magicite)
            if (r.Item.RowId != 0) mnemeItemIds.Add(r.Item.RowId);
        Console.WriteLine($"[diag] EurekaMagiciteItem mneme items: {mnemeItemIds.Count}");

        // ---- Merge with the scraped recipe data ----
        var extracted = JsonDocument.Parse(File.ReadAllText(Path.Combine(scratch, "eureka_extracted.json"))).RootElement;

        var mnemeOut = new List<object>();
        var mnemeIdxToItemId = new Dictionary<int, uint>();
        foreach (var m in extracted.GetProperty("mnemes").EnumerateArray())
        {
            var idx = m.GetProperty("idx").GetInt32();
            var name = m.GetProperty("name").GetString();
            if (!itemByName.TryGetValue(name, out var hit))
            { Console.WriteLine($"  !! mneme not in Item sheet: '{name}'"); continue; }
            if (!mnemeItemIds.Contains(hit.Id))
                Console.WriteLine($"  ?? '{name}' ({hit.Id}) not in EurekaMagiciteItem");
            mnemeIdxToItemId[idx] = hit.Id;
            mnemeOut.Add(new
            {
                idx,
                name,
                itemId = hit.Id,
                icon = hit.Icon,
                category = m.GetProperty("type").GetString(),
                logogramIdx = m.TryGetProperty("logogramIdx", out var lg) ? lg.GetInt32() : 0,
            });
        }

        // ---- Logograms: the unidentified items that yield mnemes ----
        var logogramOut = new List<object>();
        foreach (var l in extracted.GetProperty("logograms").EnumerateArray())
        {
            var idx = l.GetProperty("idx").GetInt32();
            var shortName = l.GetProperty("name").GetString();
            var fullName = $"{shortName} Logogram";

            if (!itemByName.TryGetValue(fullName, out var hit))
            {
                Console.WriteLine($"  !! logogram not in Item sheet: '{fullName}'");
                continue;
            }

            logogramOut.Add(new
            {
                idx,
                name = fullName,
                itemId = hit.Id,
                icon = hit.Icon,
                acquiredBy = l.TryGetProperty("acquired-by", out var by)
                    ? (by.ValueKind == JsonValueKind.Array
                        ? by.EnumerateArray().Select(x => x.GetString()).ToArray()
                        : new[] { by.GetString() })
                    : Array.Empty<string>(),
            });
        }
        Console.WriteLine($"resolved logograms: {logogramOut.Count}/9");

        var actionOut = new List<object>();
        int missing = 0;
        foreach (var a in extracted.GetProperty("logosActions").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString();
            if (!actionByName.TryGetValue(name, out var hit))
            { Console.WriteLine($"  !! action not in Action sheet: '{name}'"); missing++; continue; }

            var recipes = new List<List<object>>();
            foreach (var combo in a.GetProperty("combinations").EnumerateArray())
            {
                var slots = combo.EnumerateArray()
                    .Select(x => int.Parse(x.GetString()))
                    .GroupBy(i => i)
                    .Select(g => new { itemId = mnemeIdxToItemId[g.Key], count = g.Count() })
                    .Cast<object>().ToList();
                recipes.Add(slots);
            }

            double Num(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0d;
            string Str(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
            string[] Arr(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Select(x => x.GetString()).ToArray() : Array.Empty<string>();

            magiaByActionId.TryGetValue(hit.Id, out var magiaIndex);
            if (magiaIndex == 0) Console.WriteLine($"  ?? '{name}' has no EurekaMagiaAction row");

            actionOut.Add(new
            {
                actionId = hit.Id,
                magiaIndex,
                name,
                icon = hit.Icon,
                type = Str("type"),
                uses = Num("uses"),
                cast = Num("cast"),
                recast = Num("recast"),
                jobs = Arr("jobs"),
                tags = Arr("attributes"),
                recipes,
            });
        }

        Console.WriteLine($"\nresolved actions: {actionOut.Count}/56  (missing {missing})");
        Console.WriteLine($"resolved mnemes : {mnemeOut.Count}/28");

        var outPath = Path.Combine(scratch, "logoria_db.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(
            new { actions = actionOut, mnemes = mnemeOut, logograms = logogramOut },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {outPath}");

        Console.WriteLine("\n--- first 6 resolved actions ---");
        foreach (var a in actionOut.Take(6)) Console.WriteLine(JsonSerializer.Serialize(a));
    }
}

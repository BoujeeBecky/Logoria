using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>One slot of the in-game Logos Action Log grid.</summary>
    public sealed record LogEntry(int Slot, LogosAction? Action, bool Registered);

    /// <summary>A decoded read of the in-game Logos Action Log.</summary>
    public sealed record LogReadResult(
        string Source,
        int Offset,
        int Stride,
        string Method,
        IReadOnlyList<LogEntry> Entries)
    {
        public int SetCount => Entries.Count(e => e.Registered);

        /// <summary>
        /// Reads that identify actions from the value itself are far more trustworthy
        /// than a positional flag run, which could be any unrelated array.
        /// </summary>
        public bool SelfIdentifying => Method is "icon ids" or "action ids";

        public string Describe() =>
            $"{SetCount}/56 registered  [{Method}]  from {Source}";

        /// <summary>
        /// Identifies a read by what it actually concluded, so overlapping windows
        /// over the same data collapse into one candidate.
        /// </summary>
        public string Signature() =>
            Method + ":" + string.Join(",", Entries
                .Where(e => e.Registered && e.Action != null)
                .Select(e => e.Action!.ActionId)
                .OrderBy(id => id));

        /// <summary>First few registered names, to tell similar candidates apart.</summary>
        public string PreviewNames(int take = 4)
        {
            var names = Entries
                .Where(e => e.Registered && e.Action != null)
                .Select(e => e.Action!.FallbackName)
                .Take(take)
                .ToList();

            if (names.Count == 0) return "(none)";
            var suffix = SetCount > names.Count ? ", ..." : string.Empty;
            return string.Join(", ", names) + suffix;
        }
    }

    /// <summary>
    /// Reads the Logos Action Log that Drake shows you, so the dex can be seeded
    /// from your real history in one go, with no synthesis and no materials.
    /// <para>
    /// The log draws as a grid of action icons, so the underlying data is most
    /// likely one entry per slot holding an icon id (or an action id), with
    /// unregistered slots either zero or a shared placeholder. A plain 0/1 flag run
    /// is supported too, but ranks lowest because it cannot prove what it is.
    /// </para>
    /// </summary>
    public unsafe class LogosLogReader
    {
        /// <summary>There are exactly 56 Logos Actions, so the log is a 56-entry run.</summary>
        public const int ExpectedEntries = 56;

        /// <summary>The log window, confirmed in-game 2026-07-25.</summary>
        public const string LogAddonName = "EurekaMagiaActionNotebook";

        private static readonly Dictionary<int, LogosAction> ByIcon = BuildIconMap();
        private static readonly Dictionary<int, LogosAction> ByActionId = BuildActionMap();

        private static Dictionary<int, LogosAction> BuildIconMap()
        {
            var map = new Dictionary<int, LogosAction>();
            foreach (var a in LogosDatabase.Actions) map[a.IconId] = a;
            return map;
        }

        private static Dictionary<int, LogosAction> BuildActionMap()
        {
            var map = new Dictionary<int, LogosAction>();
            foreach (var a in LogosDatabase.Actions) map[(int)a.ActionId] = a;
            return map;
        }

        public bool IsLogOpen()
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(LogAddonName);
                return !wrapper.IsNull && wrapper.IsReady && wrapper.IsVisible;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Every plausible read of the log, most trustworthy first.</summary>
        public List<LogReadResult> FindLog()
        {
            var results = new List<LogReadResult>();

            try
            {
                ScanAddonValues(results);
                ScanNumberArrays(results);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Logos Action Log scan failed.");
            }

            // A valid run matches at many overlapping offsets, which produced dozens
            // of identical candidates. Collapse anything that yields the same set of
            // registered actions down to a single entry.
            var deduped = new List<LogReadResult>();
            var seenSignatures = new HashSet<string>();

            foreach (var result in results
                         .OrderByDescending(r => r.SelfIdentifying)
                         .ThenByDescending(r => r.SetCount))
            {
                if (!seenSignatures.Add(result.Signature())) continue;
                deduped.Add(result);
            }

            return deduped;
        }

        private void ScanAddonValues(List<LogReadResult> results)
        {
            var stage = AtkStage.Instance();
            if (stage == null) return;

            var manager = stage->RaptureAtkUnitManager;
            if (manager == null) return;

            var list = manager->AllLoadedUnitsList;
            var count = Math.Min((int)list.Count, list.Entries.Length);

            for (var i = 0; i < count; i++)
            {
                var unit = list.Entries[i].Value;
                if (unit == null || !unit->IsVisible) continue;

                var name = unit->NameString;
                if (string.IsNullOrEmpty(name) ||
                    !name.StartsWith("Eureka", StringComparison.Ordinal)) continue;

                var values = unit->AtkValuesSpan;
                if (values.Length < ExpectedEntries) continue;

                var numbers = new int?[values.Length];
                for (var v = 0; v < values.Length; v++)
                {
                    numbers[v] = values[v].Type switch
                    {
                        AtkValueType.Int => values[v].Int,
                        AtkValueType.UInt => (int)values[v].UInt,
                        AtkValueType.Bool => values[v].Byte != 0 ? 1 : 0,
                        _ => null,
                    };
                }

                CollectRuns(numbers, $"addon {name}", results);
            }
        }

        private void ScanNumberArrays(List<LogReadResult> results)
        {
            var framework = Framework.Instance();
            if (framework == null) return;

            var uiModule = framework->GetUIModule();
            if (uiModule == null) return;

            var rapture = uiModule->GetRaptureAtkModule();
            if (rapture == null) return;

            var holder = rapture->AtkModule.AtkArrayDataHolder;

            for (var index = 0; index < holder.NumberArrayCount; index++)
            {
                var array = holder.GetNumberArrayData(index);
                if (array == null || array->IntArray == null) continue;
                if (array->Size < ExpectedEntries) continue;

                var numbers = new int?[array->Size];
                for (var i = 0; i < array->Size; i++) numbers[i] = array->IntArray[i];

                CollectRuns(numbers, $"number array {index}", results);
            }
        }

        private static void CollectRuns(int?[] numbers, string source, List<LogReadResult> results)
        {
            // One buffer reused for every candidate window. Allocating a fresh
            // 56-element array per offset per stride meant millions of short-lived
            // allocations for a single scan of a large array, and the GC pressure
            // showed up as a visible hitch. TryIdentityRun never retains it.
            var window = new int?[ExpectedEntries];

            // Icon and action-id runs identify themselves, so look for them first and
            // across every stride. Flag runs are a last resort.
            for (var stride = 1; stride <= 8; stride++)
            {
                var span = (ExpectedEntries - 1) * stride;

                for (var offset = 0; offset + span < numbers.Length; offset++)
                {
                    // Cheap reject before doing any copying: a run cannot start on a
                    // slot that held a string or another non-numeric value.
                    if (numbers[offset] is null) continue;

                    for (var i = 0; i < ExpectedEntries; i++) window[i] = numbers[offset + (i * stride)];

                    var byIcon = TryIdentityRun(window, ByIcon, "icon ids");
                    if (byIcon != null)
                    {
                        results.Add(new LogReadResult(source, offset, stride, "icon ids", byIcon));
                        continue;
                    }

                    var byAction = TryIdentityRun(window, ByActionId, "action ids");
                    if (byAction != null)
                        results.Add(new LogReadResult(source, offset, stride, "action ids", byAction));
                }
            }

            // Only bother with flag runs if nothing self-identifying turned up.
            if (results.Any(r => r.Source == source && r.SelfIdentifying)) return;

            for (var stride = 1; stride <= 8; stride++)
            {
                var span = (ExpectedEntries - 1) * stride;

                for (var offset = 0; offset + span < numbers.Length; offset++)
                {
                    // Validate before building anything. The old version allocated a
                    // 56-entry list up front and threw it away the moment the first
                    // value failed, which was almost every offset.
                    var ok = true;
                    var set = 0;

                    for (var i = 0; i < ExpectedEntries; i++)
                    {
                        var value = numbers[offset + (i * stride)];
                        if (value is not (0 or 1)) { ok = false; break; }
                        if (value == 1) set++;
                    }

                    if (!ok || set == 0) continue;

                    var entries = new List<LogEntry>(ExpectedEntries);
                    for (var i = 0; i < ExpectedEntries; i++)
                    {
                        var registered = numbers[offset + (i * stride)] == 1;
                        entries.Add(new LogEntry(i + 1, LogosDatabase.ByMagiaIndex((uint)(i + 1)), registered));
                    }

                    results.Add(new LogReadResult(source, offset, stride, "0/1 flags", entries));
                    return; // one flag candidate per source is plenty
                }
            }
        }

        /// <summary>
        /// Accepts a window where every value is either "empty" or a recognised id.
        /// Unregistered slots may be 0 or a single shared placeholder value, so one
        /// repeated unrecognised value is tolerated and treated as empty.
        /// </summary>
        private static List<LogEntry>? TryIdentityRun(
            int?[] window, Dictionary<int, LogosAction> lookup, string method)
        {
            int? placeholder = null;
            var recognised = 0;

            // First pass: work out whether a single unrecognised value is acting as
            // the empty-slot placeholder.
            var unknowns = new HashSet<int>();
            foreach (var value in window)
            {
                if (value is null) return null;
                var v = value.Value;
                if (v == 0 || lookup.ContainsKey(v)) continue;
                unknowns.Add(v);
                if (unknowns.Count > 1) return null;
            }

            if (unknowns.Count == 1) placeholder = unknowns.First();

            var entries = new List<LogEntry>(ExpectedEntries);
            var seen = new HashSet<int>();

            for (var i = 0; i < window.Length; i++)
            {
                var v = window[i]!.Value;
                var empty = v == 0 || v == placeholder;

                if (empty)
                {
                    // Fall back to positional identity so the row still names an action.
                    entries.Add(new LogEntry(i + 1, LogosDatabase.ByMagiaIndex((uint)(i + 1)), false));
                    continue;
                }

                // A real log never lists the same action twice.
                if (!seen.Add(v)) return null;

                entries.Add(new LogEntry(i + 1, lookup[v], true));
                recognised++;
            }

            return recognised == 0 ? null : entries;
        }

        /// <summary>
        /// In-use arrays large enough to hold the log, previewed for manual decoding
        /// when the automatic detection comes up empty.
        /// </summary>
        public List<(int Index, int Size, string Preview)> CandidateLogArrays()
        {
            var found = new List<(int, int, string)>();

            try
            {
                var framework = Framework.Instance();
                if (framework == null) return found;

                var uiModule = framework->GetUIModule();
                if (uiModule == null) return found;

                var rapture = uiModule->GetRaptureAtkModule();
                if (rapture == null) return found;

                var holder = rapture->AtkModule.AtkArrayDataHolder;

                for (var index = 0; index < holder.NumberArrayCount; index++)
                {
                    var array = holder.GetNumberArrayData(index);
                    if (array == null || array->IntArray == null) continue;
                    if (array->Size < ExpectedEntries) continue;
                    if (array->RefCount <= 0) continue;

                    // Anything mentioning a Logos icon or action id is worth seeing.
                    var interesting = false;
                    for (var i = 0; i < array->Size; i++)
                    {
                        var v = array->IntArray[i];
                        if (ByIcon.ContainsKey(v) || ByActionId.ContainsKey(v)) { interesting = true; break; }
                    }

                    if (!interesting) continue;
                    found.Add((index, array->Size, PreviewInts(array->IntArray, array->Size, 160)));
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Failed to scan candidate log arrays.");
            }

            return found;
        }

        /// <summary>Raw AtkValues of the log window, for when the heuristic finds nothing.</summary>
        public string DumpLogAddonValues()
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(LogAddonName);
                if (wrapper.IsNull) return "(log window not open)";

                var addon = (AtkUnitBase*)wrapper.Address;
                var values = addon->AtkValuesSpan;

                var sb = new StringBuilder();
                sb.AppendLine($"{LogAddonName}: {values.Length} AtkValues");

                for (var i = 0; i < values.Length && i < 500; i++)
                {
                    var v = values[i];
                    var text = v.Type switch
                    {
                        AtkValueType.Int => v.Int.ToString(),
                        AtkValueType.UInt => v.UInt.ToString(),
                        AtkValueType.Bool => (v.Byte != 0).ToString(),
                        AtkValueType.Undefined => "-",
                        _ => v.Type.ToString(),
                    };
                    sb.Append($"[{i}]={text}  ");
                    if ((i + 1) % 8 == 0) sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"(failed: {ex.Message})";
            }
        }

        private static string PreviewInts(int* values, int size, int take)
        {
            var count = Math.Min(size, take);
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(values[i]);
            }
            if (size > count) sb.Append(", ...");
            return sb.ToString();
        }

        /// <summary>
        /// One-shot sync: reads the log, takes the most trustworthy interpretation,
        /// and folds it into the dex. Only ever acts on a self-identifying read, so
        /// an ambiguous flag run can never silently corrupt the dex.
        /// </summary>
        public (bool Read, int Added, int Registered) TrySyncBest(Configuration configuration)
        {
            if (!IsLogOpen()) return (false, 0, 0);

            var best = FindLog().FirstOrDefault(r => r.SelfIdentifying);
            if (best == null) return (false, 0, 0);

            var added = ImportIntoDex(best, configuration);
            return (true, added, best.SetCount);
        }

        /// <summary>Writes a decoded read into the dex. Returns how many were newly recorded.</summary>
        public int ImportIntoDex(LogReadResult result, Configuration configuration)
        {
            var contentId = LogosDexService.CurrentContentId;
            if (contentId == 0) return 0;

            var added = 0;
            foreach (var entry in result.Entries)
            {
                if (entry.Action == null || !entry.Registered) continue;
                if (configuration.IsObtained(contentId, entry.Action.ActionId)) continue;

                configuration.MarkObtained(contentId, entry.Action.ActionId);
                added++;
            }

            if (added > 0) configuration.Save();
            return added;
        }
    }
}

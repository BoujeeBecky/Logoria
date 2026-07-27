using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using Logoria.Data;

namespace Logoria.Services
{
    public sealed record ArrayCandidate(
        int Index,
        int Size,
        int HeaderCount,
        int MnemeMatches,
        int ActionMatches,
        bool MatchesStrideLayout,
        string Preview)
    {
        /// <summary>Higher is more likely to be the manipulator's mneme stock array.</summary>
        public int MnemeScore => (MatchesStrideLayout ? 100 : 0) + (MnemeMatches * 10);

        /// <summary>Higher is more likely to be the held Logos Actions list.</summary>
        public int ActionScore => ActionMatches * 10;

        /// <summary>What this array most looks like.</summary>
        public string Guess => ActionMatches > MnemeMatches
            ? "held Logos Actions"
            : MnemeMatches > 0 ? "mneme stock" : "unclear";
    }

    public sealed record CapturedEvent(
        DateTime At,
        string AddonName,
        string EventType,
        int EventParam);

    /// <summary>
    /// Self-service replacement for poking around in /xldev. Finds the Logos
    /// Manipulator's addon name, locates the number array holding mneme stock, and
    /// records the UI events the addon receives, so the remaining unknowns can be
    /// filled in without reading raw memory by hand.
    /// </summary>
    public unsafe class DiagnosticsService : IDisposable
    {
        private static readonly HashSet<int> MnemeItemIds = BuildMnemeIds();
        private static readonly HashSet<int> ActionIds = BuildActionIds();

        private readonly List<CapturedEvent> events = new();
        private readonly SortedDictionary<string, int> addonsSeen = new(StringComparer.Ordinal);
        private readonly List<string> recentAddons = new();

        // AddonLifecycle callbacks arrive on the game thread; the UI reads on the
        // render thread. Guard every shared collection.
        private readonly object gate = new();

        private bool capturing;
        private bool listenersRegistered;
        private bool listenersAreWide;

        public bool IsCapturing => capturing;

        /// <summary>
        /// The only windows Logoria has any business watching: the three manipulator
        /// panels and Drake's log.
        /// </summary>
        private static readonly string[] ScopedAddons = BuildScopedAddons();

        private static string[] BuildScopedAddons()
        {
            var names = new List<string>(ManipulatorService.KnownAddons)
            {
                LogosLogReader.LogAddonName,
            };
            return names.ToArray();
        }

        /// <summary>
        /// Whether capture watches every window in the game rather than only
        /// Logoria's four.
        /// <para>
        /// Off by default, and it should stay that way. Scoped listeners are enough
        /// for everything diagnostics is normally for, and they mean the plugin never
        /// sees that you opened your retainer, your free company chest or a trade
        /// window. The wide mode exists for exactly one job: a patch renamed a window
        /// and we need to find out what it is called now.
        /// </para>
        /// </summary>
        public bool WideScan { get; private set; }

        /// <summary>
        /// Changes scope. Re-registers immediately if a capture is already running,
        /// since the listeners were bound with the old scope.
        /// </summary>
        public void SetWideScan(bool wide)
        {
            if (WideScan == wide) return;
            WideScan = wide;

            if (!listenersRegistered) return;

            UnregisterListeners();
            RegisterListeners();
        }

        /// <summary>A point-in-time copy, safe to enumerate while capturing.</summary>
        public CapturedEvent[] EventsSnapshot()
        {
            lock (gate) return events.ToArray();
        }

        public int EventCount
        {
            get { lock (gate) return events.Count; }
        }

        /// <summary>Addon names seen opening while capture was on, most recent first.</summary>
        public List<string> RecentAddons
        {
            get { lock (gate) return new List<string>(recentAddons); }
        }

        private static HashSet<int> BuildMnemeIds()
        {
            var set = new HashSet<int>();
            foreach (var m in MnemeDatabase.All) set.Add((int)m.ItemId);
            return set;
        }

        private static HashSet<int> BuildActionIds()
        {
            var set = new HashSet<int>();
            foreach (var a in LogosDatabase.Actions) set.Add((int)a.ActionId);
            return set;
        }

        // ---------------------------------------------------------------- capture

        public void StartCapture()
        {
            if (capturing) return;
            capturing = true;

            RegisterListeners();

            Service.Log.Information(
                $"Logoria diagnostics: capture started ({(WideScan ? "wide" : "scoped")}).");
        }

        /// <summary>
        /// Binds the lifecycle listeners. Scoped by addon name unless wide scan is
        /// on, so in the normal case the callbacks are never even invoked for
        /// windows that are none of Logoria's business.
        /// </summary>
        private void RegisterListeners()
        {
            if (listenersRegistered) return;

            if (WideScan)
            {
                Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonSetup);
                Service.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, OnAddonReceiveEvent);
            }
            else
            {
                Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, ScopedAddons, OnAddonSetup);
                Service.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, ScopedAddons, OnAddonReceiveEvent);
            }

            listenersRegistered = true;

            // Remembered rather than re-read from WideScan at unregister time: the
            // scoped and global registrations are separate subscriptions, so
            // unregistering with the wrong overload would leave one live.
            listenersAreWide = WideScan;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered) return;

            try
            {
                if (listenersAreWide)
                {
                    Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, OnAddonSetup);
                    Service.AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, OnAddonReceiveEvent);
                }
                else
                {
                    Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, ScopedAddons, OnAddonSetup);
                    Service.AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, ScopedAddons, OnAddonReceiveEvent);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Diagnostics: failed to unregister listeners.");
            }

            listenersRegistered = false;
        }

        public void StopCapture()
        {
            if (!capturing) return;
            capturing = false;
            Service.Log.Information("Logoria diagnostics: capture stopped.");
        }

        public void ClearCapture()
        {
            lock (gate)
            {
                events.Clear();
                addonsSeen.Clear();
                recentAddons.Clear();
            }
        }

        /// <summary>
        /// Whether a window is one Logoria has any reason to know about. Matches by
        /// substring rather than the exact list, because the point of watching at all
        /// is to notice a renamed Eureka window.
        /// </summary>
        private static bool IsRelevantAddon(string name) =>
            name.IndexOf("Eureka", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Mneme", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Magicite", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Magia", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Logos", StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnAddonSetup(AddonEvent type, AddonArgs args)
        {
            if (!capturing) return;

            try
            {
                var name = args.AddonName;
                if (string.IsNullOrEmpty(name)) return;

                // Same rule as events: record a window only if it is relevant, or if
                // wide scan was deliberately turned on to hunt for a renamed one.
                if (!WideScan && !IsRelevantAddon(name)) return;

                lock (gate)
                {
                    addonsSeen.TryGetValue(name, out var n);
                    addonsSeen[name] = n + 1;

                    recentAddons.Remove(name);
                    recentAddons.Insert(0, name);
                    if (recentAddons.Count > 30) recentAddons.RemoveAt(recentAddons.Count - 1);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Diagnostics: addon setup capture failed.");
            }
        }

        private void OnAddonReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!capturing) return;
            if (args is not AddonReceiveEventArgs receive) return;

            try
            {
                // Redundant while the listeners are scoped, and the whole safety net
                // in wide mode. Kept in both cases so the storage rule does not
                // depend on how the subscription happened to be bound.
                var name = receive.AddonName ?? string.Empty;
                if (!IsRelevantAddon(name)) return;

                var entry = new CapturedEvent(
                    DateTime.Now,
                    name,
                    receive.AtkEventType.ToString(),
                    receive.EventParam);

                lock (gate)
                {
                    events.Add(entry);
                    if (events.Count > 400) events.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Diagnostics: receive-event capture failed.");
            }
        }

        // ------------------------------------------------------------- addon list

        /// <summary>Every addon currently loaded, with its visibility.</summary>
        public List<(string Name, bool Visible)> ListLoadedAddons()
        {
            var result = new List<(string, bool)>();

            try
            {
                var stage = FFXIVClientStructs.FFXIV.Component.GUI.AtkStage.Instance();
                if (stage == null) return result;

                var manager = stage->RaptureAtkUnitManager;
                if (manager == null) return result;

                var list = manager->AllLoadedUnitsList;
                var count = Math.Min((int)list.Count, list.Entries.Length);

                for (var i = 0; i < count; i++)
                {
                    var unit = list.Entries[i].Value;
                    if (unit == null) continue;

                    var name = unit->NameString;
                    if (string.IsNullOrEmpty(name)) continue;

                    result.Add((name, unit->IsVisible));
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Diagnostics: addon enumeration failed.");
            }

            return result
                .OrderByDescending(x => x.Item2)
                .ThenBy(x => x.Item1, StringComparer.Ordinal)
                .ToList();
        }

        // ------------------------------------------------------- number array scan

        /// <summary>
        /// Scans every UI number array for one holding mneme stock. Checks the known
        /// stride-4 layout (count at [0], then stock/id pairs) and also does a plain
        /// scan for mneme item ids so an unexpected layout still gets flagged.
        /// </summary>
        public List<ArrayCandidate> ScanNumberArrays()
        {
            var candidates = new List<ArrayCandidate>();

            try
            {
                var framework = Framework.Instance();
                if (framework == null) return candidates;

                var uiModule = framework->GetUIModule();
                if (uiModule == null) return candidates;

                var rapture = uiModule->GetRaptureAtkModule();
                if (rapture == null) return candidates;

                var holder = rapture->AtkModule.AtkArrayDataHolder;

                for (var index = 0; index < holder.NumberArrayCount; index++)
                {
                    var array = holder.GetNumberArrayData(index);
                    if (array == null || array->IntArray == null) continue;

                    var size = array->Size;
                    if (size <= 1 || size > 8192) continue;

                    var mnemeMatches = 0;
                    var actionMatches = 0;
                    for (var i = 0; i < size; i++)
                    {
                        var v = array->IntArray[i];
                        if (MnemeItemIds.Contains(v)) mnemeMatches++;
                        else if (ActionIds.Contains(v)) actionMatches++;
                    }

                    var header = array->IntArray[0];
                    var stride = MatchesStrideLayout(array, size, header);

                    if (mnemeMatches == 0 && actionMatches == 0 && !stride) continue;

                    candidates.Add(new ArrayCandidate(
                        index, size, header, mnemeMatches, actionMatches, stride, Preview(array, size)));
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Diagnostics: number array scan failed.");
            }

            return candidates.OrderByDescending(c => Math.Max(c.MnemeScore, c.ActionScore)).ToList();
        }

        /// <summary>The layout LogogramHelper documented: [0]=count, then [4i]=stock, [4i+1]=id.</summary>
        private static bool MatchesStrideLayout(
            FFXIVClientStructs.FFXIV.Component.GUI.NumberArrayData* array, int size, int header)
        {
            if (header <= 0 || header > 64) return false;
            if ((header * 4) + 1 >= size) return false;

            var hits = 0;
            for (var i = 1; i <= header; i++)
            {
                var id = array->IntArray[(4 * i) + 1];
                if (MnemeItemIds.Contains(id)) hits++;
            }

            return hits >= Math.Max(1, header / 2);
        }

        private static string Preview(
            FFXIVClientStructs.FFXIV.Component.GUI.NumberArrayData* array, int size)
        {
            var take = Math.Min(size, 24);
            var sb = new StringBuilder();
            for (var i = 0; i < take; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(array->IntArray[i]);
            }
            if (size > take) sb.Append(", ...");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ report

        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Logoria diagnostics report ===");
            sb.AppendLine($"generated: {DateTime.Now:u}");
            sb.AppendLine();

            // Told, not assumed. This gets pasted to someone else, so it should say
            // what it holds. Note the scan is a filter, not a dump: an array is only
            // reported if it already contains Logos item or action ids, or matches
            // the Logos stride layout, and then only its first 24 integers. Arrays
            // belonging to the rest of the game are counted and discarded unread.
            sb.AppendLine("-- what is in this report --");
            sb.AppendLine("  Eureka window names, and for UI arrays that already contain Logos");
            sb.AppendLine("  ids, their index and first 24 numbers. Plus the ids of Eureka UI");
            sb.AppendLine("  events seen during capture.");
            sb.AppendLine("  No character name, no content id, no text of any kind.");
            if (WideScan)
                sb.AppendLine("  WIDE SCAN WAS ON: every window you opened is listed below.");
            sb.AppendLine();

            // Filtered to Eureka windows unless wide scan is deliberately on. The
            // report is written to be pasted somewhere public, and which unrelated
            // windows someone happened to have open is nobody's business.
            sb.AppendLine("-- visible Eureka windows --");
            foreach (var (name, visible) in ListLoadedAddons())
                if (visible && (WideScan || IsRelevantAddon(name)))
                    sb.AppendLine($"  {name}");
            sb.AppendLine();

            sb.AppendLine("-- addons opened during capture (most recent first) --");
            foreach (var name in RecentAddons) sb.AppendLine($"  {name}");
            sb.AppendLine();

            sb.AppendLine("-- number array candidates --");
            foreach (var c in ScanNumberArrays())
            {
                sb.AppendLine($"  [{c.Index}] size={c.Size} header={c.HeaderCount} " +
                              $"mnemeIds={c.MnemeMatches} actionIds={c.ActionMatches} " +
                              $"strideLayout={c.MatchesStrideLayout} looksLike={c.Guess}");
                sb.AppendLine($"      {c.Preview}");
            }
            sb.AppendLine();

            sb.AppendLine("-- captured Eureka UI events --");
            foreach (var e in EventsSnapshot())
                sb.AppendLine($"  {e.At:HH:mm:ss.fff}  {e.AddonName,-24} {e.EventType,-18} param={e.EventParam}");

            return sb.ToString();
        }

        // Unregisters through the same overload it registered with, so a scoped
        // subscription cannot be left live by a global unregister.
        public void Dispose() => UnregisterListeners();
    }
}

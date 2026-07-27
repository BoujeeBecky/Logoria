using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Logoria.Services;

namespace Logoria.UI
{
    /// <summary>
    /// Everything needed to finish wiring up the manipulator, without leaving the game.
    /// </summary>
    public class DiagnosticsWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string statusLine = string.Empty;

        public DiagnosticsWindow(Plugin plugin)
            : base("Logoria Diagnostics###LogoriaDiagnosticsWindow")
        {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(640, 460),
                MaximumSize = new Vector2(1600, 1200),
            };
            Size = new Vector2(820, 620);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw()
        {
            var diag = plugin.Diagnostics;

            ImGui.TextWrapped(
                "Run this at a Logos Manipulator. Step 1 finds the window's internal name, "
                + "step 2 finds where the game keeps your mneme counts, and step 3 records what "
                + "the window does when you click things.");

            ImGui.Spacing();
            DrawCaptureControls(diag);
            ImGui.Separator();

            if (ImGui.BeginTabBar("DiagTabs"))
            {
                if (ImGui.BeginTabItem("1. Addons"))
                {
                    DrawAddonsTab(diag);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("2. Mneme array"))
                {
                    DrawArrayTab(diag);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("3. Events"))
                {
                    DrawEventsTab(diag);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Import log (start here)"))
                {
                    DrawImportLogTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("4. Find the log"))
                {
                    DrawLogHuntTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("5. Callbacks"))
                {
                    DrawCallbacksTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawCaptureControls(DiagnosticsService diag)
        {
            if (diag.IsCapturing)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.20f, 0.20f, 1f));
                if (ImGui.Button("Stop capture")) diag.StopCapture();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored(UIHelpers.ObtainedGreen, "Capturing. Open the manipulator and click around.");
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.50f, 0.25f, 1f));
                if (ImGui.Button("Start capture")) diag.StartCapture();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored(UIHelpers.Dim, "Not capturing.");
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear")) diag.ClearCapture();

            ImGui.SameLine();
            if (ImGui.Button("Save report")) SaveReport(diag);

            ImGui.SameLine();
            if (ImGui.Button("Copy report"))
            {
                ImGui.SetClipboardText(diag.BuildReport());
                statusLine = "Report copied to clipboard.";
            }

            // Scope control sits with the capture buttons rather than in settings:
            // this is the switch that decides how much of the game Logoria watches,
            // so it should be visible at the moment someone starts watching.
            var wide = diag.WideScan;
            if (ImGui.Checkbox("Wide scan: watch every window, not just Eureka's", ref wide))
                diag.SetWideScan(wide);

            if (wide)
            {
                ImGui.SameLine();
                Theme.TextColored(UIHelpers.Gold,
                    "On: every window you open is recorded and appears in the report.");
            }

            // Said before the button is pressed, not after. A report meant to be
            // pasted to someone else should say what it contains first.
            Theme.TextColored(UIHelpers.Dim,
                "Reports list open window names, and only those UI arrays that already contain "
                + "Logos ids (first 24 numbers each). No character name, content id or text.");

            if (!string.IsNullOrEmpty(statusLine))
                ImGui.TextColored(UIHelpers.ObtainedGreen, statusLine);
        }

        private void SaveReport(DiagnosticsService diag)
        {
            try
            {
                var dir = Service.PluginInterface.GetPluginConfigDirectory();
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"logoria-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, diag.BuildReport());
                statusLine = $"Saved to {path}";
                Service.Log.Information($"Diagnostics report written to {path}");
            }
            catch (Exception ex)
            {
                statusLine = $"Save failed: {ex.Message}";
                Service.Log.Error(ex, "Failed to write diagnostics report.");
            }
        }

        private void DrawAddonsTab(DiagnosticsService diag)
        {
            ImGui.TextWrapped(
                "With capture on, open the Logos Manipulator. Its internal name will appear at "
                + "the top of 'Opened during capture'. That is the value ManipulatorService.AddonName needs.");
            ImGui.Spacing();

            ImGui.TextColored(UIHelpers.Gold, $"Currently configured: {plugin.Manipulator.AddonName}");
            ImGui.TextColored(
                plugin.Manipulator.IsManipulatorOpen() ? UIHelpers.ObtainedGreen : UIHelpers.Dim,
                plugin.Manipulator.IsManipulatorOpen()
                    ? "That addon is open right now, so the name is already correct."
                    : "That addon is not currently open.");

            ImGui.Separator();

            if (ImGui.CollapsingHeader("Opened during capture", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // One copy per frame; the property snapshots under a lock each call.
                var recent = diag.RecentAddons;

                if (recent.Count == 0)
                    ImGui.TextColored(UIHelpers.Dim, "Nothing yet. Start capture, then open the manipulator.");

                foreach (var name in recent)
                {
                    ImGui.Bullet();
                    ImGui.SameLine();
                    ImGui.TextUnformatted(name);
                    // "Use this" only offers the windows Logoria is allowed to drive.
                    // Everything else is still listed, because seeing what opened is
                    // the point of this tab, it just cannot be adopted as a callback
                    // target with one click.
                    ImGui.SameLine();
                    if (ManipulatorService.IsKnownAddon(name))
                    {
                        if (ImGui.SmallButton($"Use this##addon{name}"))
                        {
                            plugin.Configuration.ManipulatorAddonName = name;
                            plugin.Configuration.Save();
                            statusLine = $"Manipulator addon name set to '{name}'.";
                        }

                        ImGui.SameLine();
                    }

                    if (ImGui.SmallButton($"Copy##addon{name}")) ImGui.SetClipboardText(name);
                }
            }

            if (ImGui.CollapsingHeader("All visible addons right now"))
            {
                // A live look at your own screen, nothing recorded. Reports only ever
                // include Eureka windows unless wide scan is on, so what is listed
                // here does not end up in anything you paste.
                Theme.TextColored(UIHelpers.Dim,
                    "Live view only. Not recorded, and not included in reports.");

                foreach (var (name, visible) in diag.ListLoadedAddons())
                {
                    if (!visible) continue;
                    ImGui.Bullet();
                    ImGui.SameLine();
                    ImGui.TextUnformatted(name);
                }
            }
        }

        private void DrawArrayTab(DiagnosticsService diag)
        {
            ImGui.TextWrapped(
                "Scans every UI number array for your mneme item ids and for Logos Action ids. "
                + "Open the manipulator first so the arrays are populated. An array full of action "
                + "ids is the held-actions list, which lets the dex record everything you own "
                + "rather than only what you have slotted.");
            ImGui.Spacing();

            ImGui.TextColored(UIHelpers.Gold,
                $"Mneme stock array: index {plugin.Inventory.StockNumberArrayIndex}"
                + $"   (reading live right now: {(plugin.Inventory.HasLiveStock ? "yes" : "no")})");

            var held = plugin.Configuration.HeldActionsNumberArray;
            ImGui.TextColored(held >= 0 ? UIHelpers.Gold : UIHelpers.Dim,
                held >= 0
                    ? $"Held actions array: index {held}"
                    : "Held actions array: not found yet (dex reads equipped slots only)");
            ImGui.Spacing();

            if (ImGui.Button("Scan number arrays"))
            {
                var results = diag.ScanNumberArrays();
                statusLine = results.Count == 0
                    ? "No candidates found. Is the manipulator open?"
                    : $"Found {results.Count} candidate(s). Best: index {results[0].Index}.";
                cachedCandidates = results;
            }

            ImGui.Separator();

            if (cachedCandidates == null)
            {
                ImGui.TextColored(UIHelpers.Dim, "No scan run yet.");
                return;
            }

            if (cachedCandidates.Count == 0)
            {
                ImGui.TextColored(UIHelpers.ShortMneme,
                    "Nothing matched. Open the Logos Manipulator and scan again.");
                return;
            }

            foreach (var c in cachedCandidates)
            {
                var looksLikeActions = c.ActionMatches > c.MnemeMatches;
                var color = looksLikeActions ? UIHelpers.Gold : UIHelpers.ReadyCyan;

                ImGui.TextColored(color,
                    $"index {c.Index}   size {c.Size}   header {c.HeaderCount}   " +
                    $"mneme ids {c.MnemeMatches}   action ids {c.ActionMatches}   " +
                    $"stride {(c.MatchesStrideLayout ? "yes" : "no")}   looks like: {c.Guess}");

                if (ImGui.SmallButton($"Use as mneme stock##arr{c.Index}"))
                {
                    plugin.Configuration.ManipulatorStockNumberArray = c.Index;
                    plugin.Configuration.Save();
                    plugin.Inventory.Refresh();
                    statusLine = $"Mneme stock array set to index {c.Index}.";
                }

                ImGui.SameLine();
                if (ImGui.SmallButton($"Use as held actions##arr{c.Index}"))
                {
                    plugin.Configuration.HeldActionsNumberArray = c.Index;
                    plugin.Configuration.Save();
                    statusLine = $"Held actions array set to index {c.Index}. The dex will now read it.";
                }

                ImGui.SameLine();
                if (ImGui.SmallButton($"Copy##arr{c.Index}")) ImGui.SetClipboardText(c.Index.ToString());

                ImGui.PushStyleColor(ImGuiCol.Text, UIHelpers.Dim);
                ImGui.TextWrapped(c.Preview);
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }
        }

        private System.Collections.Generic.List<ArrayCandidate>? cachedCandidates;
        private int assumedBaseOffset;
        private System.Collections.Generic.List<LogReadResult>? logReads;
        private int selectedRead;
        private bool showRegisteredOnly;
        private string logFilter = string.Empty;
        private string customMarker = string.Empty;

        private static void DrawMarker(CallbackCaptureService capture, string label)
        {
            // The ImGui id is only the text after "##", so a shared suffix would make
            // every marker button the same widget and only one would respond.
            if (ImGui.Button($"{label}##mk_{label}")) capture.AddMarker(label);
        }

        private void DrawImportLogTab()
        {
            ImGui.TextWrapped(
                "The game already knows what you have made. Speak to Drake, the NPC beside the "
                + "Logos Manipulator, to open your Logos Action Log. While that window is open, "
                + "press Read below and Logoria will import the whole thing at once.");
            ImGui.Spacing();

            ImGui.TextColored(UIHelpers.Gold, "No synthesis and no materials needed.");
            ImGui.Spacing();

            var logOpen = plugin.LogReader.IsLogOpen();
            ImGui.TextColored(logOpen ? UIHelpers.ObtainedGreen : UIHelpers.ShortMneme,
                logOpen
                    ? $"{LogosLogReader.LogAddonName} is open."
                    : $"{LogosLogReader.LogAddonName} is not open. Talk to Drake first.");
            ImGui.Spacing();

            if (ImGui.Button("Read the log now"))
            {
                logReads = plugin.LogReader.FindLog();
                selectedRead = 0;
                statusLine = logReads.Count == 0
                    ? "No 56-entry log found. Is Drake's log window open?"
                    : $"Found {logReads.Count} candidate read(s).";
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear##log")) logReads = null;

            ImGui.Separator();

            if (logReads == null)
            {
                ImGui.TextColored(UIHelpers.Dim, "Nothing read yet.");
                return;
            }

            if (logReads.Count == 0)
            {
                ImGui.TextColored(UIHelpers.ShortMneme,
                    "No 56-entry run found. If the log window IS open, the data is shaped "
                    + "differently than expected. Use the fallbacks below and send me the output.");

                ImGui.Spacing();
                ImGui.TextColored(UIHelpers.Gold, "Fallback: dump the raw data");
                ImGui.TextWrapped(
                    "Copies every in-use array that could hold 56 per-action entries, plus the "
                    + "log window's own AtkValues. Paste that back to me and I will decode it.");

                if (ImGui.Button("Copy raw log data"))
                {
                    var used = plugin.LogReader.CandidateLogArrays();
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"-- candidate log arrays (log open: {plugin.LogReader.IsLogOpen()}) --");
                    foreach (var (index, size, preview) in used)
                        sb.AppendLine($"  [{index}] size={size}\n      {preview}");
                    if (used.Count == 0) sb.AppendLine("  (none found)");

                    sb.AppendLine();
                    sb.AppendLine(plugin.LogReader.DumpLogAddonValues());

                    ImGui.SetClipboardText(sb.ToString());
                    statusLine = $"Copied {used.Count} array(s) plus the raw AtkValues to your clipboard.";
                    Service.Log.Information(sb.ToString());
                }

                return;
            }

            // Candidate picker: the heuristic can match more than one region, so the
            // names below are the real check.
            // Keep the picker in a bounded, scrollable box. Letting it grow freely is
            // what squashed the results table down to a grey line.
            var pickerHeight = Math.Min(logReads.Count, 5) * (ImGui.GetTextLineHeightWithSpacing() + 2f) + 8f;

            if (ImGui.BeginChild("LogCandidates", new Vector2(0, pickerHeight), true))
            {
                for (var i = 0; i < logReads.Count; i++)
                {
                    var r = logReads[i];
                    if (ImGui.RadioButton($"{r.Describe()}##read{i}", selectedRead == i)) selectedRead = i;

                    if (r.SelfIdentifying)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(UIHelpers.ObtainedGreen, "(trustworthy)");
                    }

                    ImGui.TextColored(UIHelpers.Dim, $"      {r.PreviewNames()}");
                }
            }

            // EndChild is required even when BeginChild returns false.
            ImGui.EndChild();

            ImGui.Separator();

            var read = logReads[Math.Min(selectedRead, logReads.Count - 1)];

            ImGui.TextColored(UIHelpers.Gold, $"This read says you have registered {read.SetCount} of 56.");
            ImGui.TextWrapped(read.SelfIdentifying
                ? "This read names each action from the data itself, so if the list below matches "
                  + "the icons in Drake's window, it is correct."
                : "This read only sees on/off flags, so it is inferring names from position. "
                  + "Compare the count above against how many icons Drake's window shows before trusting it.");
            ImGui.Spacing();

            if (ImGui.Button("Import into my dex"))
            {
                var added = plugin.LogReader.ImportIntoDex(read, plugin.Configuration);
                statusLine = $"Imported {added} new dex entries.";
            }

            ImGui.SameLine();
            ImGui.Checkbox("Show registered only", ref showRegisteredOnly);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(160f);
            ImGui.InputTextWithHint("##logfilter", "filter by name...", ref logFilter, 64);

            ImGui.Spacing();

            const ImGuiTableFlags flags = ImGuiTableFlags.Borders
                                          | ImGuiTableFlags.RowBg
                                          | ImGuiTableFlags.ScrollY
                                          | ImGuiTableFlags.SizingFixedFit;

            // Use whatever height is left, but never collapse to nothing: a table with
            // no room renders as a bare line and hides every row.
            var tableHeight = Math.Max(220f, ImGui.GetContentRegionAvail().Y);
            if (!ImGui.BeginTable("LogReadTable", 3, flags, new Vector2(0, tableHeight))) return;

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Log #", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Registered", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            foreach (var entry in read.Entries)
            {
                if (showRegisteredOnly && !entry.Registered) continue;

                var label = entry.Action == null
                    ? "(no action at this slot)"
                    : plugin.GameText.ActionName(entry.Action);

                if (!string.IsNullOrWhiteSpace(logFilter) &&
                    label.IndexOf(logFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(entry.Slot.ToString());

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(label);

                ImGui.TableSetColumnIndex(2);
                ImGui.TextColored(
                    entry.Registered ? UIHelpers.ObtainedGreen : UIHelpers.Dim,
                    entry.Registered ? "yes" : "no");
            }

            ImGui.EndTable();
        }

        private void DrawLogHuntTab()
        {
            var probe = plugin.StateProbe;

            ImGui.TextWrapped(
                "The game does remember which actions you have registered: the Hydatos armour "
                + "augmentation unlocks at 56 unique entries. That record is not in PlayerState, "
                + "so the likely home is Eureka's own save block. Rather than guess, we diff it.");
            ImGui.Spacing();

            ImGui.TextColored(UIHelpers.Gold, "How to use this");
            ImGui.TextWrapped(
                "1. Stand in Eureka and press Take snapshot.\n"
                + "2. Synthesise a Logos Action you have never made before.\n"
                + "3. Press Diff. Whatever bit flipped is the log entry for that action.");
            ImGui.Spacing();

            if (!probe.IsInEureka())
            {
                ImGui.TextColored(UIHelpers.ShortMneme, "Not currently in Eureka, so there is no save block to read.");
                return;
            }

            ImGui.TextColored(UIHelpers.ObtainedGreen, "In Eureka. Save block is readable.");

            if (ImGui.Button("Take snapshot"))
            {
                statusLine = probe.TakeSnapshot()
                    ? $"Snapshot taken ({probe.SnapshotLength} bytes) at {probe.SnapshotAt:HH:mm:ss}."
                    : "Snapshot failed.";
            }

            ImGui.SameLine();
            if (ImGui.Button("Diff now")) statusLine = "Diffed.";

            ImGui.SameLine();
            if (ImGui.Button("Clear snapshot")) probe.ClearSnapshot();

            ImGui.SameLine();
            if (ImGui.Button("Copy hex dump")) ImGui.SetClipboardText(probe.HexDump());

            ImGui.Spacing();
            ImGui.SetNextItemWidth(140f);
            ImGui.InputInt("Assumed log base offset", ref assumedBaseOffset);
            ImGui.TextColored(UIHelpers.Dim,
                "Used only to translate a changed bit into a log index. Adjust until the name matches.");

            ImGui.Separator();

            if (!probe.HasSnapshot)
            {
                ImGui.TextColored(UIHelpers.Dim, "No snapshot yet.");
                return;
            }

            var deltas = probe.Diff();
            ImGui.TextColored(UIHelpers.Gold,
                $"Snapshot at {probe.SnapshotAt:HH:mm:ss}   changed bytes: {deltas.Count}");

            if (deltas.Count == 0)
            {
                ImGui.TextColored(UIHelpers.Dim,
                    "Nothing has changed yet. Go synthesise a brand new action, then press Diff.");
                return;
            }

            foreach (var delta in deltas)
            {
                ImGui.TextColored(UIHelpers.ReadyCyan,
                    $"offset 0x{delta.Offset:X4} ({delta.Offset})   " +
                    $"0x{delta.Before:X2} -> 0x{delta.After:X2}");

                foreach (var bit in delta.ChangedBits())
                {
                    var direction = delta.WasSet(bit) ? "set" : "cleared";
                    ImGui.TextColored(UIHelpers.Dim, $"    bit {bit} {direction}:");

                    foreach (var reading in probe.InterpretBit(delta.Offset, bit, assumedBaseOffset))
                    {
                        ImGui.SameLine(0f, 0f);
                        ImGui.NewLine();
                        ImGui.TextColored(UIHelpers.Dim, $"        {reading}");
                    }
                }

                ImGui.Spacing();
            }

            ImGui.Separator();
            ImGui.TextColored(UIHelpers.Gold, "Popcount search");
            ImGui.TextWrapped(
                "If your dex count is already accurate, a 7-byte window whose set-bit count "
                + "matches it is very likely the log.");

            var known = plugin.Dex.ObtainedCount();
            ImGui.Text($"Dex currently knows {known} obtained.");

            if (ImGui.Button("Find 7-byte windows with that many bits set"))
            {
                var hits = probe.FindCandidateBitfields(known);
                statusLine = hits.Count == 0
                    ? $"No 7-byte window has exactly {known} bits set."
                    : $"Windows matching {known} bits: " + string.Join(", ", hits.ConvertAll(h => $"0x{h.Offset:X4}"));
            }
        }

        private void DrawCallbacksTab()
        {
            var capture = plugin.CallbackCapture;

            ImGui.TextWrapped(
                "Tab 3 only sees ATK events, which is why every ListItemClick reported param 0: "
                + "the selected row lives in the list component, not the event. This tab hooks "
                + "FireCallback to record what the window actually sends to its agent, which is "
                + "what auto-fill needs to replay. The hook only reads, and always calls through.");
            ImGui.Spacing();

            if (capture.IsEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.20f, 0.20f, 1f));
                if (ImGui.Button("Stop capturing callbacks")) capture.Disable();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored(UIHelpers.ObtainedGreen, "Hook active.");
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.50f, 0.25f, 1f));
                if (ImGui.Button("Start capturing callbacks")) capture.Enable();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored(UIHelpers.Dim, "Hook inactive.");
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear##cb")) capture.Clear();

            ImGui.SameLine();
            if (ImGui.Button("Copy##cb"))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var c in capture.Snapshot())
                {
                    if (c.IsMarker)
                    {
                        sb.AppendLine($"{c.At:HH:mm:ss.fff}  === {c.Values} ===");
                        continue;
                    }

                    var closes = c.ClosesAddon ? "  [closes]" : string.Empty;
                    sb.AppendLine($"{c.At:HH:mm:ss.fff}  {c.AddonName,-30} {c.Values}{closes}");
                }
                ImGui.SetClipboardText(sb.ToString());
                statusLine = "Callbacks copied.";
            }

            var captureAll = capture.CaptureAllAddons;
            if (ImGui.Checkbox("Capture every addon (not just Eureka windows)", ref captureAll))
                capture.CaptureAllAddons = captureAll;

            if (capture.LastError != null)
                ImGui.TextColored(UIHelpers.ShortMneme, $"Error: {capture.LastError}");

            ImGui.Separator();

            // Markers make the capture self-documenting. A bare list of callbacks is
            // very hard to line up with what was actually clicked.
            ImGui.TextColored(UIHelpers.Gold, "Press a marker BEFORE each step, then do it:");

            DrawMarker(capture, "picked mneme");
            ImGui.SameLine();
            DrawMarker(capture, "put mneme in slot");
            ImGui.SameLine();
            DrawMarker(capture, "removed from slot");

            DrawMarker(capture, "pressed Synthesize");
            ImGui.SameLine();
            DrawMarker(capture, "confirmed Yes");
            ImGui.SameLine();
            DrawMarker(capture, "replaced held slot");

            ImGui.SetNextItemWidth(220f);
            ImGui.InputTextWithHint("##markertext", "custom marker...", ref customMarker, 64);
            ImGui.SameLine();
            if (ImGui.Button("Add##custommarker") && !string.IsNullOrWhiteSpace(customMarker))
            {
                capture.AddMarker(customMarker);
                customMarker = string.Empty;
            }

            ImGui.Separator();

            var rows = capture.Snapshot();

            if (rows.Length == 0)
            {
                ImGui.TextColored(UIHelpers.Dim,
                    "Nothing captured. Turn the hook on, then click a mneme and press Synthesize.");
                return;
            }

            const ImGuiTableFlags flags = ImGuiTableFlags.Borders
                                          | ImGuiTableFlags.RowBg
                                          | ImGuiTableFlags.ScrollY
                                          | ImGuiTableFlags.SizingFixedFit;

            var height = Math.Max(200f, ImGui.GetContentRegionAvail().Y);
            if (!ImGui.BeginTable("DiagCallbacks", 4, flags, new Vector2(0, height))) return;

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Addon", ImGuiTableColumnFlags.WidthFixed, 220);
            ImGui.TableSetupColumn("AtkValues", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Closes", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            // Oldest first, so the sequence reads in the order it happened.
            foreach (var c in rows)
            {
                ImGui.TableNextRow();

                if (c.IsMarker)
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                        ImGui.GetColorU32(new Vector4(0.30f, 0.26f, 0.08f, 0.65f)));

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(c.At.ToString("HH:mm:ss.fff"));
                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(c.AddonName);
                ImGui.TableSetColumnIndex(2);
                if (c.IsMarker) ImGui.TextColored(UIHelpers.Gold, c.Values);
                else ImGui.TextUnformatted(c.Values);
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(c.ClosesAddon ? "yes" : string.Empty);
            }

            ImGui.EndTable();
        }

        private void DrawEventsTab(DiagnosticsService diag)
        {
            ImGui.TextWrapped(
                "With capture on, click a mneme in the manipulator, then the mix button. Each row "
                + "is an event the window received. The event type and param are what auto-fill "
                + "needs to replay.");
            ImGui.Spacing();

            var rows = diag.EventsSnapshot();

            if (rows.Length == 0)
            {
                ImGui.TextColored(UIHelpers.Dim, "No Eureka UI events captured yet.");
                return;
            }

            const ImGuiTableFlags flags = ImGuiTableFlags.Borders
                                          | ImGuiTableFlags.RowBg
                                          | ImGuiTableFlags.ScrollY
                                          | ImGuiTableFlags.SizingFixedFit;

            if (!ImGui.BeginTable("DiagEvents", 4, flags, new Vector2(0, 360))) return;

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Addon", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableSetupColumn("Param", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (var i = rows.Length - 1; i >= 0; i--)
            {
                var e = rows[i];
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(e.At.ToString("HH:mm:ss.fff"));
                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(e.AddonName);
                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(e.EventType);
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(e.EventParam.ToString());
            }

            ImGui.EndTable();
        }
    }
}

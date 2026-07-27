using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Logoria.Data;
using Logoria.Services;
using UiKit;

namespace Logoria.UI
{
    /// <summary>
    /// The farming planner: pick what you are working toward, and see the combined
    /// shopping list of mnemes and where each one comes from.
    /// </summary>
    public class FarmingWindow : LogoriaWindow, IDisposable
    {
        private readonly Plugin plugin;

        /// <summary>Second click confirms Clear list. Reset whenever the list empties.</summary>
        private bool confirmingClear;

        public FarmingWindow(Plugin plugin)
            : base("Logoria - Farming Plan###LogoriaFarmingWindow")
        {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(720, 460),
                MaximumSize = new Vector2(1600, 1400),
            };
            Size = new Vector2(860, 600);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw() => DrawContent();

        /// <summary>Body only, so the main shell can host this as a page.</summary>
        public void DrawContent()
        {
            var config = plugin.Configuration;
            var farming = plugin.Farming;
            var dex = plugin.Dex;

            if (!dex.IsLoggedIn)
            {
                UIHelpers.CentredText("Log in to plan your farming.", UIHelpers.Dim);
                return;
            }

            var farmed = farming.FarmedActions();

            DrawHeader(farming, farmed.Count);
            ImGui.Separator();

            if (farmed.Count == 0)
            {
                // Nothing left to confirm; leaving it armed would make the next
                // "Clear list" press fire straight into a confirmation about nothing.
                confirmingClear = false;

                DrawEmptyWatermark();

                ImGui.Spacing();
                UIHelpers.CentredText("Nothing on your farm list yet.", UIHelpers.Dim);
                ImGui.Spacing();
                ImGui.TextWrapped(
                    "Add actions from the dex (the Farm button on each row) or from the collection "
                    + "log. Everything you add gets totalled into one shopping list below.");

                ImGui.Spacing();
                if (ImGui.Button("Add everything I can almost make"))
                    AddNearlyReady();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Adds unregistered actions that need only one more mneme.");

                return;
            }

            // Split width persists too, keyed the same way as the dex table so one
            // reset clears both.
            if (ImGui.BeginTable($"FarmSplit_v2##{config.TableLayoutEpoch}", 2,
                    ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableSetupColumn("Farming", ImGuiTableColumnFlags.WidthFixed, 300);
                ImGui.TableSetupColumn("Shopping list", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                DrawFarmList(config, farming, farmed);

                ImGui.TableSetColumnIndex(1);
                DrawShoppingList(farming);

                ImGui.EndTable();
            }
        }

        /// <summary>
        /// Faint crystal behind an empty farm list, so the page reads as intentional
        /// rather than unfinished. Drawn behind the text, low enough not to compete.
        /// </summary>
        private void DrawEmptyWatermark()
        {
            var watermark = plugin.Assets.Watermark;
            if (watermark == null) return;

            var avail = ImGui.GetContentRegionAvail();
            var size = Math.Min(Math.Min(avail.X, avail.Y) * 0.75f, 260f);
            if (size <= 32f) return;

            var origin = ImGui.GetCursorScreenPos();
            var min = new Vector2(
                origin.X + ((avail.X - size) * 0.5f),
                origin.Y + ((avail.Y - size) * 0.4f));

            ImGui.GetWindowDrawList().AddImage(
                watermark.Handle,
                min,
                new Vector2(min.X + size, min.Y + size),
                Vector2.Zero,
                Vector2.One,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        }

        private void DrawHeader(FarmingService farming, int count)
        {
            var progress = farming.OverallProgress();

            Theme.TextColored(UIHelpers.Gold, $"Farming {count} action{(count == 1 ? "" : "s")}");
            ImGui.SameLine();
            Theme.Text($"materials {progress * 100f:F0}% gathered");

            ImGui.SameLine();
            if (ImGui.SmallButton("Open tracker")) plugin.FloatingWindow.IsOpen = true;
            // Confirmed rather than immediate. It throws away a list that can take a
            // while to build, and it sits one pixel away from "Open tracker".
            ImGui.SameLine();
            if (confirmingClear)
            {
                if (ImGui.SmallButton($"Really clear all {count}?"))
                {
                    plugin.Configuration.FarmActionIds.Clear();
                    plugin.Configuration.TargetActionId = 0;
                    plugin.Configuration.Save();
                    confirmingClear = false;
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("Cancel")) confirmingClear = false;
            }
            else if (ImGui.SmallButton("Clear list"))
            {
                confirmingClear = true;
            }

            // The kit's bar, not ImGui's: this one sat next to the nav rail's and the
            // tracker's and was visibly a different control, with no recessed track
            // and no eased fill.
            Ui.ProgressBar(progress, 12f, $"{progress * 100f:F0}%", animateId: "farmheader");
        }

        private void DrawFarmList(Configuration config, FarmingService farming, System.Collections.Generic.List<LogosAction> farmed)
        {
            ImGui.BeginChild("FarmListChild", new Vector2(0, -1));

            foreach (var action in farmed)
            {
                var ready = farming.IsReady(action);
                var expanded = config.TargetActionId == action.ActionId;

                ImGui.PushID((int)action.ActionId);

                UIHelpers.DrawActionIcon(action, 22f);
                ImGui.SameLine();

                var color = ready ? UIHelpers.ReadyCyan : UIHelpers.Dim;
                if (ImGui.Selectable(plugin.GameText.ActionName(action), expanded))
                {
                    config.TargetActionId = expanded ? 0u : action.ActionId;
                    config.Save();
                }

                // SameLine takes an offset from the content region's left edge, so
                // passing the *remaining* width put the tag further left the longer
                // the action name was. Anchor it to the right edge instead, and skip
                // it entirely when there is nothing to say.
                if (ready)
                {
                    var tag = "ready";
                    ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(tag).X);
                    Theme.TextColored(color, tag);
                }

                if (expanded)
                {
                    ImGui.Indent(12f);
                    foreach (var need in farming.NeedsFor(action))
                        DrawNeedLine(need, compact: true);

                    // Points at the shopping list rather than repeating it here. One
                    // action can need three logograms and a dozen spawns between
                    // them, which does not fit a 300px column.
                    ImGui.Spacing();
                    Theme.TextColored(UIHelpers.Dim,
                        "Where to farm these, with map pins, is on the right.");
                    ImGui.Spacing();

                    if (ImGui.SmallButton("Remove from farm list"))
                        config.ToggleFarming(action.ActionId);

                    ImGui.Unindent(12f);
                    ImGui.Spacing();
                }

                ImGui.PopID();
            }

            ImGui.EndChild();
        }

        private void DrawShoppingList(FarmingService farming)
        {
            ImGui.BeginChild("ShoppingChild", new Vector2(0, -1));

            var groups = farming.NeedsByLogogram();

            if (groups.Count == 0)
            {
                Theme.TextColored(UIHelpers.Dim, "Nothing needed.");
                ImGui.EndChild();
                return;
            }

            // Said once at the top rather than repeated per row: these come from the
            // community wikis, so they should be right rather than are right. Only
            // shown when something below actually carries coordinates, otherwise it
            // is a caveat about nothing.
            if (groups.Any(g => EurekaLocations.ForLogogram(g.Source).Count > 0))
            {
                Theme.TextColored(UIHelpers.Dim,
                    "Coordinates are community-sourced and should be accurate. "
                    + "Entries marked ~ are approximate.");
                ImGui.Spacing();
            }

            foreach (var group in groups)
            {
                var header = group.Satisfied
                    ? $"{group.Source.FallbackName}  (done)"
                    : $"{group.Source.FallbackName}  ({group.TotalShort} still needed)";

                Theme.TextColored(group.Satisfied ? UIHelpers.ObtainedGreen : UIHelpers.Gold, header);

                // The acquisition line is the actual "where do I go" answer.
                Theme.TextColored(UIHelpers.Dim,
                    $"    {FarmingGuide.DescribeAll(group.Source.AcquiredBy)}");

                // Shown for every group, not only the ones you are short of. Hiding
                // them once satisfied meant the coordinates vanished exactly when
                // someone went looking for them.
                DrawPlaces(group.Source);

                foreach (var need in group.Mnemes)
                {
                    ImGui.Indent(12f);
                    DrawNeedLine(need, compact: false);
                    ImGui.Unindent(12f);
                }

                ImGui.Spacing();
            }

            ImGui.EndChild();
        }

        /// <summary>
        /// Places to go for a logogram, each opening the map on click.
        /// <para>
        /// Confidence is shown rather than hidden. The coordinates are community
        /// sourced, several of these enemies spawn in more than one place, and a few
        /// entries are a FATE marker or a nearby landmark rather than the enemy's own
        /// listing. A pin that only "should" be right must not look like one that is.
        /// </para>
        /// </summary>
        private void DrawPlaces(Logogram source)
        {
            var places = EurekaLocations.ForLogogram(source);

            ImGui.Indent(12f);

            // Say so rather than drawing nothing. Two logograms genuinely have no
            // enemy to pin (gold coffers and heatboxes are not dropped by a mob), and
            // silence there looks exactly like a missing feature.
            if (places.Count == 0)
            {
                Theme.TextColored(UIHelpers.Dim,
                    "No map coordinates: this one is not tied to a specific enemy.");
                ImGui.Unindent(12f);
                return;
            }

            Theme.TextColored(UIHelpers.Gold, "Where to farm");

            foreach (var place in places)
            {
                var approximate = place.Confidence == LocationConfidence.Approximate;
                var zone = EurekaLocations.ZoneInfo(place.Zone);

                ImGui.PushID($"{source.Index}_{place.Mob}_{place.X}_{place.Y}");

                if (Theme.ButtonGhost("Map", new Vector2(46f, 0)))
                    plugin.Maps.OpenMap(place);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Open {zone.Name} at {place.X:0.0}, {place.Y:0.0}");

                ImGui.SameLine(0f, 6f);
                Theme.TextColored(approximate ? UIHelpers.Dim : UIHelpers.ObtainedGreen, place.Mob);

                ImGui.SameLine(0f, 6f);
                Theme.TextMono($"{place.X:0.0}, {place.Y:0.0}", UIHelpers.Dim);

                ImGui.SameLine(0f, 6f);
                Theme.TextColored(UIHelpers.Dim, zone.Name.Replace("Eureka ", string.Empty));

                if (approximate)
                {
                    ImGui.SameLine(0f, 6f);
                    Theme.TextColored(Theme.Gold, "~");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(place.Note is null
                            ? "Approximate: start looking here."
                            : $"Approximate: {place.Note}.");
                }

                ImGui.PopID();
            }

            ImGui.Unindent(12f);
        }

        private void DrawNeedLine(MnemeNeed need, bool compact)
        {
            var name = plugin.GameText.MnemeName(need.ItemId);
            var color = need.Satisfied ? UIHelpers.ObtainedGreen : UIHelpers.ShortMneme;

            UIHelpers.DrawMnemeIcon(need.ItemId, ImGui.GetTextLineHeight());
            ImGui.SameLine(0f, 5f);

            // Mono keeps the have/need column aligned down a long shopping list.
            Theme.TextMono($"{need.Have}/{need.Needed}", color);
            ImGui.SameLine(0f, 6f);
            Theme.TextColored(color, name);

            if (compact || need.Satisfied) return;

            ImGui.SameLine();
            Theme.TextColored(UIHelpers.Dim, $"(need {need.Short} more)");
        }

        /// <summary>
        /// Convenience for a cold start: everything unregistered that is one mneme
        /// away from being craftable.
        /// </summary>
        private void AddNearlyReady()
        {
            var config = plugin.Configuration;
            var inventory = plugin.Inventory;
            var dex = plugin.Dex;

            foreach (var action in LogosDatabase.Actions)
            {
                if (dex.StateOf(action) == DexState.Obtained) continue;

                foreach (var recipe in action.Recipes)
                {
                    var missing = 0;
                    foreach (var slot in recipe.Slots)
                        missing += Math.Max(0, slot.Count - inventory.CountOf(slot.ItemId));

                    if (missing is > 0 and <= 1)
                    {
                        config.FarmActionIds.Add(action.ActionId);
                        break;
                    }
                }
            }

            config.Save();
        }
    }
}

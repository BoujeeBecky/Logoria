using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Logoria.Data;
using Logoria.Services;
using UiKit;

namespace Logoria.UI
{
    public class FloatingWindow : LogoriaWindow, IDisposable
    {
        private readonly Plugin plugin;

        public FloatingWindow(Plugin plugin)
            : base("Logoria Tracker###LogoriaFloatingWindow")
        {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(260, 200),
                MaximumSize = new Vector2(700, 1000),
            };
            Size = new Vector2(360, 460);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void PreDraw()
        {
            base.PreDraw();

            var config = plugin.Configuration;
            ImGui.SetNextWindowBgAlpha(config.FloatingWindowOpacity);

            if (config.FloatingWindowLock)
                Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
            else
                Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
        }

        public override void Draw()
        {
            // This overlay sits over the game world, so its text needs the heavier
            // outline shadow rather than the panel-friendly drop shadow.
            using var overlayText = Theme.OverlayText();

            var config = plugin.Configuration;
            var dex = plugin.Dex;
            var inventory = plugin.Inventory;
            var text = plugin.GameText;

            if (!dex.IsLoggedIn)
            {
                Theme.TextColored(UIHelpers.Dim, "Not logged in.");
                return;
            }

            var obtained = dex.ObtainedCount();
            var ready = dex.ReadyToDiscover();

            Theme.TextColored(UIHelpers.Gold, "Logos Tracker");
            // Right-aligned against the content region, not the raw window width:
            // the latter ignores padding and sits the counter under the scrollbar
            // once the list is long enough to need one.
            var counter = $"{obtained}/{LogosDatabase.Total}";
            ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(counter).X);
            ImGui.TextUnformatted(counter);

            ImGui.Separator();

            // The farm list takes over when you have one: it is what you are actually
            // working on, so the generic "everything ready" list is just noise then.
            var farmed = plugin.Farming.FarmedActions();
            if (farmed.Count > 0)
            {
                DrawFarmList(farmed, config, dex, inventory, text);
                return;
            }

            // Ready-to-discover entries are promoted to the top: that is the only
            // thing you actually need to act on.
            if (ready.Count > 0)
            {
                Theme.TextColored(UIHelpers.ReadyCyan, $"Ready to synthesise ({ready.Count})");
                ImGui.Spacing();

                foreach (var action in ready)
                    DrawRow(action, DexState.Ready, dex, inventory, text);

                ImGui.Spacing();
                ImGui.Separator();
            }
            else
            {
                Theme.TextColored(UIHelpers.Dim, "Nothing new to synthesise right now.");
                ImGui.Separator();
            }

            if (ImGui.BeginChild("TrackerList", new Vector2(0, 0)))
            {
                foreach (var action in LogosDatabase.Actions)
                {
                    var state = dex.StateOf(action);

                    if (state == DexState.Ready) continue; // already shown above
                    if (config.FloatingWindowHideObtained && state == DexState.Obtained) continue;

                    DrawRow(action, state, dex, inventory, text);
                }
            }

            // EndChild is required even when BeginChild returns false.
            ImGui.EndChild();
        }

        /// <summary>
        /// The pinnable farm list. Each entry collapses to one line; clicking opens
        /// its materials and where to farm them, so the overlay stays small.
        /// </summary>
        private void DrawFarmList(
            System.Collections.Generic.List<LogosAction> farmed,
            Configuration config,
            LogosDexService dex,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            var farming = plugin.Farming;
            var progress = farming.OverallProgress();

            Theme.TextColored(UIHelpers.Gold, $"Farming {farmed.Count}");
            ImGui.SameLine();
            Ui.ProgressBar(progress, 10f, $"{progress * 100f:F0}%", animateId: "floatfarm");

            ImGui.Separator();

            ImGui.BeginChild("FarmListFloat", new Vector2(0, 0));

            foreach (var action in farmed)
            {
                var expanded = config.TargetActionId == action.ActionId;
                var ready = farming.IsReady(action);

                ImGui.PushID((int)action.ActionId);

                UIHelpers.DrawActionIcon(action, 18f);
                ImGui.SameLine();

                var arrow = expanded ? "-" : "+";
                if (ImGui.Selectable($"{arrow} {text.ActionName(action)}", expanded))
                {
                    config.TargetActionId = expanded ? 0u : action.ActionId;
                    config.Save();
                }

                if (ready)
                {
                    ImGui.SameLine();
                    Theme.TextColored(UIHelpers.ReadyCyan, "ready");
                }

                if (expanded) DrawFarmDetail(action, inventory, text);

                ImGui.PopID();
            }

            ImGui.EndChild();
        }

        /// <summary>
        /// Materials and sources for one farmed action. Shows every combination, not
        /// just the planned one, because the cheapest recipe is often not the one
        /// whose mnemes you happen to be closest to.
        /// </summary>
        private void DrawFarmDetail(
            LogosAction action,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            ImGui.Indent(14f);

            var recipeNumber = 0;
            foreach (var recipe in action.Recipes)
            {
                recipeNumber++;
                var canMake = inventory.CanSynthesise(recipe);

                Theme.TextColored(canMake ? UIHelpers.ObtainedGreen : UIHelpers.Dim,
                    $"Combination {recipeNumber} ({UIHelpers.OddsHint(recipe)})");

                foreach (var slot in recipe.Slots)
                {
                    var have = inventory.CountOf(slot.ItemId);
                    var enough = have >= slot.Count;

                    UIHelpers.DrawMnemeIcon(slot.ItemId, ImGui.GetTextLineHeight());
                    ImGui.SameLine(0f, 5f);
                    Theme.TextMono($"{have}/{slot.Count}",
                        enough ? UIHelpers.ObtainedGreen : UIHelpers.ShortMneme);
                    ImGui.SameLine(0f, 6f);
                    Theme.TextColored(enough ? UIHelpers.ObtainedGreen : UIHelpers.ShortMneme,
                        text.MnemeName(slot.ItemId));

                    // Only explain where to farm what you are actually short of.
                    if (enough) continue;

                    var source = MnemeDatabase.SourceOf(slot.ItemId);
                    if (source == null) continue;

                    Theme.TextColored(UIHelpers.Dim, $"      from {source.FallbackName}");
                    Theme.TextColored(UIHelpers.Dim, $"      {FarmingGuide.DescribeAll(source.AcquiredBy)}");
                }

                ImGui.Spacing();
            }

            // Without this the overlay was a dead end: adding anything to the farm
            // list replaces the whole tracker with the farm view, and the only way
            // back out was to open the main window.
            // ToggleFarming already clears TargetActionId and saves.
            if (ImGui.SmallButton("Remove from farm list"))
                plugin.Configuration.ToggleFarming(action.ActionId);

            ImGui.Unindent(14f);
        }

        private void DrawRow(
            LogosAction action,
            DexState state,
            LogosDexService dex,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            ImGui.PushID((int)action.ActionId);

            UIHelpers.DrawActionIcon(action, 20f);
            ImGui.SameLine();

            Theme.TextColored(UIHelpers.ColorFor(state), text.ActionName(action));

            // Same bindings as the collection log: left adds to the farm list, right
            // toggles registered. They used to be reversed here, so the same click on
            // the same action meant different things in two windows, and a stray left
            // click on an always-on-screen overlay silently marked something obtained.
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    plugin.Configuration.ToggleFarming(action.ActionId);
                }
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    dex.SetObtained(action.ActionId, state != DexState.Obtained);
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                Theme.TextColored(UIHelpers.Gold, text.ActionName(action));
                Theme.TextColored(UIHelpers.Dim, plugin.Jobs.Describe(action));

                // In a tooltip already, so name the jobs instead of hiding them
                // behind a second hover.
                ImGui.PushTextWrapPos(320f);
                Theme.TextColored(UIHelpers.Dim, plugin.Jobs.DescribeJobs(action));
                ImGui.PopTextWrapPos();

                ImGui.Separator();

                var shown = inventory.BestAvailableRecipe(action) ?? action.CheapestRecipe;
                if (shown != null)
                {
                    UIHelpers.DrawRecipe(shown, inventory, text);
                    Theme.TextColored(UIHelpers.Dim, UIHelpers.OddsHint(shown));
                }

                ImGui.Separator();
                Theme.TextColored(UIHelpers.Dim,
                    "Left click adds to your farm list. Right click toggles registered.");
                ImGui.EndTooltip();
            }

            ImGui.PopID();
        }
    }
}

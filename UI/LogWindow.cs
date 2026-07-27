using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Logoria.Data;
using Logoria.Services;
using UiKit;

namespace Logoria.UI
{
    /// <summary>
    /// A visual collection log: all 56 actions as a grid, in the same order the
    /// in-game log uses, so it reads at a glance rather than as a table.
    /// </summary>
    public class LogWindow : LogoriaWindow, IDisposable
    {
        private const int MinColumns = 6;
        private const int MaxColumns = 14;
        private const float CellSize = 46f;

        /// <summary>Space reserved around each cell so the target ring is not clipped.</summary>
        private const float RingPadding = 4f;

        private readonly Plugin plugin;
        private bool dimUnobtained = true;

        public LogWindow(Plugin plugin)
            : base("Logos Collection Log###LogoriaLogWindow")
        {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(460, 420),
                MaximumSize = new Vector2(1200, 1200),
            };
            Size = new Vector2(520, 560);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw() => DrawContent();

        /// <summary>Body only, so the main shell can host this as a page.</summary>
        public void DrawContent()
        {
            var config = plugin.Configuration;
            var dex = plugin.Dex;
            var inventory = plugin.Inventory;
            var text = plugin.GameText;

            if (!dex.IsLoggedIn)
            {
                UIHelpers.CentredText("Log in to view your collection log.", UIHelpers.Dim);
                return;
            }

            var obtained = dex.ObtainedCount();
            var ready = dex.ReadyToDiscover();

            Theme.TextColored(UIHelpers.Gold, $"{obtained} / {LogosDatabase.Total} registered");
            ImGui.SameLine();

            // The kit's bar rather than ImGui's, so every progress bar in the plugin
            // is the same control.
            var fraction = LogosDatabase.Total > 0 ? obtained / (float)LogosDatabase.Total : 0f;
            Ui.ProgressBar(fraction, 14f, $"{fraction * 100f:F0}%", animateId: "logheader");

            if (obtained >= LogosDatabase.Total)
                Theme.TextColored(UIHelpers.ObtainedGreen,
                    "All 56 registered. Eureka armour augmentation is unlocked.");
            else
                Theme.TextColored(UIHelpers.Dim,
                    $"{LogosDatabase.Total - obtained} to go for the augmented Elemental gear.");

            if (ready.Count > 0)
                Theme.TextColored(UIHelpers.ReadyCyan,
                    $"{ready.Count} can be synthesised right now with what you are holding.");

            ImGui.Checkbox("Dim the ones you have not made", ref dimUnobtained);

            ImGui.Separator();
            Theme.TextColored(UIHelpers.Dim,
                "Left click adds to your farm list. Right click toggles registered.");
            ImGui.Spacing();

            DrawGrid(config, dex, inventory, text);
        }

        private void DrawGrid(
            Configuration config,
            LogosDexService dex,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            ImGui.BeginChild("LogGrid", new Vector2(0, 0));

            // The target ring is drawn outside the icon bounds, so the grid needs
            // room for it. Without this the ring is clipped by the neighbouring cell
            // and by the top edge of the scroll region.
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(RingPadding * 2f, RingPadding * 2f));
            ImGui.Dummy(new Vector2(0f, RingPadding));

            // Columns follow the width instead of being fixed at eight. Hosted as a
            // page in the main shell the region is far wider than the standalone
            // window, and a fixed count left most of it empty.
            var columns = Columns();
            var column = 0;

            foreach (var action in LogosDatabase.Actions)
            {
                var state = dex.StateOf(action);
                var isTarget = config.IsFarming(action.ActionId);

                if (column > 0) ImGui.SameLine();

                ImGui.PushID((int)action.ActionId);
                var origin = ImGui.GetCursorScreenPos();

                // Unobtained entries are drawn faded so the ones you have pop, the
                // same way the in-game log greys out empty slots.
                var faded = dimUnobtained && state != DexState.Obtained;
                if (faded) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.35f);
                UIHelpers.DrawActionIcon(action, CellSize - 8f);
                if (faded) ImGui.PopStyleVar();

                var draw = ImGui.GetWindowDrawList();
                var min = origin;
                var max = new Vector2(origin.X + CellSize - 8f, origin.Y + CellSize - 8f);

                if (state == DexState.Ready)
                    draw.AddRect(min, max, ImGui.GetColorU32(UIHelpers.ReadyCyan), 3f, 0, 2.5f);
                else if (state == DexState.Obtained)
                    draw.AddRect(min, max, ImGui.GetColorU32(UIHelpers.ObtainedGreen with { W = 0.55f }), 3f, 0, 1.5f);

                if (isTarget)
                {
                    // Sprite frame when the texture is ready, plain outline until
                    // then. The sprite is white so it takes the theme's colour.
                    Ui.GlowFrame(
                        plugin.Assets.GlowBorder?.Handle,
                        new Vector2(min.X - RingPadding, min.Y - RingPadding),
                        new Vector2(max.X + RingPadding, max.Y + RingPadding),
                        Theme.Gold,
                        corner: 14f);
                }

                if (ImGui.IsItemHovered())
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        config.ToggleFarming(action.ActionId);
                        if (!isTarget) plugin.FloatingWindow.IsOpen = true;
                    }
                    else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        dex.SetObtained(action.ActionId, state != DexState.Obtained);
                    }

                    DrawTooltip(action, state, inventory, text);
                }

                ImGui.PopID();

                column++;
                if (column >= columns) column = 0;
            }

            ImGui.PopStyleVar();
            ImGui.EndChild();
        }

        /// <summary>
        /// How many icons fit across, clamped so the grid neither becomes a single
        /// long strip on a very wide window nor spills off a narrow one. Measured
        /// inside the scroll child, so the scrollbar is already accounted for.
        /// </summary>
        private static int Columns()
        {
            var cell = CellSize - 8f + (RingPadding * 2f);
            var fits = (int)((ImGui.GetContentRegionAvail().X + (RingPadding * 2f)) / cell);
            return Math.Clamp(fits, MinColumns, MaxColumns);
        }

        private void DrawTooltip(
            LogosAction action,
            DexState state,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            ImGui.BeginTooltip();

            Theme.TextColored(UIHelpers.Gold, text.ActionName(action));
            Theme.TextColored(UIHelpers.ColorFor(state), UIHelpers.LabelFor(state));
            Theme.TextColored(UIHelpers.Dim, $"Log #{action.MagiaIndex}  |  {plugin.Jobs.Describe(action)}");

            // Already inside a tooltip, so the job roster is spelled out here rather
            // than hidden behind a hover that cannot happen.
            ImGui.PushTextWrapPos(360f);
            Theme.TextColored(UIHelpers.Dim, plugin.Jobs.DescribeJobs(action));
            ImGui.PopTextWrapPos();

            var description = text.ActionDescription(action);
            if (!string.IsNullOrWhiteSpace(description))
            {
                ImGui.Separator();
                ImGui.PushTextWrapPos(360f);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
            }

            var shown = inventory.BestAvailableRecipe(action) ?? action.CheapestRecipe;
            if (shown != null)
            {
                ImGui.Separator();
                UIHelpers.DrawRecipe(shown, inventory, text);
                Theme.TextColored(UIHelpers.Dim, UIHelpers.OddsHint(shown));
            }

            ImGui.EndTooltip();
        }
    }
}

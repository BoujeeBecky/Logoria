using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Logoria.Data;
using Logoria.Services;
using UiKit;

namespace Logoria.UI
{
    /// <summary>Which page the shell is showing.</summary>
    public enum ShellPage
    {
        Dex,
        CollectionLog,
        Farming,
        Settings,
        Help,
    }

    public class MainWindow : LogoriaWindow, IDisposable
    {
        private const float NavWidth = 172f;

        private readonly Plugin plugin;

        private string searchQuery = string.Empty;
        private DexState? stateFilter;
        private bool onlyCraftable;
        private ShellPage page = ShellPage.Dex;

        public MainWindow(Plugin plugin)
            : base("Logoria###LogoriaMainWindow")
        {
            this.plugin = plugin;

            // Wider than before: the nav rail takes a fixed slice off the left.
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(940, 560),
                MaximumSize = new Vector2(2200, 1600),
            };
            Size = new Vector2(1120, 700);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw()
        {
            var dex = plugin.Dex;

            if (!dex.IsLoggedIn)
            {
                ImGui.Spacing();
                UIHelpers.CentredText("Log in to view your Logos Dex.", Theme.TextMuted);
                return;
            }

            // Nav rail on the left, page content on the right, in one shell rather
            // than a scatter of separate windows. Both sit on shadowed panels so
            // they read as layered surfaces rather than flat regions.
            if (Theme.BeginPanel("NavRail", new Vector2(NavWidth, 0)))
            {
                DrawNav(dex);
            }
            Theme.EndPanel();

            ImGui.SameLine();

            // Panelled rather than a bare child: a bordered child is the only kind
            // ImGui gives padding to, so this is what stops text hugging the edge.
            if (Theme.BeginPanel("PageBody", new Vector2(0, 0), shadow: true, background: Theme.Panel))
            {
                switch (page)
                {
                    case ShellPage.CollectionLog:
                        plugin.LogWindow.DrawContent();
                        break;
                    case ShellPage.Farming:
                        plugin.FarmingWindow.DrawContent();
                        break;
                    case ShellPage.Settings:
                        plugin.SettingsWindow.DrawContent();
                        break;
                    case ShellPage.Help:
                        plugin.HelpWindow.DrawContent();
                        break;
                    default:
                        DrawDexPage();
                        break;
                }
            }

            // Must be called even when BeginPanel returns false, exactly like EndChild.
            Theme.EndPanel();
        }

        private static string Icon(FontAwesomeIcon icon) => icon.ToIconString();

        /// <summary>
        /// What to call the current character on screen. Never returns an empty
        /// string when hiding is on, because the label is doing a job: it is the
        /// only thing telling you the dex is per character.
        /// </summary>
        private static string CharacterLabel(Configuration config) =>
            config.HideCharacterName
                ? "This character"
                : LogosDexService.CurrentCharacterName;

        private void DrawNav(LogosDexService dex)
        {
            var config = plugin.Configuration;
            var obtained = dex.ObtainedCount();
            var total = LogosDatabase.Total;

            DrawBrand();
            ImGui.Spacing();
            Theme.Rule();
            ImGui.Spacing();

            // One sliding highlight for the whole rail. Only the page rows take part:
            // the two below are window toggles, and more than one of those can be on
            // at once, which would leave a single highlight fighting over where to
            // be. They keep the static treatment, which also reads correctly, since
            // "this window is open" is not the same statement as "you are here".
            Theme.BeginNavGroup("LogoriaRail");

            Theme.SectionLabel("Collection");
            if (Theme.NavItem("Dex", page == ShellPage.Dex, icon: Icon(FontAwesomeIcon.Book)))
                page = ShellPage.Dex;
            if (Theme.NavItem("Collection Log", page == ShellPage.CollectionLog,
                    icon: Icon(FontAwesomeIcon.Th)))
                page = ShellPage.CollectionLog;

            ImGui.Spacing();
            Theme.SectionLabel("Planning");
            var farmCount = config.FarmActionIds.Count;
            if (Theme.NavItem("Farming Plan", page == ShellPage.Farming,
                    farmCount > 0 ? farmCount.ToString() : null,
                    Icon(FontAwesomeIcon.Seedling)))
                page = ShellPage.Farming;

            ImGui.Spacing();
            Theme.SectionLabel("Overlays");
            if (Theme.NavItem("Floating Tracker", plugin.FloatingWindow.IsOpen,
                    icon: Icon(FontAwesomeIcon.Thumbtack), slide: false))
                plugin.FloatingWindow.IsOpen = !plugin.FloatingWindow.IsOpen;

            ImGui.Spacing();
            Theme.SectionLabel("Plugin");
            if (Theme.NavItem("Settings", page == ShellPage.Settings, icon: Icon(FontAwesomeIcon.Cog)))
                page = ShellPage.Settings;
            if (Theme.NavItem("Help", page == ShellPage.Help,
                    icon: Icon(FontAwesomeIcon.QuestionCircle)))
                page = ShellPage.Help;
#if LOGORIA_DIAG
            // Development builds only, and opt-in even there. The Release build does
            // not contain the diagnostics code at all, so there is nothing to show.
            if (config.ShowDiagnostics
                && Theme.NavItem("Diagnostics", plugin.DiagnosticsWindow.IsOpen,
                    icon: Icon(FontAwesomeIcon.Stethoscope), slide: false))
                plugin.DiagnosticsWindow.IsOpen = !plugin.DiagnosticsWindow.IsOpen;
#endif

            Theme.EndNavGroup();

            // Status footer, pinned to the bottom like Umbra's account strip.
            // Measured rather than guessed: the old fixed 58px predated the character
            // name and the pill, so the registered count was clipped off the bottom.
            var line = ImGui.GetTextLineHeightWithSpacing();
            var footerHeight = (line * 3f)          // rule, character name, count
                               + 8f                 // progress bar
                               + line               // manipulator pill
                               + (ImGui.GetStyle().ItemSpacing.Y * 4f);

            var remaining = ImGui.GetContentRegionAvail().Y - footerHeight;
            if (remaining > 0) ImGui.Dummy(new Vector2(0, remaining));

            Theme.Rule(0.7f);
            ImGui.Spacing();

            // Naming the character makes it obvious that the dex is per-character,
            // rather than looking like the data vanished. Hidden for screenshots and
            // streaming, but replaced rather than blanked, so the per-character
            // meaning survives.
            var characterName = CharacterLabel(config);
            if (!string.IsNullOrEmpty(characterName))
                Theme.TextColored(Theme.TextMuted, characterName);

            Theme.TextMono($"{obtained} / {total} registered", Theme.TextFaint);
            Ui.ProgressBar(total > 0 ? obtained / (float)total : 0f, 8f, animateId: "navdex");

            var manipulatorOpen = plugin.Manipulator.IsManipulatorOpen();
            Theme.Pill(manipulatorOpen ? "Manipulator open" : "Manipulator closed",
                manipulatorOpen ? Theme.Success : Theme.TextFaint);
        }

        /// <summary>
        /// Brand block for the rail. Uses the crystal mark when the texture is
        /// ready and falls back to the wordmark in text, since textures load
        /// asynchronously and the first frames will have neither.
        /// </summary>
        private void DrawBrand()
        {
            var mark = plugin.Assets.LogoMark;

            // The subtitle gets its own full-width line rather than sitting beside
            // the mark. Stacked next to a 30px icon it ran past the rail and
            // rendered as "Eureka Logos De".
            if (mark != null)
            {
                const float markSize = 24f;
                ImGui.Image(mark.Handle, new Vector2(markSize, markSize));
                ImGui.SameLine(0f, 8f);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
                Theme.TextColored(Theme.Gold, "LOGORIA");
            }
            else
            {
                Theme.TextColored(Theme.Gold, "LOGORIA");
            }

            Theme.TextColored(Theme.TextFaint, "Eureka Logos Dex");
        }

        private void DrawDexPage()
        {
            var config = plugin.Configuration;
            var dex = plugin.Dex;
            var inventory = plugin.Inventory;
            var text = plugin.GameText;

            DrawHeader(dex);
            ImGui.Spacing();
            DrawFilters();
            ImGui.Spacing();
            DrawTable(dex, inventory, text, config);
        }

        /// <summary>
        /// Banner strip with the page title over it. The art is 4:1, so it is
        /// cropped rather than squashed, and faded into the panel at the bottom so
        /// it reads as part of the page instead of a pasted-in picture.
        /// </summary>
        private void DrawBanner()
        {
            var banner = plugin.Assets.Banner;
            if (banner == null) return;

            const float height = 74f;
            var width = ImGui.GetContentRegionAvail().X;
            if (width <= 0f) return;

            var min = ImGui.GetCursorScreenPos();
            var max = new Vector2(min.X + width, min.Y + height);
            var draw = ImGui.GetWindowDrawList();

            // Centre-crop vertically to the strip's aspect rather than stretching.
            var aspect = width / height;
            var sourceAspect = banner.Width / (float)banner.Height;
            var vHalf = sourceAspect / aspect * 0.5f;
            var uv0 = new Vector2(0f, Math.Max(0f, 0.5f - vHalf));
            var uv1 = new Vector2(1f, Math.Min(1f, 0.5f + vHalf));

            draw.PushClipRect(min, max, true);
            draw.AddImageRounded(banner.Handle, min, max, uv0, uv1,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)),
                Theme.Rounding, ImDrawFlags.RoundCornersTop);

            // Fade the lower half into the panel so the seam disappears.
            var fadeTop = new Vector2(min.X, min.Y + (height * 0.35f));
            Ui.GradientRect(fadeTop, max,
                Theme.Fade(Theme.Panel, 0f), Theme.Panel, 0f, 14);

            draw.PopClipRect();

            ImGui.Dummy(new Vector2(width, height));

            // Title sits over the art rather than under it.
            var titlePos = new Vector2(min.X + 14f, max.Y - 30f);
            ImGui.SetCursorScreenPos(titlePos);
        }

        private void DrawHeader(LogosDexService dex)
        {
            var ready = dex.ReadyToDiscover();
            var farmCount = plugin.Configuration.FarmActionIds.Count;

            DrawBanner();

            Theme.TextColored(Theme.TextPrimary, "Logos Action Dex");
            ImGui.SameLine();
            Theme.TextColored(Theme.TextFaint, $"|  {LogosDatabase.Total} actions");

            ImGui.Spacing();

            // Stat pills read faster than a sentence, and match the nav rail's tone.
            if (ready.Count > 0)
            {
                // Gentle pulse: this is the one thing on the page worth acting on,
                // so it earns a little motion. Bounded well away from 0 so it reads
                // as breathing rather than blinking.
                var pulse = 0.80f + (UiAnim.Pulse(2.4f) * 0.20f);
                Theme.Pill($"{ready.Count} ready to synthesise", Theme.Fade(Theme.Accent, pulse));
                ImGui.SameLine();
            }

            if (farmCount > 0)
            {
                Theme.Pill($"farming {farmCount}  ({plugin.Farming.OverallProgress() * 100f:F0}%)", Theme.Gold);
                ImGui.SameLine();
            }

            if (!plugin.Inventory.HasLiveStock)
                Theme.Pill("no live mneme stock", Theme.TextFaint);
            else
                Theme.Pill("mneme stock live", Theme.Success);

            if (dex.ObtainedCount() == 0)
            {
                ImGui.Spacing();

                if (dex.OtherCharactersHaveData())
                {
                    Theme.TextColored(Theme.Gold,
                        $"This dex is for {CharacterLabel(plugin.Configuration)}. Another character "
                        + "has entries recorded, so nothing has been lost.");
                }
                else
                {
                    // First run. Without this the dex is 56 grey rows and no hint that
                    // the game will fill them in for you.
                    Theme.TextColored(Theme.Gold,
                        "New here? Open Drake's Logos Action Log, beside the Logos Manipulator, "
                        + "and your whole dex fills in at once.");

                    if (Theme.ButtonGhost("Read the Help page"))
                        page = ShellPage.Help;
                }
            }

            ImGui.Spacing();
            Theme.Rule();
        }

        /// <summary>
        /// Two rows rather than one. Search, four radios and a checkbox on a single
        /// line overflowed the window at anything but a very wide size, and the
        /// rightmost filter was clipped to "Unknow". Splitting them fits at the
        /// minimum window width with room to spare.
        /// </summary>
        private void DrawFilters()
        {
            // Search takes whatever is left after the Clear button, so it grows with
            // the window instead of being pinned to a width that may not fit.
            var clearWidth = ImGui.CalcTextSize("Clear").X + (ImGui.GetStyle().FramePadding.X * 2f);
            var searchWidth = Math.Max(160f, ImGui.GetContentRegionAvail().X - clearWidth - 12f);

            ImGui.SetNextItemWidth(searchWidth);
            ImGui.InputTextWithHint("##Search", "Search name or description...", ref searchQuery, 100);

            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                searchQuery = string.Empty;
                stateFilter = null;
                onlyCraftable = false;
            }

            if (ImGui.RadioButton("All", stateFilter == null)) stateFilter = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("Ready", stateFilter == DexState.Ready)) stateFilter = DexState.Ready;
            ImGui.SameLine();
            if (ImGui.RadioButton("Obtained", stateFilter == DexState.Obtained)) stateFilter = DexState.Obtained;
            ImGui.SameLine();
            if (ImGui.RadioButton("Unknown", stateFilter == DexState.Unknown)) stateFilter = DexState.Unknown;

            // Separated by a gap rather than a pipe glyph. At this text size the bar
            // rendered as a stray half-height tick beside "Unknown" and read as a
            // drawing artefact instead of a divider.
            ImGui.SameLine(0f, 18f);
            ImGui.Checkbox("Only what I can make now", ref onlyCraftable);
        }

        private void DrawTable(
            LogosDexService dex,
            MnemeInventoryService inventory,
            GameTextService text,
            Configuration config)
        {
            // Horizontal separators only. Full borders made every row look like a
            // boxed cell and fought with the rounded panels elsewhere.
            //
            // Column widths now persist. They previously did not, because ImGui's
            // saved widths outrank TableSetupColumn and stale ones from an older
            // layout were squeezing the Effect column to a few characters. The id
            // carries a layout version and the user's reset counter, so a layout
            // change or a reset lands on fresh defaults instead of inheriting them.
            // Bump the "v2" whenever these columns change.
            const ImGuiTableFlags flags = ImGuiTableFlags.RowBg
                                          | ImGuiTableFlags.BordersInnerH
                                          | ImGuiTableFlags.Resizable
                                          | ImGuiTableFlags.ScrollY
                                          | ImGuiTableFlags.SizingStretchProp
                                          | ImGuiTableFlags.PadOuterX;

            if (!ImGui.BeginTable($"LogosDexTable_v2##{config.TableLayoutEpoch}", 5, flags)) return;

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 92);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 2.1f);
            ImGui.TableSetupColumn("Effect", ImGuiTableColumnFlags.WidthStretch, 2.6f);
            ImGui.TableSetupColumn("Recipe", ImGuiTableColumnFlags.WidthStretch, 2.0f);

            // Derived from the buttons it has to hold rather than a fixed 138, which
            // fitted only at the default font size and clipped once the font was
            // scaled up in settings.
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed,
                (ToggleButtonWidth() * 2f) + 18f);
            ImGui.TableHeadersRow();

            var shown = 0;

            foreach (var action in LogosDatabase.Actions)
            {
                var state = dex.StateOf(action);
                var best = inventory.BestAvailableRecipe(action);

                if (stateFilter.HasValue && state != stateFilter.Value) continue;
                if (onlyCraftable && best == null) continue;
                if (!MatchesSearch(action, text)) continue;

                shown++;
                ImGui.TableNextRow();

                // A ready-to-discover row gets a tinted background. This is the
                // whole point of the dex, so it should be impossible to miss.
                if (state == DexState.Ready && config.HighlightReadyToDiscover)
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                        ImGui.GetColorU32(new Vector4(0.10f, 0.32f, 0.38f, 0.55f)));
                }

                ImGui.TableSetColumnIndex(0);
                Theme.StatusDot(UIHelpers.LabelFor(state), UIHelpers.ColorFor(state),
                    state != DexState.Unknown);
                if (ImGui.IsItemClicked())
                    dex.SetObtained(action.ActionId, state != DexState.Obtained);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(state == DexState.Obtained
                        ? "Registered. Click to clear."
                        : "Not registered. Click to mark as obtained.");

                ImGui.TableSetColumnIndex(1);
                UIHelpers.DrawActionIcon(action, 30f);
                ImGui.SameLine();
                // Both lines are clipped to the column rather than allowed to run
                // under the next one. The role summary is the longer of the two and
                // was the line that overflowed; its tooltip names the actual jobs.
                var nameWidth = ImGui.GetContentRegionAvail().X;
                ImGui.BeginGroup();
                Theme.TextEllipsis(text.ActionName(action), nameWidth, Theme.TextPrimary);
                Theme.TextEllipsis(plugin.Jobs.Describe(action), nameWidth, Theme.TextFaint,
                    plugin.Jobs.DescribeJobs(action));
                ImGui.EndGroup();

                // One line with the full text on hover. Wrapping made every row five
                // lines tall and only four actions fit on screen.
                ImGui.TableSetColumnIndex(2);
                var description = text.ActionDescription(action);
                if (string.IsNullOrWhiteSpace(description)) description = string.Join(", ", action.Tags);
                Theme.TextEllipsis(description, ImGui.GetContentRegionAvail().X, Theme.TextMuted);

                ImGui.TableSetColumnIndex(3);
                DrawRecipeCell(action, best, inventory, text);

                ImGui.TableSetColumnIndex(4);
                DrawActionCell(action, best, text);
            }

            ImGui.EndTable();

            // An empty table under a filter row reads as a bug. Say what happened and
            // give the way out, since the cause is usually a filter left set rather
            // than the search box the eye goes to first.
            if (shown == 0)
            {
                ImGui.Spacing();
                UIHelpers.CentredText("No actions match your filters.", Theme.TextMuted);
                ImGui.Spacing();

                var width = ImGui.CalcTextSize("Reset filters").X
                            + (ImGui.GetStyle().FramePadding.X * 2f) + 8f;
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - width) * 0.5f);

                if (Theme.ButtonGhost("Reset filters", new Vector2(width, 0)))
                {
                    searchQuery = string.Empty;
                    stateFilter = null;
                    onlyCraftable = false;
                }
            }
        }

        private void DrawRecipeCell(
            LogosAction action,
            LogosRecipe? best,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            // Show the recipe you can actually make; otherwise show the cheapest so
            // you know what to go farm.
            var shown = best ?? action.CheapestRecipe;
            if (shown == null) return;

            // Compact single line per slot: icon, "2x Cure L", then "1/2" in mono.
            foreach (var slot in shown.Slots)
            {
                var have = inventory.CountOf(slot.ItemId);
                var enough = have >= slot.Count;

                UIHelpers.DrawMnemeIcon(slot.ItemId, ImGui.GetTextLineHeight());
                ImGui.SameLine(0f, 5f);

                Theme.TextColored(enough ? Theme.Success : Theme.Danger,
                    $"{slot.Count}x {text.MnemeName(slot.ItemId)}");
                ImGui.SameLine(0f, 6f);
                Theme.TextMono($"{have}/{slot.Count}", Theme.TextFaint);
            }

            Theme.TextColored(Theme.TextFaint, UIHelpers.OddsHint(shown));

            if (action.Recipes.Count > 1 && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                Theme.TextColored(UIHelpers.Gold, $"All {action.Recipes.Count} combinations:");
                ImGui.Separator();
                foreach (var recipe in action.Recipes)
                {
                    UIHelpers.DrawRecipe(recipe, inventory, text, compact: true);
                    Theme.TextColored(UIHelpers.Dim, $"  ({UIHelpers.OddsHint(recipe)})");
                    ImGui.Spacing();
                }
                ImGui.EndTooltip();
            }
        }

        /// <summary>
        /// Width of the Farm/Farming and Fill buttons: the widest label any of them
        /// can show, plus padding. Recomputed each frame because the font size is a
        /// setting, so a cached pixel width would go stale on a font change.
        /// </summary>
        /// <summary>Static: this runs once per row, so allocating it per call meant
        /// 57 throwaway arrays every frame the dex was open.</summary>
        private static readonly string[] ToggleButtonLabels = { "Farm", "Farming", "Fill" };

        private static float ToggleButtonWidth()
        {
            var widest = 0f;
            foreach (var label in ToggleButtonLabels)
                widest = Math.Max(widest, ImGui.CalcTextSize(label).X);

            return widest + (ImGui.GetStyle().FramePadding.X * 2f) + 4f;
        }

        private void DrawActionCell(LogosAction action, LogosRecipe? best, GameTextService text)
        {
            var config = plugin.Configuration;
            var farming = config.IsFarming(action.ActionId);

            // Wide enough for the longest label either button can ever show, measured
            // rather than guessed. A fixed 58px clipped the g off "Farming", and
            // sizing to the current label instead would make the button resize under
            // the cursor the moment it is clicked.
            var buttonSize = new Vector2(ToggleButtonWidth(), 0);

            var farmPressed = farming
                ? Theme.ButtonAccent($"Farming##t{action.ActionId}", buttonSize)
                : Theme.ButtonGhost($"Farm##t{action.ActionId}", buttonSize);

            if (farmPressed)
            {
                config.ToggleFarming(action.ActionId);
                if (!farming) plugin.FloatingWindow.IsOpen = true;
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(farming
                    ? "Remove from your farm list."
                    : "Add to your farm list: its mnemes get totalled into the farming plan, "
                      + "and the tracker shows what you are short of and where to get it.");

            var canFill = best != null && plugin.Manipulator.IsManipulatorOpen();

            // Side by side rather than stacked: two full-width buttons per row made
            // the column look like a floating pair of slabs.
            ImGui.SameLine(0f, 6f);

            if (!canFill) ImGui.BeginDisabled();
            if (Theme.ButtonAccent($"Fill##{action.ActionId}", buttonSize) && best != null)
                plugin.Manipulator.RequestAutoFill(action, best, plugin.Inventory, text);
            if (!canFill) ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                if (best == null)
                    ImGui.SetTooltip("You are missing mnemes for every combination.");
                else if (!plugin.Manipulator.IsManipulatorOpen())
                    ImGui.SetTooltip("Open the Logos Manipulator first.");
                else
                    ImGui.SetTooltip(
                        "Loads this combination into the Astral Array.\n"
                        + "Nothing is consumed: press Extract Mneme yourself when ready.");
            }
        }

        private bool MatchesSearch(LogosAction action, GameTextService text)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;

            if (text.ActionName(action).Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) return true;
            if (text.ActionDescription(action).Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) return true;

            foreach (var tag in action.Tags)
                if (tag.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}

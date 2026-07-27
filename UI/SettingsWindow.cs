using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Logoria.Services;

namespace Logoria.UI
{
    public class SettingsWindow : LogoriaWindow, IDisposable
    {
        private readonly Plugin plugin;
        private int lastSeeded = -1;
        private string lastSyncMessage = string.Empty;

        public SettingsWindow(Plugin plugin)
            : base("Logoria Settings###LogoriaSettingsWindow", ImGuiWindowFlags.AlwaysAutoResize)
        {
            this.plugin = plugin;
            Size = new Vector2(460, 380);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw() => DrawContent();

        /// <summary>Body only, so the main shell can host this as a page.</summary>
        public void DrawContent()
        {
            var config = plugin.Configuration;
            var changed = false;

            Theme.TextColored(UIHelpers.Gold, "Dex Tracking");
            ImGui.Separator();

            var autoDetect = config.EnableAutoDetect;
            if (ImGui.Checkbox("Automatically record actions you have equipped", ref autoDetect))
            {
                config.EnableAutoDetect = autoDetect;
                changed = true;
            }
            Hint("Logos Actions you slot are read from the game and recorded permanently. "
                 + "The game itself never stores which actions you have made, so this is how the dex fills in.");

            var autoSync = config.AutoSyncFromLog;
            if (ImGui.Checkbox("Sync from Drake's Logos Action Log automatically", ref autoSync))
            {
                config.AutoSyncFromLog = autoSync;
                changed = true;
            }
            Hint("Drake, beside the Logos Manipulator, keeps the authoritative record of every "
                 + "action you have registered. Opening his log syncs your whole dex at once.");

            if (ImGui.Button("Sync from the log now"))
            {
                var (read, added, registered) = plugin.LogReader.TrySyncBest(config);
                lastSyncMessage = read
                    ? $"Synced: {registered}/56 registered, {added} newly recorded."
                    : "Could not read the log. Talk to Drake so his log window is open, then try again.";
            }

            if (!string.IsNullOrEmpty(lastSyncMessage))
                Theme.TextColored(UIHelpers.ObtainedGreen, lastSyncMessage);

            ImGui.Spacing();

            var announce = config.AnnounceDiscoveriesInChat;
            if (ImGui.Checkbox("Announce new dex entries in chat", ref announce))
            {
                config.AnnounceDiscoveriesInChat = announce;
                changed = true;
            }

            var interval = config.AutoDetectIntervalSeconds;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Scan interval (seconds)", ref interval, 0.5f, 10f, "%.1f"))
            {
                config.AutoDetectIntervalSeconds = interval;
                changed = true;
            }

            ImGui.Spacing();
            if (ImGui.Button("Seed dex from what I can make now"))
                lastSeeded = plugin.Dex.MarkAllReadyAsObtained();

            Hint("One-off helper for an existing character: marks every action you currently "
                 + "hold the mnemes for as already obtained. Only do this if that is actually true.");

            if (lastSeeded >= 0)
                Theme.TextColored(UIHelpers.ObtainedGreen, $"Seeded {lastSeeded} entries.");

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Appearance");
            ImGui.Separator();

            var vanilla = config.VanillaMode;
            if (ImGui.Checkbox("Vanilla mode (plain ImGui)", ref vanilla))
            {
                config.VanillaMode = vanilla;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("Strips the theme, gradients, shadows, grain and animation, leaving stock "
                 + "ImGui windows. Removes hundreds of draw calls a frame, so it is worth "
                 + "having if the UI costs you frames. Everything below stops applying.");

            // Skip only the appearance options, not the rest of the window. An early
            // return here would also have hidden the Main Window, Floating Tracker
            // and Manipulator sections further down, which have nothing to do with
            // how things are drawn.
            if (config.VanillaMode)
            {
                ImGui.Spacing();
                Theme.TextColored(Theme.TextMuted,
                    "Appearance options are hidden while vanilla mode is on.");
            }
            else
            {

            ImGui.Spacing();

            DrawThemePicker(config, ref changed);

            if (plugin.Fonts.IsCustom)
            {
                Theme.TextColored(Theme.Success, $"Font: {plugin.Fonts.LoadedName}");

                var fontSize = config.FontSizePx;
                ImGui.SetNextItemWidth(180f);
                if (ImGui.SliderFloat("Font size", ref fontSize, 10f, 24f, "%.0f px"))
                {
                    config.FontSizePx = fontSize;
                    changed = true;
                }
                Hint("Takes effect after a plugin reload: the font atlas is built once at "
                     + "startup, not per frame.");
            }
            else
            {
                Theme.TextColored(Theme.TextFaint, "Font: Dalamud default");
                Hint("Drop a .ttf or .otf into the plugin's assets/fonts folder to use your "
                     + "own. Inter, Figtree and Rubik all suit this UI.");
            }

            ImGui.Spacing();

            var animate = config.EnableAnimation;
            if (ImGui.Checkbox("Animate transitions", ref animate))
            {
                config.EnableAnimation = animate;
                changed = true;
            }
            Hint("Hover fades, eased progress bars, and a slow pulse on anything ready to "
                 + "synthesise. Off makes everything snap instantly.");

            if (config.EnableAnimation)
            {
                var speed = config.AnimationSpeed;
                ImGui.SetNextItemWidth(180f);
                if (ImGui.SliderFloat("Animation speed", ref speed, 0.25f, 3f, "%.2fx"))
                {
                    config.AnimationSpeed = speed;
                    changed = true;
                }
            }

            var grain = config.NoiseStrength;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Film grain", ref grain, 0f, 1.5f, "%.2f"))
            {
                config.NoiseStrength = grain;
                changed = true;
            }
            Hint("Tiled grain over panels. Breaks up banding in the gradients and gives flat "
                 + "fills a hint of material. Should be felt rather than seen.");

            var glass = config.GlassMode;
            if (ImGui.Checkbox("Frosted glass surfaces", ref glass))
            {
                config.GlassMode = glass;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("Translucent panels with a bright edge and a larger radius, so the game "
                 + "shows through. Note this cannot blur what is behind it the way CSS "
                 + "backdrop-filter does, so a busy background stays sharp. Text switches to "
                 + "outlined shadows automatically to stay readable.");

            if (config.GlassMode)
            {
                var glassOpacity = config.GlassOpacity;
                ImGui.SetNextItemWidth(180f);
                if (ImGui.SliderFloat("Glass opacity", ref glassOpacity, 0.05f, 0.85f, "%.2f"))
                {
                    config.GlassOpacity = glassOpacity;
                    ReinstallTheme(config);
                    changed = true;
                }
                Hint("Lower is clearer. Below about 0.20 the game behind starts to compete "
                     + "with the text.");
            }

            var shadow = config.TextShadowStyle;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.Combo("Text shadow", ref shadow, "None\0Drop\0Outline\0"))
            {
                config.TextShadowStyle = shadow;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("Outline is heaviest and keeps text readable over the game world; drop is "
                 + "subtler and suits opaque panels. The floating tracker always uses outline "
                 + "regardless, since it sits over the world.");

            var gradient = config.SurfaceGradient;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Surface depth", ref gradient, 0f, 0.10f, "%.3f"))
            {
                config.SurfaceGradient = gradient;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("Vertical lighting on panels: lighter at the top, darker at the bottom. "
                 + "0 is flat. Past about 0.08 it stops reading as lighting and starts looking striped.");

            var bevel = config.BevelStrength;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Edge bevel", ref bevel, 0f, 1f, "%.2f"))
            {
                config.BevelStrength = bevel;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("A light hairline along the top edge and a dark one along the bottom. "
                 + "This does more for the raised look than the gradient does.");

            var gloss = config.GlossStrength;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Shine", ref gloss, 0f, 1.5f, "%.2f"))
            {
                config.GlossStrength = gloss;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("Glassy highlight across the top of buttons, pills and the selected nav row, "
                 + "like light catching a curved surface.");

            var domed = config.DomedTokens;
            if (ImGui.Checkbox("Domed pills and progress fills", ref domed))
            {
                config.DomedTokens = domed;
                ReinstallTheme(config);
                changed = true;
            }
            Hint("On, small rounded elements are shaded darker at the edge and lighter toward "
                 + "the middle, which reads as a rounded object. Off, they are shaded lighter "
                 + "at the top like a flat panel. Panels themselves are unaffected either way. "
                 + "Compare on the pills at the top of the dex and the bar in the nav rail.");

            } // end of the appearance options

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Main Window");
            ImGui.Separator();

            var highlight = config.HighlightReadyToDiscover;
            if (ImGui.Checkbox("Highlight rows you can make but have never had", ref highlight))
            {
                config.HighlightReadyToDiscover = highlight;
                changed = true;
            }

            var hideName = config.HideCharacterName;
            if (ImGui.Checkbox("Hide my character name", ref hideName))
            {
                config.HideCharacterName = hideName;
                changed = true;
            }
            Hint("For screenshots and streaming. Shows \"This character\" instead of your name. "
                 + "Your dex is still stored per character either way; only the label changes.");

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Floating Tracker");
            ImGui.Separator();

            if (ImGui.Button("Open floating tracker"))
                plugin.FloatingWindow.IsOpen = true;
            ImGui.SameLine();
            if (ImGui.Button("Open collection log"))
                plugin.LogWindow.IsOpen = true;
            Hint("Also available as /logofloat and /logolog, or from the buttons at the top of "
                 + "the main dex window.");

            var opacity = config.FloatingWindowOpacity;
            ImGui.SetNextItemWidth(180f);
            if (ImGui.SliderFloat("Opacity", ref opacity, 0.2f, 1.0f, "%.2f"))
            {
                config.FloatingWindowOpacity = opacity;
                changed = true;
            }

            var locked = config.FloatingWindowLock;
            if (ImGui.Checkbox("Lock position and size", ref locked))
            {
                config.FloatingWindowLock = locked;
                changed = true;
            }

            var hideObtained = config.FloatingWindowHideObtained;
            if (ImGui.Checkbox("Hide entries already obtained", ref hideObtained))
            {
                config.FloatingWindowHideObtained = hideObtained;
                changed = true;
            }

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Layout");
            ImGui.Separator();

            if (ImGui.Button("Reset table column widths"))
            {
                config.TableLayoutEpoch++;
                changed = true;
            }
            Hint("Column widths in the dex and the farming plan are remembered once you drag "
                 + "them. This starts them over, which is the way back if a column ends up "
                 + "dragged down to nothing.");

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Logos Manipulator");
            ImGui.Separator();

            var autoOpen = config.AutoOpenOnManipulator;
            if (ImGui.Checkbox("Open Logoria when the manipulator opens", ref autoOpen))
            {
                config.AutoOpenOnManipulator = autoOpen;
                changed = true;
            }

            ImGui.Spacing();
            Theme.TextColored(UIHelpers.ObtainedGreen, "Auto-fill is enabled.");
            Hint("The Fill button in the dex clears the Astral Array and loads a recipe into it. "
                 + "It deliberately stops there: nothing is consumed until you press Extract Mneme "
                 + "yourself.");

#if LOGORIA_DIAG
            ImGui.Spacing();
            Theme.TextColored(UIHelpers.Gold, "Troubleshooting (development build)");
            ImGui.Separator();

            var showDiagnostics = config.ShowDiagnostics;
            if (ImGui.Checkbox("Show the diagnostics window", ref showDiagnostics))
            {
                config.ShowDiagnostics = showDiagnostics;
                if (!showDiagnostics) plugin.DiagnosticsWindow.IsOpen = false;
                changed = true;
            }
            Hint("Development builds only. Diagnostics reports the manipulator's addon name, "
                 + "its number array indices and the UI callbacks it receives. The released "
                 + "build does not contain this code at all.");

            if (config.ShowDiagnostics)
            {
                if (ImGui.Button("Open diagnostics (/logodiag)"))
                    plugin.DiagnosticsWindow.IsOpen = true;
            }
#endif

            if (changed) config.Save();
        }

        /// <summary>Reapplies the whole look from config. Cheap, and keeps every
        /// appearance control in sync rather than each one patching one field.</summary>
        private static void ReinstallTheme(Configuration config) =>
            Theme.Install(config.ThemeName, config.TextShadowStyle,
                config.SurfaceGradient, config.BevelStrength, config.GlossStrength,
                config.GlassMode, config.GlassOpacity, config.VanillaMode,
                config.DomedTokens);

        /// <summary>
        /// Theme picker with live swatches. A name alone tells you nothing about what
        /// a palette looks like, so each row previews its own colours.
        /// </summary>
        private void DrawThemePicker(Configuration config, ref bool changed)
        {
            Theme.TextColored(Theme.TextMuted, "Theme");

            foreach (var preset in UiKit.UiThemes.All)
            {
                var selected = string.Equals(preset.Name, config.ThemeName,
                    StringComparison.OrdinalIgnoreCase);

                ImGui.PushID(preset.Name);

                var draw = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var rowHeight = ImGui.GetTextLineHeight() + 10f;
                var swatchSize = rowHeight - 12f;

                if (ImGui.Selectable("##themerow", selected, ImGuiSelectableFlags.None,
                        new Vector2(0, rowHeight)))
                {
                    config.ThemeName = preset.Name;
                    ReinstallTheme(config);
                    changed = true;
                }

                // Swatch strip: accent, highlight, then the three surface tones.
                var x = origin.X + 8f;
                var y = origin.Y + 6f;

                foreach (var colour in new[]
                         {
                             preset.Accent, preset.Highlight, preset.PanelRaised,
                             preset.Panel, preset.Border,
                         })
                {
                    draw.AddRectFilled(
                        new Vector2(x, y),
                        new Vector2(x + swatchSize, y + swatchSize),
                        ImGui.GetColorU32(colour), 3f);
                    x += swatchSize + 3f;
                }

                draw.AddText(new Vector2(x + 8f, y),
                    ImGui.GetColorU32(selected ? Theme.Accent : Theme.TextMuted), preset.Name);

                ImGui.PopID();
            }

            ImGui.Spacing();
        }

        private static void Hint(string message)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, UIHelpers.Dim);
            ImGui.TextWrapped(message);
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }
    }
}

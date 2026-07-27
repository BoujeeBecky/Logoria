using System.Numerics;
using UiKit;

namespace Logoria.UI
{
    /// <summary>
    /// Logoria's brand layer over the shared UI kit.
    /// <para>
    /// The look itself lives in the sibling Dalamud_UI_Framework checkout and is
    /// linked in as source. This file only defines Logoria's palette and re-exposes
    /// the kit under familiar names, so call sites read naturally and the kit stays
    /// brand-neutral.
    /// </para>
    /// </summary>
    public static class Theme
    {
        /// <summary>
        /// Applies Logoria's palette to the kit. Call once at plugin start, before
        /// any window draws.
        /// </summary>
        public static void Install(
            string themeName = "Aetherial",
            int textShadowStyle = 1,
            float surfaceGradient = 0.045f,
            float bevelStrength = 0.55f,
            float glossStrength = 0.8f,
            bool glass = false,
            float glassOpacity = 0.30f,
            bool vanilla = false,
            bool domedTokens = true)
        {
            // Semantic colours are set on the base palette so they survive every
            // theme switch: success and danger must not change meaning with taste.
            var basePalette = new UiPalette
            {
                // Glass lets the game through, so a drop shadow is no longer enough
                // to keep text legible. Force the outline unless shadows are off
                // entirely, which is an explicit choice we should not override.
                TextShadow = (textShadowStyle, glass) switch
                {
                    (0, _) => TextShadowStyle.None,
                    (_, true) => TextShadowStyle.Outline,
                    (2, _) => TextShadowStyle.Outline,
                    _ => TextShadowStyle.Drop,
                },
                Success = UiPalette.Rgb(0x63, 0xD1, 0x8A),
                Danger = UiPalette.Rgb(0xE0, 0x6C, 0x6C),
                SurfaceGradient = surfaceGradient,
                BevelStrength = bevelStrength,
                GlossStrength = glossStrength,
                Glass = glass,
                GlassOpacity = glassOpacity,
                Vanilla = vanilla,
                DomedTokens = domedTokens,
            };

            UiPalette.Current = UiThemes.Build(themeName, basePalette);
        }

        /// <summary>Switches theme live, keeping shadow and semantic settings.</summary>
        public static void ApplyTheme(string themeName) => UiThemes.Apply(themeName);

        public static string[] ThemeNames => UiThemes.Names;

        /// <summary>
        /// Swaps to the heavier outline shadow while drawing the floating overlay,
        /// which sits over the game world rather than an opaque panel. Dispose to
        /// restore the normal drop shadow.
        /// </summary>
        public static OverlayTextScope OverlayText() => new();

        public sealed class OverlayTextScope : System.IDisposable
        {
            private readonly UiPalette previous;

            public OverlayTextScope()
            {
                previous = UiPalette.Current;
                UiPalette.Current = previous with
                {
                    TextShadow = TextShadowStyle.Outline,
                    TextShadowColour = new Vector4(0f, 0f, 0f, 0.9f),
                };
            }

            public void Dispose() => UiPalette.Current = previous;
        }

        // ---- palette passthrough -------------------------------------------
        public static Vector4 Backdrop => UiPalette.Current.Backdrop;
        public static Vector4 Panel => UiPalette.Current.Panel;
        public static Vector4 PanelRaised => UiPalette.Current.PanelRaised;
        public static Vector4 PanelHover => UiPalette.Current.PanelHover;
        public static Vector4 Border => UiPalette.Current.Border;
        public static Vector4 TextPrimary => UiPalette.Current.TextPrimary;
        public static Vector4 TextMuted => UiPalette.Current.TextMuted;
        public static Vector4 TextFaint => UiPalette.Current.TextFaint;
        public static Vector4 Accent => UiPalette.Current.Accent;
        public static Vector4 AccentDim => UiPalette.Current.AccentDim;
        public static Vector4 Gold => UiPalette.Current.Highlight;
        public static Vector4 Success => UiPalette.Current.Success;
        public static Vector4 Danger => UiPalette.Current.Danger;

        public static float Rounding => UiPalette.Current.Rounding;

        public static Vector4 Fade(Vector4 colour, float alpha) => UiPalette.Fade(colour, alpha);

        public static uint U32(Vector4 colour) => UiTheme.U32(colour);

        // ---- component passthrough -----------------------------------------
        public static UiTheme.StyleScope Push() => UiTheme.Push();

        public static void SectionLabel(string text) => Ui.SectionLabel(text);

        public static bool NavItem(string label, bool selected, string? badge = null,
            string? icon = null, bool slide = true) =>
            Ui.NavItem(label, selected, badge, icon, slide);

        public static void BeginNavGroup(string id) => Ui.BeginNavGroup(id);

        public static void EndNavGroup() => Ui.EndNavGroup();

        public static void Pill(string text, Vector4 colour) => Ui.Pill(text, colour);

        public static void StatusDot(string label, Vector4 colour, bool filled) =>
            Ui.StatusDot(label, colour, filled);

        public static void ProgressBar(float fraction, float height, string? overlay = null) =>
            Ui.ProgressBar(fraction, height, overlay);

        public static void Rule(float alpha = 1f) => Ui.Rule(alpha);

        /// <summary>Graded edge-to-centre: reads as a convex token, not a flat panel.</summary>
        public static void DomeRect(Vector2 min, Vector2 max, Vector4 edge, Vector4 centre,
            float rounding, int steps = 10) =>
            Ui.DomeRect(min, max, edge, centre, rounding, steps);

        /// <summary>A recessed surface, for tracks and slots. Inverse of a panel.</summary>
        public static void Well(Vector2 min, Vector2 max, Vector4 baseColour, float rounding,
            bool border = true) =>
            Ui.Well(min, max, baseColour, rounding, border);

        public static bool ButtonAccent(string label, Vector2 size = default) =>
            Ui.ButtonAccent(label, size);

        public static bool ButtonGhost(string label, Vector2 size = default) =>
            Ui.ButtonGhost(label, size);

        public static void TextEllipsis(string text, float maxWidth, Vector4 colour,
            string? tooltip = null) =>
            Ui.TextEllipsis(text, maxWidth, colour, tooltip);

        /// <summary>Coloured text with the configured shadow behind it.</summary>
        public static void Text(string text, Vector4 colour) => Ui.TextShadowed(text, colour);

        public static void Text(string text) => Ui.TextShadowed(text);

        /// <summary>Same argument order as ImGui.TextColored, with a shadow.</summary>
        public static void TextColored(Vector4 colour, string text) => Ui.TextColoured(colour, text);

        /// <summary>Same as ImGui.TextUnformatted, with a shadow.</summary>
        public static void TextUnformatted(string text) => Ui.TextUnformatted(text);

        /// <summary>Figures in the monospace face, so columns of numbers line up.</summary>
        public static void TextMono(string text, Vector4 colour) => Ui.TextMono(text, colour);

        public static void DropShadow(Vector2 min, Vector2 max, float spread = 9f, float strength = 0.55f) =>
            Ui.DropShadow(min, max, spread, strength);

        public static void ShadowForNext(Vector2 size, float spread = 9f, float strength = 0.55f) =>
            Ui.ShadowForNext(size, spread, strength);

        public static bool BeginPanel(string id, Vector2 size, bool shadow = true, Vector4? background = null) =>
            Ui.BeginPanel(id, size, shadow, background);

        public static void EndPanel() => Ui.EndPanel();
    }
}

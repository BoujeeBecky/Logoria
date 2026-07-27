using System;
using System.Linq;
using System.Numerics;

namespace UiKit
{
    /// <summary>
    /// A named colour variant.
    /// <para>
    /// Only the brand and surface colours are themed. Semantic colours (Success,
    /// Warning, Danger) deliberately stay constant across every preset: danger has
    /// to read as danger no matter which theme is on. Text colours stay constant
    /// too, so contrast never depends on the user's taste.
    /// </para>
    /// </summary>
    public sealed record UiThemePreset(
        string Name,
        Vector4 Accent,
        Vector4 AccentDim,
        Vector4 Highlight,
        Vector4 Backdrop,
        Vector4 Panel,
        Vector4 PanelRaised,
        Vector4 PanelHover,
        Vector4 Border);

    public static class UiThemes
    {
        private static Vector4 C(int r, int g, int b, float a = 1f) => UiPalette.Rgb(r, g, b, a);

        /// <summary>
        /// Built-in presets. Accent drives selection, primary buttons and progress
        /// fills, so it is chosen to stay readable at small sizes rather than to be
        /// the loudest colour in the palette.
        /// </summary>
        public static readonly UiThemePreset[] All =
        [
            new("Aetherial",
                Accent:      C(0x5A, 0xD4, 0xE6),   // eureka cyan
                AccentDim:   C(0x2E, 0x6E, 0x7A),
                Highlight:   C(0xE8, 0xC4, 0x6A),
                Backdrop:    C(0x14, 0x16, 0x1B, 0.97f),
                Panel:       C(0x1B, 0x1E, 0x25),
                PanelRaised: C(0x23, 0x27, 0x30),
                PanelHover:  C(0x2C, 0x31, 0x3C),
                Border:      C(0x33, 0x38, 0x44)),

            new("Classic",
                Accent:      C(0xE8, 0xC0, 0x4A),   // gold
                AccentDim:   C(0x6E, 0x5A, 0x22),
                Highlight:   C(0x66, 0xA6, 0xFF),   // soft blue
                Backdrop:    C(0x10, 0x10, 0x1C, 0.97f),
                Panel:       C(0x17, 0x17, 0x28),
                PanelRaised: C(0x1F, 0x1F, 0x35),
                PanelHover:  C(0x2A, 0x2A, 0x46),
                Border:      C(0x3A, 0x3A, 0x60)),

            new("Synthwave",
                Accent:      C(0xFF, 0x4A, 0xA6),   // hot pink
                AccentDim:   C(0x7A, 0x22, 0x50),
                Highlight:   C(0x35, 0xE0, 0xFF),   // electric cyan
                Backdrop:    C(0x11, 0x08, 0x1D, 0.97f),
                Panel:       C(0x18, 0x0C, 0x28),
                PanelRaised: C(0x22, 0x12, 0x36),
                PanelHover:  C(0x30, 0x1A, 0x4A),
                Border:      C(0x59, 0x28, 0x72)),

            new("Ice",
                Accent:      C(0x8C, 0xDC, 0xFF),   // glacial cyan
                AccentDim:   C(0x35, 0x60, 0x7C),
                Highlight:   C(0xBF, 0xF0, 0xF0),   // frost white
                Backdrop:    C(0x0B, 0x11, 0x18, 0.97f),
                Panel:       C(0x11, 0x19, 0x23),
                PanelRaised: C(0x18, 0x23, 0x30),
                PanelHover:  C(0x21, 0x30, 0x41),
                Border:      C(0x32, 0x4C, 0x66)),

            new("Crimson Court",
                Accent:      C(0xE8, 0xC0, 0x40),   // rich gold
                AccentDim:   C(0x6E, 0x59, 0x1E),
                Highlight:   C(0xF0, 0x59, 0x63),   // crimson
                Backdrop:    C(0x14, 0x07, 0x0A, 0.97f),
                Panel:       C(0x1D, 0x0B, 0x0F),
                PanelRaised: C(0x28, 0x10, 0x16),
                PanelHover:  C(0x36, 0x17, 0x1F),
                Border:      C(0x59, 0x22, 0x2E)),

            new("Emerald Casino",
                Accent:      C(0xE8, 0xC4, 0x38),   // table gold
                AccentDim:   C(0x6B, 0x5A, 0x1A),
                Highlight:   C(0x44, 0xE0, 0x8C),   // emerald
                Backdrop:    C(0x06, 0x12, 0x0C, 0.97f),
                Panel:       C(0x0A, 0x1A, 0x12),
                PanelRaised: C(0x10, 0x24, 0x19),
                PanelHover:  C(0x17, 0x32, 0x23),
                Border:      C(0x24, 0x50, 0x38)),

            // Green-forward with yellow accents rather than yellow-dominant: a
            // yellow-heavy surface reads as glare and collides with Warning.
            new("Banana",
                Accent:      C(0xE8, 0xD1, 0x45),   // ripe banana
                AccentDim:   C(0x6B, 0x60, 0x1E),
                Highlight:   C(0x8C, 0xE0, 0x66),   // leaf green
                Backdrop:    C(0x10, 0x11, 0x07, 0.97f),
                Panel:       C(0x17, 0x19, 0x0B),
                PanelRaised: C(0x20, 0x23, 0x10),
                PanelHover:  C(0x2C, 0x30, 0x17),
                Border:      C(0x4C, 0x4A, 0x1F)),

            new("Boujee",
                Accent:      C(0xF5, 0xB8, 0x95),   // rose gold
                AccentDim:   C(0x75, 0x53, 0x42),
                Highlight:   C(0x9E, 0xAD, 0xFF),   // periwinkle
                Backdrop:    C(0x0C, 0x0C, 0x1A, 0.97f),
                Panel:       C(0x12, 0x12, 0x25),
                PanelRaised: C(0x1B, 0x1A, 0x33),
                PanelHover:  C(0x26, 0x24, 0x45),
                Border:      C(0x5E, 0x46, 0x46)),

            new("Opulent",
                Accent:      C(0xE8, 0xC8, 0x59),   // ornate gold
                AccentDim:   C(0x6E, 0x5C, 0x26),
                Highlight:   C(0xC4, 0x9E, 0xFF),   // royal lilac
                Backdrop:    C(0x0F, 0x09, 0x17, 0.97f),
                Panel:       C(0x16, 0x0D, 0x22),
                PanelRaised: C(0x20, 0x14, 0x30),
                PanelHover:  C(0x2C, 0x1C, 0x42),
                Border:      C(0x59, 0x48, 0x22)),

            new("Graphite",
                Accent:      C(0xA8, 0xB2, 0xC4),   // neutral steel
                AccentDim:   C(0x4A, 0x52, 0x60),
                Highlight:   C(0xD8, 0xDE, 0xE8),
                Backdrop:    C(0x13, 0x14, 0x16, 0.97f),
                Panel:       C(0x1A, 0x1B, 0x1E),
                PanelRaised: C(0x23, 0x25, 0x29),
                PanelHover:  C(0x2E, 0x31, 0x36),
                Border:      C(0x3A, 0x3D, 0x43)),
        ];

        public static UiThemePreset Default => All[0];

        public static string[] Names => All.Select(p => p.Name).ToArray();

        public static UiThemePreset Find(string? name) =>
            All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? Default;

        /// <summary>
        /// Builds a palette from a preset, keeping every non-themed value (text,
        /// semantics, metrics, shadow settings) from <paramref name="basePalette"/>.
        /// </summary>
        public static UiPalette Build(UiThemePreset preset, UiPalette? basePalette = null)
        {
            var b = basePalette ?? new UiPalette();

            return b with
            {
                Accent = preset.Accent,
                AccentDim = preset.AccentDim,
                Highlight = preset.Highlight,
                Backdrop = preset.Backdrop,
                Panel = preset.Panel,
                PanelRaised = preset.PanelRaised,
                PanelHover = preset.PanelHover,
                Border = preset.Border,
                BorderBright = Lighten(preset.Border, 0.18f),
            };
        }

        public static UiPalette Build(string? name, UiPalette? basePalette = null) =>
            Build(Find(name), basePalette);

        /// <summary>
        /// Switches the live palette, preserving anything not owned by the preset.
        /// Components read <see cref="UiPalette.Current"/> per frame, so this
        /// repaints everything immediately.
        /// </summary>
        public static void Apply(string? name) =>
            UiPalette.Current = Build(name, UiPalette.Current);

        private static Vector4 Lighten(Vector4 c, float amount) =>
            new(
                Math.Clamp(c.X + amount, 0f, 1f),
                Math.Clamp(c.Y + amount, 0f, 1f),
                Math.Clamp(c.Z + amount, 0f, 1f),
                c.W);
    }
}

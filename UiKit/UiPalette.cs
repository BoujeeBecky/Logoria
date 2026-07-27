using System.Numerics;

namespace UiKit
{
    /// <summary>How text is separated from what sits behind it.</summary>
    public enum TextShadowStyle
    {
        /// <summary>No shadow. Cheapest, fine on flat opaque panels.</summary>
        None,

        /// <summary>One offset copy behind the text.</summary>
        Drop,

        /// <summary>Copies on all four sides. Best for text over the game world.</summary>
        Outline,
    }

    /// <summary>
    /// Every colour the kit draws with. Swap <see cref="Current"/> at plugin start
    /// to rebrand the whole UI without touching a single component.
    /// </summary>
    public sealed record UiPalette
    {
        public Vector4 Backdrop { get; init; } = Rgb(0x14, 0x16, 0x1B, 0.97f);
        public Vector4 Panel { get; init; } = Rgb(0x1B, 0x1E, 0x25);
        public Vector4 PanelRaised { get; init; } = Rgb(0x23, 0x27, 0x30);
        public Vector4 PanelHover { get; init; } = Rgb(0x2C, 0x31, 0x3C);
        public Vector4 Border { get; init; } = Rgb(0x33, 0x38, 0x44);
        public Vector4 BorderBright { get; init; } = Rgb(0x45, 0x4C, 0x5C);

        public Vector4 TextPrimary { get; init; } = Rgb(0xE6, 0xE8, 0xEE);
        public Vector4 TextMuted { get; init; } = Rgb(0x92, 0x98, 0xA8);
        public Vector4 TextFaint { get; init; } = Rgb(0x62, 0x68, 0x78);

        /// <summary>Primary brand colour: selection, focus, primary actions.</summary>
        public Vector4 Accent { get; init; } = Rgb(0x5A, 0xD4, 0xE6);
        public Vector4 AccentDim { get; init; } = Rgb(0x2E, 0x6E, 0x7A);

        public Vector4 Highlight { get; init; } = Rgb(0xE8, 0xC4, 0x6A);
        public Vector4 Success { get; init; } = Rgb(0x63, 0xD1, 0x8A);
        public Vector4 Warning { get; init; } = Rgb(0xE0, 0xA8, 0x5C);
        public Vector4 Danger { get; init; } = Rgb(0xE0, 0x6C, 0x6C);

        // ---- metrics -------------------------------------------------------
        public float Rounding { get; init; } = 6f;
        public float WindowRounding { get; init; } = 8f;
        public Vector2 WindowPadding { get; init; } = new(14f, 14f);
        public Vector2 FramePadding { get; init; } = new(11f, 7f);
        public Vector2 ItemSpacing { get; init; } = new(9f, 9f);
        public Vector2 ItemInnerSpacing { get; init; } = new(8f, 6f);

        /// <summary>Inner padding for panels and child surfaces.</summary>
        public Vector2 PanelPadding { get; init; } = new(14f, 12f);

        /// <summary>Padding inside table cells. Generous, so rows breathe.</summary>
        public Vector2 CellPadding { get; init; } = new(14f, 10f);

        /// <summary>Left inset for nav row labels.</summary>
        public float NavTextInset { get; init; } = 16f;

        // ---- depth ---------------------------------------------------------

        /// <summary>
        /// How far surfaces lighten at the top and darken at the bottom, 0 to 1.
        /// A flat fill reads as a hole punched in the screen; a slight vertical
        /// gradient is what makes a panel look like a lit surface. Keep it small:
        /// past about 0.08 it stops reading as lighting and starts looking striped.
        /// </summary>
        public float SurfaceGradient { get; init; } = 0.045f;

        /// <summary>
        /// Strength of the bevel: a light hairline along the top inside edge and a
        /// dark one along the bottom. This is what sells the raised look, more than
        /// the gradient does. 0 disables it.
        /// </summary>
        public float BevelStrength { get; init; } = 0.55f;

        /// <summary>
        /// Glassy shine across the top of raised elements. This is what reads as a
        /// curved, lit surface rather than a flat chip. 0 disables it.
        /// </summary>
        public float GlossStrength { get; init; } = 0.8f;

        /// <summary>
        /// Whether small rounded elements grade edge-to-centre rather than
        /// top-to-bottom.
        /// <para>
        /// Affects pills and the progress-bar fill only, never panels. A panel is a
        /// flat plane and should stay planar; a pill is a small token and reads as a
        /// physical object when it domes. Taste rather than correctness, so it is a
        /// switch instead of a decision baked into the components.
        /// </para>
        /// </summary>
        public bool DomedTokens { get; init; } = true;

        // ---- glass ---------------------------------------------------------

        /// <summary>
        /// Frosted-glass surfaces: translucent fill, bright hairline border, larger
        /// radius.
        /// <para>
        /// Note this cannot reproduce CSS <c>backdrop-filter: blur()</c>. Blurring
        /// what sits behind an element means sampling the framebuffer under it, and
        /// ImGui only appends geometry to a draw list. What is behind stays sharp.
        /// The tint, border and sheen carry the look.
        /// </para>
        /// </summary>
        public bool Glass { get; init; }

        /// <summary>Tint laid over whatever shows through a glass surface.</summary>
        public Vector4 GlassTint { get; init; } = new(1f, 1f, 1f, 0.055f);

        /// <summary>
        /// The bright hairline around a glass surface. This edge is the single most
        /// recognisable part of the look: without it a translucent panel just reads
        /// as a faded box.
        /// </summary>
        public Vector4 GlassBorder { get; init; } = new(1f, 1f, 1f, 0.33f);

        /// <summary>How much of the surface colour survives. 0 is fully see-through.</summary>
        public float GlassOpacity { get; init; } = 0.30f;

        public float GlassRounding { get; init; } = 12f;

        public float GlassBorderWidth { get; init; } = 1.5f;

        // ---- text shadow ---------------------------------------------------

        /// <summary>
        /// How the kit shadows text. <see cref="TextShadowStyle.Drop"/> is a single
        /// offset copy, cheap and good on panels. <see cref="TextShadowStyle.Outline"/>
        /// draws on all four sides, which is what keeps overlay text legible against
        /// an arbitrary game background.
        /// </summary>
        public TextShadowStyle TextShadow { get; init; } = TextShadowStyle.Drop;

        public Vector2 TextShadowOffset { get; init; } = new(1f, 1f);

        public Vector4 TextShadowColour { get; init; } = new(0f, 0f, 0f, 0.85f);

        public float ScrollbarSize { get; init; } = 12f;

        /// <summary>
        /// Maximum error, in pixels, when ImGui tessellates a circle or a rounded
        /// corner. Lower means more segments and smoother curves. ImGui's default of
        /// 0.30 visibly facets at the radii glass mode uses.
        /// </summary>
        public float CircleTessellationMaxError { get; init; } = 0.12f;

        // ---- vanilla -------------------------------------------------------

        /// <summary>
        /// Strips the kit back to plain ImGui: no theme, no gradients, bevels,
        /// gloss, shadows, grain, text shadows or animation. Components fall back to
        /// stock widgets.
        /// <para>
        /// This is a performance escape hatch for weaker machines, not a style. It
        /// removes hundreds of draw-list calls per frame, so it also removes the
        /// theming: colours come from ImGui's defaults, and the theme picker stops
        /// having any effect. Opt-in, never the default.
        /// </para>
        /// </summary>
        public bool Vanilla { get; init; }

        /// <summary>Shorthand for the check every component makes.</summary>
        public static bool IsVanilla => Current.Vanilla;

        /// <summary>The palette all components read from. Assign once at startup.</summary>
        public static UiPalette Current { get; set; } = new();

        public static Vector4 Rgb(int r, int g, int b, float a = 1f) =>
            new(r / 255f, g / 255f, b / 255f, a);

        /// <summary>Same colour at a fraction of its current alpha.</summary>
        public static Vector4 Fade(Vector4 c, float alpha) => c with { W = c.W * alpha };

        /// <summary>
        /// Composites <paramref name="over"/> onto <paramref name="under"/> and
        /// returns an opaque result.
        /// <para>
        /// Use this instead of drawing translucent colours when the result will be
        /// banded or layered: stacking translucent fills compounds their alpha where
        /// they overlap, which shows up as visible seams.
        /// </para>
        /// </summary>
        public static Vector4 Blend(Vector4 under, Vector4 over)
        {
            var a = System.Math.Clamp(over.W, 0f, 1f);
            return new Vector4(
                (over.X * a) + (under.X * (1f - a)),
                (over.Y * a) + (under.Y * (1f - a)),
                (over.Z * a) + (under.Z * (1f - a)),
                under.W);
        }

        /// <summary>Moves a colour toward white. Negative amounts move toward black.</summary>
        public static Vector4 Shade(Vector4 c, float amount) => new(
            System.Math.Clamp(c.X + amount, 0f, 1f),
            System.Math.Clamp(c.Y + amount, 0f, 1f),
            System.Math.Clamp(c.Z + amount, 0f, 1f),
            c.W);
    }
}

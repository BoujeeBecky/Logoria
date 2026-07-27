using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace UiKit
{
    /// <summary>
    /// Drawn components. Everything here is procedural: no bitmap assets, no custom
    /// fonts required. All of it reads <see cref="UiPalette.Current"/>.
    /// </summary>
    public static class Ui
    {
        private static UiPalette P => UiPalette.Current;

        // ---- optional textures ---------------------------------------------
        // The kit stays asset-free by default: every effect has a procedural
        // implementation and only upgrades if a plugin supplies art.

        /// <summary>
        /// 9-sliceable soft shadow. Supply one for a true gaussian falloff; without
        /// it <see cref="DropShadow"/> stacks rects, which is close but bands a
        /// little at large spreads.
        /// </summary>
        public static ImTextureID? ShadowTexture { get; set; }

        /// <summary>Border inset of the shadow sprite, in source pixels.</summary>
        public static float ShadowTextureInset { get; set; } = 64f;

        /// <summary>
        /// Seamless grain tiled over surfaces. Breaks up gradient banding and gives
        /// flat fills a hint of material.
        /// </summary>
        public static ImTextureID? NoiseTexture { get; set; }

        public static float NoiseTileSize { get; set; } = 128f;

        /// <summary>0 disables the grain even when a texture is set.</summary>
        public static float NoiseStrength { get; set; } = 1f;

        /// <summary>
        /// Soft glow frame drawn around the selected nav row, over its fill. Must be
        /// white so it can take the theme's accent.
        /// </summary>
        public static ImTextureID? NavHighlightTexture { get; set; }

        /// <summary>Where the corners end in the nav highlight sprite, 0 to 0.5.</summary>
        public static float NavHighlightInset { get; set; } = 0.30f;

        /// <summary>
        /// Glyph font for component icons, normally Dalamud's FontAwesome handle.
        /// Supplied by the plugin so the kit does not reach for Dalamud services
        /// itself. Components fall back to no icon when it is unset.
        /// </summary>
        /// <remarks>
        /// Test with <see cref="HasFont"/>, never with HasValue: ImFontPtr is a
        /// struct, so assigning one to a nullable always yields HasValue even when
        /// the pointer inside is null, and pushing a null font crashes natively.
        /// </remarks>
        public static ImFontPtr? IconFont { get; set; }

        /// <summary>Width reserved for a nav icon, including its gap.</summary>
        public static float NavIconWidth { get; set; } = 24f;

        /// <summary>
        /// Fixed-width face for figures. Proportional digits make a column of
        /// numbers look ragged and shift as values change; tabular ones stay put.
        /// Unset simply falls back to the normal font.
        /// </summary>
        public static ImFontPtr? MonoFont { get; set; }

        /// <summary>
        /// True only when a font handle is present *and* points at a real font.
        /// Dalamud's font pointers are null until the atlas has been built, and
        /// during a rebuild, so this has to be re-tested every frame.
        /// </summary>
        private static bool TryGetFont(ImFontPtr? font, out ImFontPtr value)
        {
            value = font ?? default;
            return font.HasValue && !value.IsNull;
        }

        private static uint C(Vector4 colour) => ImGui.GetColorU32(colour);

        private static Vector4 Fade(Vector4 colour, float alpha) => UiPalette.Fade(colour, alpha);

        // ---- text ----------------------------------------------------------

        /// <summary>
        /// Draws the shadow pass for a string at a screen position. Call immediately
        /// before drawing the real text at the same position.
        /// </summary>
        public static void TextShadowAt(ImDrawListPtr draw, Vector2 pos, string text)
        {
            if (P.Vanilla) return;

            var style = P.TextShadow;
            if (style == TextShadowStyle.None || string.IsNullOrEmpty(text)) return;

            var shadow = C(P.TextShadowColour);

            if (style == TextShadowStyle.Drop)
            {
                var o = P.TextShadowOffset;
                draw.AddText(new Vector2(pos.X + o.X, pos.Y + o.Y), shadow, text);
                return;
            }

            // Outline: four cardinal offsets. Eight would be smoother but doubles
            // the draw calls for a difference you cannot see at UI sizes.
            var d = MathF.Max(1f, P.TextShadowOffset.X);
            draw.AddText(new Vector2(pos.X - d, pos.Y), shadow, text);
            draw.AddText(new Vector2(pos.X + d, pos.Y), shadow, text);
            draw.AddText(new Vector2(pos.X, pos.Y - d), shadow, text);
            draw.AddText(new Vector2(pos.X, pos.Y + d), shadow, text);
        }

        /// <summary>Draw-list text with the configured shadow behind it.</summary>
        public static void TextShadowedAt(ImDrawListPtr draw, Vector2 pos, uint colour, string text)
        {
            TextShadowAt(draw, pos, text);
            draw.AddText(pos, colour, text);
        }

        /// <summary>
        /// Coloured text with a shadow, laid out by ImGui as normal.
        /// <para>
        /// The shadow is drawn into the draw list first, then the real
        /// <c>TextColored</c> runs, so cursor advance, wrapping and hover all behave
        /// exactly as they would without the shadow.
        /// </para>
        /// </summary>
        public static void TextShadowed(string text, Vector4 colour)
        {
            if (string.IsNullOrEmpty(text)) return;

            TextShadowAt(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), text);
            ImGui.TextColored(colour, text);
        }

        /// <summary>Shadowed text in the current text colour.</summary>
        public static void TextShadowed(string text) => TextShadowed(text, P.TextPrimary);

        /// <summary>
        /// Drop-in replacement for <c>ImGui.TextColored</c>, same argument order, with
        /// the configured shadow behind it. Exists so existing call sites can be
        /// swapped over without reordering arguments.
        /// </summary>
        public static void TextColoured(Vector4 colour, string text) => TextShadowed(text, colour);

        /// <summary>Drop-in replacement for <c>ImGui.TextUnformatted</c>.</summary>
        public static void TextUnformatted(string text) => TextShadowed(text, P.TextPrimary);

        /// <summary>
        /// Figures in the monospace face, so counts and percentages line up down a
        /// column and do not jitter as their values change width.
        /// </summary>
        public static void TextMono(string text, Vector4 colour)
        {
            if (!TryGetFont(MonoFont, out var mono))
            {
                TextShadowed(text, colour);
                return;
            }

            ImGui.PushFont(mono);
            TextShadowed(text, colour);
            ImGui.PopFont();
        }

        /// <summary>Small muted caption with a leading accent bar.</summary>
        public static void SectionLabel(string text)
        {
            if (P.Vanilla)
            {
                ImGui.TextDisabled(text.ToUpperInvariant());
                return;
            }

            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var height = ImGui.GetTextLineHeight();

            draw.AddRectFilled(
                new Vector2(pos.X, pos.Y + 2f),
                new Vector2(pos.X + 3f, pos.Y + height - 2f),
                C(P.Accent), 1.5f);

            ImGui.Dummy(new Vector2(9f, height));
            ImGui.SameLine(0f, 0f);
            TextShadowed(text.ToUpperInvariant(), P.TextMuted);
        }

        /// <summary>
        /// Truncates to a pixel width with an ellipsis and shows the full text on
        /// hover. Keeps table rows to a uniform height.
        /// <para>
        /// Pass <paramref name="tooltip"/> when the hover should say more than the
        /// text itself, for instance a role summary whose tooltip lists the actual
        /// jobs. A supplied tooltip shows whether or not the text was truncated,
        /// since it is extra information rather than a recovery of what was cut.
        /// </para>
        /// </summary>
        public static void TextEllipsis(string text, float maxWidth, Vector4 colour,
            string? tooltip = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            var full = text.Replace("\n", " ");
            var fits = ImGui.CalcTextSize(full).X <= maxWidth;

            if (fits)
            {
                TextShadowed(full, colour);
            }
            else
            {
                var truncated = full;
                while (truncated.Length > 1 && ImGui.CalcTextSize(truncated + "...").X > maxWidth)
                    truncated = truncated[..^1];

                TextShadowed(truncated + "...", colour);
            }

            var hover = tooltip ?? (fits ? null : text);
            if (hover == null || !ImGui.IsItemHovered()) return;

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(420f);
            ImGui.TextUnformatted(hover);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        /// <summary>Horizontal rule. ImGui's AddLine is a no-op in these bindings.</summary>
        public static void Rule(float alpha = 1f)
        {
            if (P.Vanilla)
            {
                ImGui.Separator();
                return;
            }

            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            draw.AddRectFilled(pos, new Vector2(pos.X + width, pos.Y + 1f), C(Fade(P.Border, alpha)));
            ImGui.Dummy(new Vector2(width, 1f));
        }

        public static void CentredText(string label, Vector4 colour)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var size = ImGui.CalcTextSize(label);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (width - size.X) * 0.5f));

            // Shadowed like every other text helper here. This was the one that
            // called ImGui.TextColored directly, so centred text lost its shadow and
            // went hard to read the moment glass mode or an overlay was involved.
            TextShadowed(label, colour);
        }

        // ---- controls ------------------------------------------------------

        /// <summary>Where a group's highlight currently is, between frames.</summary>
        private sealed class NavGroupState
        {
            public float OffsetY;
            public float Height;
            public float Width;
            public bool HasTarget;

            /// <summary>Last frame's answer, since Begin runs before any item draws.</summary>
            public bool HasSelection = true;
        }

        private static readonly Dictionary<uint, NavGroupState> NavGroups = new();
        private static uint currentNavGroup;
        private static Vector2 currentNavOrigin;
        private static bool navGroupHadSelection;

        /// <summary>
        /// Starts a group whose selected row slides between positions instead of
        /// jumping.
        /// <para>
        /// The highlight is drawn here, at the top, rather than by the selected item:
        /// it has to sit behind every row's text, and an immediate-mode list cannot
        /// go back and paint underneath something it already drew. That means it uses
        /// the rectangle recorded last frame, which is why the position is stored as
        /// an offset from the group's origin rather than as screen coordinates. Store
        /// screen coordinates and the highlight detaches the moment the window moves
        /// or the list scrolls.
        /// </para>
        /// <para>
        /// One frame of lag is invisible at any real framerate, and the animation is
        /// chasing the target anyway.
        /// </para>
        /// </summary>
        public static void BeginNavGroup(string id)
        {
            if (P.Vanilla) return;

            currentNavGroup = ImGui.GetID(id);
            currentNavOrigin = ImGui.GetCursorScreenPos();
            navGroupHadSelection = false;

            if (!NavGroups.TryGetValue(currentNavGroup, out var state))
            {
                NavGroups[currentNavGroup] = new NavGroupState();
                return;
            }

            if (!state.HasTarget) return;

            // Three keys off the group id. Height is animated too, so a group with
            // rows of different heights does not snap as the highlight arrives.
            var y = UiAnim.Approach(currentNavGroup ^ 0x5EEDu, state.OffsetY, 18f);
            var h = UiAnim.Approach(currentNavGroup ^ 0xB0D1u, state.Height, 18f);
            var presence = UiAnim.Approach(currentNavGroup ^ 0xA1FAu, state.HasSelection ? 1f : 0f, 14f);

            if (presence <= 0.002f) return;

            var min = new Vector2(currentNavOrigin.X, currentNavOrigin.Y + y);
            var max = new Vector2(min.X + state.Width, min.Y + h);

            DrawNavSelection(min, max, presence);
        }

        public static void EndNavGroup()
        {
            if (P.Vanilla) return;

            if (currentNavGroup != 0 && NavGroups.TryGetValue(currentNavGroup, out var state))
                state.HasSelection = navGroupHadSelection;

            currentNavGroup = 0;
        }

        /// <summary>
        /// Drops every scrap of retained draw state. Call from the plugin's Dispose,
        /// alongside <see cref="UiAnim.Clear"/>.
        /// <para>
        /// Dalamud normally unloads a plugin into a collectible load context, so
        /// statics usually die with it. "Usually" is the problem: an unload that
        /// cannot complete leaves them alive, and a half-populated
        /// <see cref="panelStyled"/> stack corrupts the ImGui style stack for every
        /// plugin drawing afterwards. Clearing costs nothing and removes the whole
        /// question.
        /// </para>
        /// </summary>
        public static void ResetState()
        {
            NavGroups.Clear();
            currentNavGroup = 0;
            navGroupHadSelection = false;

            // A panel whose EndPanel was skipped, for instance by an exception thrown
            // mid-Draw, leaves an entry here. It is not self-correcting: every later
            // EndPanel pops somebody else's flag and unbalances the style stack.
            panelStyled.Clear();
        }

        /// <summary>
        /// The selected-row treatment, shared by the grouped and ungrouped paths so
        /// they cannot drift apart.
        /// </summary>
        private static void DrawNavSelection(Vector2 min, Vector2 max, float alpha)
        {
            var draw = ImGui.GetWindowDrawList();
            var height = max.Y - min.Y;

            if (NavHighlightTexture.HasValue)
            {
                // The sprite already carries fill, inner glow, gloss and rim, so it
                // replaces the drawn treatment rather than layering over it. Doubling
                // them up reads as a muddy double-border.
                NineSlice(
                    NavHighlightTexture.Value,
                    min, max,
                    height * 0.5f,
                    NavHighlightInset,
                    C(Fade(P.Accent, 0.95f * alpha)));
            }
            else
            {
                // Composite the accent onto the panel first so the band colours are
                // opaque. Grading translucent colours compounds alpha at the band
                // overlaps and shows as stripes through the highlight.
                var top = UiPalette.Blend(P.Panel, Fade(P.Accent, 0.26f));
                var bottom = UiPalette.Blend(P.Panel, Fade(P.Accent, 0.10f));

                GradientRect(min, max, Fade(top, alpha), Fade(bottom, alpha), P.Rounding);
                draw.AddRect(min, max, C(Fade(P.Accent, 0.40f * alpha)), P.Rounding, 0, 1f);
                Bevel(min, max, P.Rounding, P.BevelStrength * 0.8f * alpha);
                Gloss(min, max, P.Rounding, P.GlossStrength * alpha);
            }

            draw.AddRectFilled(min, new Vector2(min.X + 3f, max.Y), C(Fade(P.Accent, alpha)), 1.5f);
        }

        /// <summary>
        /// Full-width sidebar row with selection fill, hover fill, an optional icon
        /// glyph and an optional badge.
        /// </summary>
        /// <param name="icon">
        /// A glyph from <see cref="IconFont"/>, e.g. FontAwesomeIcon.Book.ToIconString().
        /// </param>
        /// <param name="slide">
        /// False to opt out of an enclosing <see cref="BeginNavGroup"/>'s sliding
        /// highlight and draw a static one instead. Use it for rows whose "selected"
        /// means something other than "you are here", since several of those can be
        /// true at once and a single sliding highlight cannot be in two places.
        /// </param>
        public static bool NavItem(string label, bool selected, string? badge = null,
            string? icon = null, bool slide = true)
        {
            if (P.Vanilla)
            {
                var text = string.IsNullOrEmpty(badge) ? label : $"{label}  ({badge})";
                var picked = ImGui.Selectable(text, selected);
                return picked;
            }

            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = ImGui.GetTextLineHeight() + 12f;

            ImGui.InvisibleButton($"##nav_{label}", new Vector2(width, height));
            var hovered = ImGui.IsItemHovered();
            var clicked = ImGui.IsItemClicked();

            // Fade rather than snap. Nothing else in this method is stateful, so the
            // animation store is what makes an immediate-mode row feel responsive.
            var hoverAmount = UiAnim.Toggle(UiAnim.Key($"navhover_{label}"), hovered, 16f);

            var max = new Vector2(pos.X + width, pos.Y + height);

            // Inside a nav group, a sliding row hands its rectangle to the group and
            // draws no background of its own: the group drew one highlight for all of
            // them, already on its way here.
            var handedToGroup = false;

            if (selected && slide && currentNavGroup != 0
                && NavGroups.TryGetValue(currentNavGroup, out var group))
            {
                group.OffsetY = pos.Y - currentNavOrigin.Y;
                group.Height = height;
                group.Width = width;
                group.HasTarget = true;
                navGroupHadSelection = true;
                handedToGroup = true;
            }

            if (selected && !handedToGroup)
            {
                DrawNavSelection(pos, max, 1f);
            }
            else if (!selected && hoverAmount > 0.001f)
            {
                // Faded in, so moving the mouse down a list leaves a soft trail
                // instead of a hard on/off flicker.
                GradientRect(pos, max,
                    Fade(UiPalette.Shade(P.PanelHover, 0.02f), hoverAmount),
                    Fade(UiPalette.Shade(P.PanelHover, -0.02f), hoverAmount), P.Rounding);
                Gloss(pos, max, P.Rounding, P.GlossStrength * 0.5f * hoverAmount);
            }

            var textX = pos.X + P.NavTextInset;

            if (!string.IsNullOrEmpty(icon) && TryGetFont(IconFont, out var font))
            {
                var iconColour = selected
                    ? P.Accent
                    : Vector4.Lerp(P.TextMuted, P.TextPrimary, hoverAmount);

                // Measure through the font stack: ImFontPtr has no direct measure in
                // these bindings, and PushFont makes CalcTextSize use it.
                ImGui.PushFont(font);
                var glyphWidth = ImGui.CalcTextSize(icon).X;
                ImGui.PopFont();

                // Centre each glyph in a fixed column so labels line up even though
                // FontAwesome glyphs vary in width.
                var glyphX = textX + ((NavIconWidth - 8f - glyphWidth) * 0.5f);
                draw.AddText(font, font.FontSize, new Vector2(glyphX, pos.Y + 6f),
                    C(iconColour), icon);

                textX += NavIconWidth;
            }

            var textColour = selected
                ? P.Accent
                : Vector4.Lerp(P.TextMuted, P.TextPrimary, hoverAmount);

            TextShadowedAt(draw, new Vector2(textX, pos.Y + 6f), C(textColour), label);

            if (!string.IsNullOrEmpty(badge))
            {
                var size = ImGui.CalcTextSize(badge);
                TextShadowedAt(draw, new Vector2(max.X - size.X - 12f, pos.Y + 6f), C(P.TextFaint), badge);
            }

            return clicked;
        }

        /// <summary>Filled accent button, for the primary action in a group.</summary>
        public static bool ButtonAccent(string label, Vector2 size = default)
        {
            if (P.Vanilla) return ImGui.Button(label, size);

            ImGui.PushStyleColor(ImGuiCol.Button, Fade(P.Accent, 0.22f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Fade(P.Accent, 0.34f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Fade(P.Accent, 0.46f));
            ImGui.PushStyleColor(ImGuiCol.Text, P.Accent);
            var pressed = ImGui.Button(label, size);
            ImGui.PopStyleColor(4);

            // Shine and bevel go on after ImGui has drawn the button body.
            Bevel(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), P.Rounding, P.BevelStrength * 0.7f);
            GlossLastItem();

            return pressed;
        }

        /// <summary>Quiet button for secondary actions.</summary>
        public static bool ButtonGhost(string label, Vector2 size = default)
        {
            if (P.Vanilla) return ImGui.Button(label, size);

            ImGui.PushStyleColor(ImGuiCol.Button, Fade(P.PanelRaised, 0.55f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, P.PanelHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, P.PanelHover);
            ImGui.PushStyleColor(ImGuiCol.Text, P.TextMuted);
            var pressed = ImGui.Button(label, size);
            ImGui.PopStyleColor(4);

            Bevel(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), P.Rounding, P.BevelStrength * 0.45f);
            GlossLastItem(strength: P.GlossStrength * 0.5f);

            return pressed;
        }

        // ---- indicators ----------------------------------------------------

        /// <summary>Rounded status chip.</summary>
        public static void Pill(string text, Vector4 colour)
        {
            if (P.Vanilla)
            {
                ImGui.TextColored(colour, text);
                return;
            }

            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var size = ImGui.CalcTextSize(text);
            var w = size.X + 14f;
            var h = size.Y + 6f;

            var pillMax = new Vector2(pos.X + w, pos.Y + h);
            var radius = h * 0.5f;

            // Opaque composite first: a translucent graded fill would band.
            if (P.DomedTokens)
            {
                // Edge-to-centre, so the pill reads as a physical token rather than
                // a flat capsule of colour.
                var rim = UiPalette.Blend(P.Panel, Fade(colour, 0.11f));
                var core = UiPalette.Blend(P.Panel, Fade(colour, 0.30f));
                DomeRect(pos, pillMax, rim, core, radius, 7);
            }
            else
            {
                var top = UiPalette.Blend(P.Panel, Fade(colour, 0.26f));
                var bottom = UiPalette.Blend(P.Panel, Fade(colour, 0.13f));
                GradientRect(pos, pillMax, top, bottom, radius);
            }
            draw.AddRect(pos, pillMax, C(Fade(colour, 0.55f)), radius, 0, 1f);
            Gloss(pos, pillMax, radius, P.GlossStrength * 0.7f);
            TextShadowedAt(draw, new Vector2(pos.X + 7f, pos.Y + 3f), C(colour), text);

            ImGui.Dummy(new Vector2(w, h));
        }

        /// <summary>Dot plus label. Reads faster down a column than a pill.</summary>
        public static void StatusDot(string label, Vector4 colour, bool filled)
        {
            if (P.Vanilla)
            {
                ImGui.TextColored(colour, label);
                return;
            }

            var draw = ImGui.GetWindowDrawList();
            var radius = ImGui.GetFontSize() * 0.26f;
            var origin = ImGui.GetCursorScreenPos();
            var centre = new Vector2(origin.X + radius + 1f, origin.Y + ImGui.GetTextLineHeight() * 0.5f);

            if (filled)
            {
                draw.AddCircleFilled(centre, radius, C(colour), 16);
                draw.AddCircle(centre, radius * 1.9f, C(Fade(colour, 0.35f)), 20, 1.2f);
            }
            else
            {
                draw.AddCircle(centre, radius, C(colour), 16, 1.4f);
            }

            ImGui.Dummy(new Vector2(radius * 3.4f, ImGui.GetTextLineHeight()));
            ImGui.SameLine(0f, 5f);
            TextShadowed(label, colour);
        }

        /// <summary>Rounded-track progress bar with an accent fill.</summary>
        /// <param name="animateId">
        /// When set, the fill eases toward the value instead of jumping. Use a stable
        /// id per bar; sharing one between bars makes them fight over the same state.
        /// </param>
        public static void ProgressBar(
            float fraction, float height, string? overlay = null, Vector4? fill = null,
            string? animateId = null)
        {
            fraction = Math.Clamp(fraction, 0f, 1f);

            if (animateId != null)
                fraction = UiAnim.Approach(UiAnim.Key($"bar_{animateId}"), fraction, 9f);

            if (P.Vanilla)
            {
                ImGui.ProgressBar(fraction, new Vector2(-1, height), overlay ?? string.Empty);
                return;
            }

            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var max = new Vector2(pos.X + width, pos.Y + height);
            var radius = height * 0.5f;

            // Track sunk into the panel, fill raised inside it. The two lighting
            // directions are what makes the fill look like it sits *in* the groove
            // rather than being painted on top of it.
            var g = P.SurfaceGradient;
            Well(pos, max, P.PanelRaised, radius, border: false);

            if (fraction > 0f)
            {
                var colour = fill ?? P.Accent;
                var fillWidth = Math.Max(height, width * fraction);
                var fillMax = new Vector2(pos.X + fillWidth, max.Y);

                if (P.DomedTokens)
                {
                    // Grading inward makes the fill read as a lozenge sitting in the
                    // track rather than a flat painted strip.
                    DomeRect(pos, fillMax,
                        UiPalette.Shade(colour, -g * 1.6f), UiPalette.Shade(colour, g * 1.2f),
                        radius, 8);
                }
                else
                {
                    GradientRect(pos, fillMax,
                        UiPalette.Shade(colour, g * 1.4f), UiPalette.Shade(colour, -g * 1.4f),
                        radius);
                }

                Gloss(pos, fillMax, radius, P.GlossStrength * 0.7f);
            }

            draw.AddRect(pos, max, C(P.Border), radius, 0, 1f);

            if (!string.IsNullOrEmpty(overlay))
            {
                var size = ImGui.CalcTextSize(overlay);
                TextShadowedAt(draw,
                    new Vector2(pos.X + (width - size.X) * 0.5f, pos.Y + (height - size.Y) * 0.5f),
                    C(P.TextPrimary), overlay);
            }

            ImGui.Dummy(new Vector2(width, height));
        }

        // ---- depth ---------------------------------------------------------

        /// <summary>
        /// A vertically graded rounded rect: lighter at the top, darker at the
        /// bottom, so a surface reads as lit rather than as a flat hole.
        /// <para>
        /// ImGui's <c>AddRectFilledMultiColor</c> does real vertex-interpolated
        /// gradients but cannot round corners. So this draws horizontal bands with
        /// rounding applied only to the first and last, overlapping them by a pixel
        /// to hide the antialiased seams between adjacent fills.
        /// </para>
        /// </summary>
        public static void GradientRect(
            Vector2 min, Vector2 max, Vector4 top, Vector4 bottom, float rounding, int bands = 20)
        {
            var draw = ImGui.GetWindowDrawList();
            var height = max.Y - min.Y;

            if (height <= 0f || max.X - min.X <= 0f) return;

            // Vanilla, or too short to band meaningfully: one flat fill.
            if (P.Vanilla || height < 4f || bands < 2)
            {
                draw.AddRectFilled(min, max, C(top), rounding);
                return;
            }

            bands = Math.Min(bands, (int)height);
            var step = height / bands;

            // Overlapping bands hides antialiased seams, but only works for opaque
            // colours: with alpha, the overlap draws twice and reads as a stripe.
            // Blend translucent colours onto their backdrop first (UiPalette.Blend).
            var translucent = top.W < 0.999f || bottom.W < 0.999f;
            var overlap = translucent ? 0f : 1f;

            for (var i = 0; i < bands; i++)
            {
                var colour = Vector4.Lerp(top, bottom, i / (float)bands);

                var y0 = min.Y + (i * step);
                var y1 = i == bands - 1 ? max.Y : y0 + step + overlap;

                var flags = i == 0 ? ImDrawFlags.RoundCornersTop
                    : i == bands - 1 ? ImDrawFlags.RoundCornersBottom
                    : ImDrawFlags.RoundCornersNone;

                draw.AddRectFilled(
                    new Vector2(min.X, y0), new Vector2(max.X, y1), C(colour), rounding, flags);
            }
        }

        /// <summary>
        /// Light hairline along the top inside edge, dark along the bottom. This is
        /// the detail that actually sells a raised surface, more than the gradient.
        /// <para>
        /// Pass <paramref name="recessed"/> to flip it, for a surface that sits below
        /// the panel rather than above it. Flipping the gradient without flipping this
        /// too gives a shape that contradicts itself and just reads as muddy.
        /// </para>
        /// </summary>
        public static void Bevel(Vector2 min, Vector2 max, float rounding, float strength = -1f,
            bool recessed = false)
        {
            if (P.Vanilla) return;
            if (strength < 0f) strength = P.BevelStrength;
            if (strength <= 0f) return;

            var draw = ImGui.GetWindowDrawList();
            var inset = MathF.Max(1f, rounding * 0.5f);

            // Dark reads far more strongly than light on a dark UI, so the two alphas
            // are deliberately unequal. Equal values look top-heavy.
            var light = new Vector4(1f, 1f, 1f, 0.06f * strength);
            var dark = new Vector4(0f, 0f, 0f, 0.22f * strength);

            var top = recessed ? dark : light;
            var bottom = recessed ? light : dark;

            // Filled rects rather than AddLine, which is a no-op in these bindings.
            draw.AddRectFilled(
                new Vector2(min.X + inset, min.Y),
                new Vector2(max.X - inset, min.Y + 1f),
                C(top));

            draw.AddRectFilled(
                new Vector2(min.X + inset, max.Y - 1f),
                new Vector2(max.X - inset, max.Y),
                C(bottom));
        }

        /// <summary>
        /// A rect graded from its edge toward its centre: darker at the rim, lighter
        /// in the middle.
        /// <para>
        /// This says something different from <see cref="GradientRect"/>. A vertical
        /// grade reads as a flat plane tilted toward a light above. Grading inward
        /// reads as a <b>convex</b> object, because on a dome the point nearest the
        /// light is the middle. Good for pills, badges, dots and anything meant to
        /// look like a physical token rather than a panel.
        /// </para>
        /// <para>
        /// Built from concentric inset fills, and they are <b>opaque</b> on purpose.
        /// Translucent rings would each cover everything inside them, so their alpha
        /// would compound toward the centre and the falloff would go non-linear and
        /// blotchy. Lerping opaque colours makes each ring exactly the value it
        /// should be. This is the same rule that governs banded gradients.
        /// </para>
        /// </summary>
        public static void DomeRect(
            Vector2 min, Vector2 max, Vector4 edge, Vector4 centre, float rounding, int steps = 10)
        {
            var draw = ImGui.GetWindowDrawList();
            var width = max.X - min.X;
            var height = max.Y - min.Y;

            if (width <= 0f || height <= 0f) return;

            if (P.Vanilla || steps < 2)
            {
                draw.AddRectFilled(min, max, C(centre), rounding);
                return;
            }

            // Each ring insets by this much. More rings than half the short side
            // would collapse to nothing, so the count is capped by the geometry.
            var reach = MathF.Min(width, height) * 0.5f;
            steps = Math.Min(steps, Math.Max(2, (int)reach));

            var step = reach / steps;

            for (var i = 0; i < steps; i++)
            {
                var inset = i * step;
                var colour = Vector4.Lerp(edge, centre, i / (float)(steps - 1));

                // Rounding has to shrink with the inset or the inner rings keep the
                // outer radius and their corners bulge past the shape.
                var r = MathF.Max(0f, rounding - inset);

                draw.AddRectFilled(
                    new Vector2(min.X + inset, min.Y + inset),
                    new Vector2(max.X - inset, max.Y - inset),
                    C(colour), r);
            }
        }

        /// <summary>
        /// A recessed surface: something sunk into the panel rather than raised off
        /// it. The counterpart to <see cref="Surface"/>.
        /// <para>
        /// Everything about the lighting flips. The gradient is darker at the top,
        /// because the inside of a trough catches light on its lower face, and the
        /// bevel flips with it. Use it for tracks, wells, slots and input fields:
        /// anywhere the eye should read "things go in here".
        /// </para>
        /// </summary>
        public static void Well(
            Vector2 min, Vector2 max, Vector4 baseColour, float rounding, bool border = true)
        {
            if (P.Vanilla) return;

            var g = P.SurfaceGradient;

            GradientRect(min, max,
                UiPalette.Shade(baseColour, -g), UiPalette.Shade(baseColour, g), rounding);

            Bevel(min, max, rounding, recessed: true);

            if (border)
                ImGui.GetWindowDrawList().AddRect(min, max, C(P.Border), rounding, 0, 1f);
        }

        /// <summary>
        /// Glassy shine over the top of a shape, like light catching a curved
        /// surface.
        /// <para>
        /// Deliberately a single rounded-top rect rather than a faded gradient:
        /// one translucent fill cannot band, whereas a stack of them can. The hard
        /// edge at the midpoint is invisible at these alphas and is what reads as a
        /// highlight rather than a wash.
        /// </para>
        /// </summary>
        public static void Gloss(Vector2 min, Vector2 max, float rounding, float strength = -1f)
        {
            if (P.Vanilla) return;
            if (strength < 0f) strength = P.GlossStrength;
            if (strength <= 0f) return;

            var height = max.Y - min.Y;
            if (height <= 2f) return;

            ImGui.GetWindowDrawList().AddRectFilled(
                min,
                new Vector2(max.X, min.Y + (height * 0.48f)),
                C(new Vector4(1f, 1f, 1f, 0.055f * strength)),
                rounding,
                ImDrawFlags.RoundCornersTop);
        }

        /// <summary>Gloss over whatever ImGui item was just submitted.</summary>
        public static void GlossLastItem(float rounding = -1f, float strength = -1f) =>
            Gloss(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                rounding < 0f ? P.Rounding : rounding, strength);

        /// <summary>
        /// Full surface treatment for a rect: shadow, vertical gradient, border and
        /// bevel. Used by <see cref="BeginPanel"/> and available for custom regions.
        /// </summary>
        public static void Surface(
            Vector2 min, Vector2 max, Vector4 baseColour, float rounding,
            bool shadow = true, bool border = true, bool bevel = true)
        {
            if (P.Vanilla)
            {
                // Nothing at all: in vanilla the child window draws its own stock
                // background, so painting one here would just be a wasted fill.
                return;
            }

            if (P.Glass)
            {
                GlassSurface(min, max, baseColour, shadow);
                return;
            }

            if (shadow) DropShadow(min, max);

            var g = P.SurfaceGradient;
            GradientRect(min, max,
                UiPalette.Shade(baseColour, g), UiPalette.Shade(baseColour, -g), rounding);

            NoiseOverlay(min, max, rounding);

            if (border)
                ImGui.GetWindowDrawList().AddRect(min, max, C(P.Border), rounding, 0, 1f);

            if (bevel) Bevel(min, max, rounding);
        }

        /// <summary>
        /// Frosted-glass surface: translucent tinted fill, bright hairline edge,
        /// sheen across the top.
        /// <para>
        /// Everything is drawn as single fills rather than graded bands, because
        /// translucent bands compound their alpha where they overlap. The vertical
        /// falloff comes from stacking two differently sized translucent rects, which
        /// only ever doubles once and reads as a soft gradient.
        /// </para>
        /// </summary>
        public static void GlassSurface(Vector2 min, Vector2 max, Vector4 tintSource, bool shadow = true)
        {
            var draw = ImGui.GetWindowDrawList();
            var rounding = P.GlassRounding;

            if (shadow) DropShadow(min, max, 14f, 0.5f);

            // Body: the surface colour at partial opacity, so what is behind shows
            // through, plus a light tint for the frosted cast.
            draw.AddRectFilled(min, max, C(tintSource with { W = P.GlassOpacity }), rounding);
            draw.AddRectFilled(min, max, C(P.GlassTint), rounding);

            // Upper half caught by the light.
            var height = max.Y - min.Y;
            if (height > 4f)
            {
                draw.AddRectFilled(
                    min,
                    new Vector2(max.X, min.Y + (height * 0.5f)),
                    C(UiPalette.Fade(P.GlassTint, 0.9f)),
                    rounding,
                    ImDrawFlags.RoundCornersTop);
            }

            // The bright edge. This is the signature of the look.
            draw.AddRect(min, max, C(P.GlassBorder), rounding, 0, P.GlassBorderWidth);

            // A brighter top arc and darker bottom sells the curvature.
            var inset = rounding * 0.6f;
            draw.AddRectFilled(
                new Vector2(min.X + inset, min.Y + P.GlassBorderWidth),
                new Vector2(max.X - inset, min.Y + P.GlassBorderWidth + 1f),
                C(new Vector4(1f, 1f, 1f, 0.16f)));
            draw.AddRectFilled(
                new Vector2(min.X + inset, max.Y - P.GlassBorderWidth - 1f),
                new Vector2(max.X - inset, max.Y - P.GlassBorderWidth),
                C(new Vector4(0f, 0f, 0f, 0.16f)));

            Gloss(min, max, rounding, P.GlossStrength * 0.9f);
        }

        /// <summary>
        /// Soft drop shadow behind a rectangle.
        /// <para>
        /// Built from filled rounded rects stacked largest to smallest with a
        /// quadratic alpha falloff, so the layers accumulate into a gradient. An
        /// earlier version used <c>AddRect</c> outlines, which read as visible
        /// concentric rings rather than a shadow.
        /// </para>
        /// <para>
        /// Draw this <b>before</b> whatever sits on top of it. For a true gaussian
        /// falloff you would need a 9-sliced shadow sprite, but at these radii the
        /// difference is not worth an asset.
        /// </para>
        /// </summary>
        public static void DropShadow(
            Vector2 min, Vector2 max, float spread = 9f, float strength = 0.55f, int steps = 9)
        {
            if (P.Vanilla) return;
            if (steps < 1 || spread <= 0f) return;

            if (ShadowTexture.HasValue)
            {
                DropShadowSprite(ShadowTexture.Value, min, max, spread * 1.6f, strength);
                return;
            }

            var draw = ImGui.GetWindowDrawList();

            for (var i = steps; i >= 1; i--)
            {
                var t = i / (float)steps;          // 1 at the outermost ring
                var grow = spread * t;
                var falloff = (1f - t) * (1f - t); // quadratic: tight near the edge
                var alpha = strength * falloff / steps * 3.2f;

                draw.AddRectFilled(
                    new Vector2(min.X - grow, min.Y - grow),
                    new Vector2(max.X + grow, max.Y + grow),
                    C(new Vector4(0f, 0f, 0f, alpha)),
                    P.Rounding + grow);
            }
        }

        /// <summary>
        /// Draws a sprite as a 9-slice: the corners keep their size while the edges
        /// and centre stretch, so one small texture fits any rectangle without
        /// distorting its detail.
        /// </summary>
        /// <param name="corner">Corner size on screen, in pixels.</param>
        /// <param name="uvInset">Where the corners end in the source, 0 to 0.5.</param>
        /// <param name="drawCentre">
        /// Leave false for shadows and frames. Drawing the middle would darken or
        /// tint whatever the sprite is meant to sit around.
        /// </param>
        public static void NineSlice(
            ImTextureID texture, Vector2 min, Vector2 max,
            float corner, float uvInset, uint tint, bool drawCentre = false)
        {
            if (max.X - min.X <= 0f || max.Y - min.Y <= 0f) return;

            var draw = ImGui.GetWindowDrawList();

            // Corners cannot overlap, or opposite edges invert and the sprite tears.
            corner = MathF.Min(corner, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
            var uv = Math.Clamp(uvInset, 0.02f, 0.48f);

            Span<float> xs = [min.X, min.X + corner, max.X - corner, max.X];
            Span<float> ys = [min.Y, min.Y + corner, max.Y - corner, max.Y];
            Span<float> us = [0f, uv, 1f - uv, 1f];
            Span<float> vs = [0f, uv, 1f - uv, 1f];

            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    if (!drawCentre && row == 1 && col == 1) continue;

                    draw.AddImage(
                        texture,
                        new Vector2(xs[col], ys[row]),
                        new Vector2(xs[col + 1], ys[row + 1]),
                        new Vector2(us[col], vs[row]),
                        new Vector2(us[col + 1], vs[row + 1]),
                        tint);
                }
            }
        }

        private static void DropShadowSprite(
            ImTextureID texture, Vector2 min, Vector2 max, float spread, float strength)
        {
            NineSlice(
                texture,
                new Vector2(min.X - spread, min.Y - spread),
                new Vector2(max.X + spread, max.Y + spread),
                spread,
                ShadowTextureInset / 256f,
                C(new Vector4(1f, 1f, 1f, Math.Clamp(strength, 0f, 1f))));
        }

        /// <summary>
        /// A glowing frame around a rect, drawn from a 9-sliced sprite. Falls back to
        /// a plain rounded outline when no texture is supplied.
        /// </summary>
        public static void GlowFrame(
            ImTextureID? texture, Vector2 min, Vector2 max, Vector4 colour,
            float corner = 16f, float uvInset = 0.25f)
        {
            if (!texture.HasValue)
            {
                ImGui.GetWindowDrawList().AddRect(min, max, C(colour), P.Rounding, 0, 2f);
                return;
            }

            // Assumes a white sprite: ImGui tints by multiplying, so a coloured
            // source can only darken or shift hue, never take an arbitrary accent.
            NineSlice(texture.Value, min, max, corner, uvInset, C(colour));
        }

        /// <summary>
        /// Tiles <see cref="NoiseTexture"/> across a rect.
        /// <para>
        /// Tiled with explicit draws rather than UVs beyond 1, because ImGui's
        /// sampler clamps rather than repeats, so a single stretched quad would
        /// smear the edge pixels instead of repeating the grain.
        /// </para>
        /// </summary>
        public static void NoiseOverlay(Vector2 min, Vector2 max, float rounding = 0f)
        {
            if (P.Vanilla) return;
            if (!NoiseTexture.HasValue || NoiseStrength <= 0f) return;
            var texture = NoiseTexture.Value;

            var width = max.X - min.X;
            var height = max.Y - min.Y;
            if (width <= 0 || height <= 0) return;

            var tile = MathF.Max(16f, NoiseTileSize);
            var cols = (int)MathF.Ceiling(width / tile);
            var rows = (int)MathF.Ceiling(height / tile);

            // Guard against a pathological number of draws on a huge surface.
            if (cols * rows > 400) return;

            var draw = ImGui.GetWindowDrawList();
            var tint = C(new Vector4(1f, 1f, 1f, Math.Clamp(NoiseStrength, 0f, 1f)));

            if (rounding > 0f) draw.PushClipRect(min, max, true);

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var p0 = new Vector2(min.X + (col * tile), min.Y + (row * tile));
                    var p1 = new Vector2(MathF.Min(p0.X + tile, max.X), MathF.Min(p0.Y + tile, max.Y));

                    // Partial tiles need their UVs trimmed or the grain stretches.
                    var u = (p1.X - p0.X) / tile;
                    var v = (p1.Y - p0.Y) / tile;

                    draw.AddImage(texture, p0, p1, Vector2.Zero, new Vector2(u, v), tint);
                }
            }

            if (rounding > 0f) draw.PopClipRect();
        }

        /// <summary>
        /// Draws a shadow behind the region the next child window will occupy, then
        /// leaves the cursor untouched so you can call BeginChild immediately.
        /// </summary>
        public static void ShadowForNext(Vector2 size, float spread = 9f, float strength = 0.55f)
        {
            var min = ImGui.GetCursorScreenPos();
            var avail = ImGui.GetContentRegionAvail();

            var width = size.X > 0 ? size.X : avail.X + size.X;
            var height = size.Y > 0 ? size.Y : avail.Y + size.Y;
            if (width <= 0 || height <= 0) return;

            DropShadow(min, new Vector2(min.X + width, min.Y + height), spread, strength);
        }

        /// <summary>
        /// A raised, shadowed, rounded panel. Pair with <see cref="EndPanel"/>.
        /// <para>
        /// Returns whatever BeginChild returned so you can skip drawing content, but
        /// <see cref="EndPanel"/> must be called <b>unconditionally</b>, exactly like
        /// ImGui's own EndChild.
        /// </para>
        /// </summary>
        public static bool BeginPanel(string id, Vector2 size, bool shadow = true, Vector4? background = null)
        {
            // Remember whether this particular panel pushed anything, rather than
            // re-reading the palette in EndPanel. Toggling vanilla from a settings
            // window happens *between* Begin and End, so asking twice could pop a
            // different number of styles than were pushed and corrupt the stack.
            if (P.Vanilla)
            {
                panelStyled.Push(false);

                // Stock child with a border, which is also what gives it padding.
                return ImGui.BeginChild(id, size, true);
            }

            panelStyled.Push(true);

            var min = ImGui.GetCursorScreenPos();
            var avail = ImGui.GetContentRegionAvail();
            var width = size.X > 0 ? size.X : avail.X + size.X;
            var height = size.Y > 0 ? size.Y : avail.Y + size.Y;

            // Paint the surface ourselves so it can be graded and bevelled; a child
            // background is a single flat colour and would cover this.
            if (width > 0 && height > 0)
            {
                Surface(min, new Vector2(min.X + width, min.Y + height),
                    background ?? P.PanelRaised, P.Rounding, shadow);
            }

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, P.Glass ? P.GlassRounding : P.Rounding);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, P.PanelPadding);

            // border: true is deliberate. A child with border false is forced to
            // zero padding by ImGui, which makes text hug the edges. The border
            // itself is drawn transparent because Surface already painted one.
            return ImGui.BeginChild(id, size, true);
        }

        public static void EndPanel()
        {
            ImGui.EndChild();

            if (panelStyled.Count == 0 || !panelStyled.Pop()) return;

            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }

        /// <summary>Whether each open panel pushed styles. See BeginPanel.</summary>
        private static readonly Stack<bool> panelStyled = new();
    }
}

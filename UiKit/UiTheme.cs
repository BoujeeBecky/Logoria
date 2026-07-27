using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace UiKit
{
    /// <summary>
    /// Applies the palette to ImGui's global style.
    /// <para>
    /// Push in a window's PreDraw and dispose in PostDraw. That is the only point
    /// the window frame itself (background, rounding, title bar) can be styled,
    /// because Dalamud's WindowSystem calls Begin before Draw.
    /// </para>
    /// </summary>
    public static class UiTheme
    {
        public static UiPalette P => UiPalette.Current;

        public static uint U32(Vector4 colour) => ImGui.GetColorU32(colour);

        public static Vector4 Fade(Vector4 colour, float alpha) => UiPalette.Fade(colour, alpha);

        public static StyleScope Push()
        {
            var p = P;
            var scope = new StyleScope();

            // Vanilla pushes nothing at all, so windows render exactly as stock
            // ImGui. The scope is still returned so callers keep their using block
            // and disposal stays symmetric.
            if (p.Vanilla) return scope;

            // Glass needs something behind it to be glass over, so the window itself
            // has to let the game through and the radii grow.
            var rounding = p.Glass ? p.GlassRounding : p.Rounding;
            var windowBg = p.Glass ? p.Backdrop with { W = p.Backdrop.W * 0.55f } : p.Backdrop;

            scope.Var(ImGuiStyleVar.WindowRounding, p.Glass ? p.GlassRounding : p.WindowRounding);
            scope.Var(ImGuiStyleVar.ChildRounding, rounding);
            scope.Var(ImGuiStyleVar.FrameRounding, rounding);
            scope.Var(ImGuiStyleVar.PopupRounding, rounding);
            scope.Var(ImGuiStyleVar.ScrollbarRounding, p.Rounding);
            scope.Var(ImGuiStyleVar.GrabRounding, p.Rounding);
            scope.Var(ImGuiStyleVar.TabRounding, p.Rounding);
            scope.Var(ImGuiStyleVar.FrameBorderSize, 1f);
            scope.Var(ImGuiStyleVar.WindowPadding, p.WindowPadding);
            scope.Var(ImGuiStyleVar.FramePadding, p.FramePadding);
            scope.Var(ImGuiStyleVar.ItemSpacing, p.ItemSpacing);
            scope.Var(ImGuiStyleVar.ItemInnerSpacing, p.ItemInnerSpacing);
            scope.Var(ImGuiStyleVar.CellPadding, p.CellPadding);
            scope.Var(ImGuiStyleVar.ScrollbarSize, p.ScrollbarSize);

            scope.Colour(ImGuiCol.WindowBg, windowBg);
            scope.Colour(ImGuiCol.ChildBg, p.Glass ? p.Panel with { W = p.GlassOpacity } : p.Panel);
            scope.Colour(ImGuiCol.PopupBg, p.Panel);
            scope.Colour(ImGuiCol.Border, p.Glass ? p.GlassBorder : p.Border);
            scope.Colour(ImGuiCol.Text, p.TextPrimary);
            scope.Colour(ImGuiCol.TextDisabled, p.TextFaint);

            scope.Colour(ImGuiCol.FrameBg,
                p.Glass ? p.PanelRaised with { W = p.GlassOpacity + 0.15f } : p.PanelRaised);
            scope.Colour(ImGuiCol.FrameBgHovered, p.PanelHover);
            scope.Colour(ImGuiCol.FrameBgActive, p.PanelHover);

            scope.Colour(ImGuiCol.Button,
                p.Glass ? p.PanelRaised with { W = p.GlassOpacity + 0.20f } : p.PanelRaised);
            scope.Colour(ImGuiCol.ButtonHovered, p.PanelHover);
            scope.Colour(ImGuiCol.ButtonActive, p.AccentDim);

            scope.Colour(ImGuiCol.Header, Fade(p.Accent, 0.20f));
            scope.Colour(ImGuiCol.HeaderHovered, Fade(p.Accent, 0.30f));
            scope.Colour(ImGuiCol.HeaderActive, Fade(p.Accent, 0.40f));

            scope.Colour(ImGuiCol.TitleBg, p.Panel);
            scope.Colour(ImGuiCol.TitleBgActive, p.PanelRaised);
            scope.Colour(ImGuiCol.TitleBgCollapsed, p.Panel);

            scope.Colour(ImGuiCol.TableHeaderBg, p.PanelRaised);
            scope.Colour(ImGuiCol.TableBorderStrong, p.Border);
            scope.Colour(ImGuiCol.TableBorderLight, Fade(p.Border, 0.55f));
            scope.Colour(ImGuiCol.TableRowBg, Fade(p.Panel, 0f));
            scope.Colour(ImGuiCol.TableRowBgAlt, Fade(p.PanelRaised, 0.35f));

            scope.Colour(ImGuiCol.PlotHistogram, p.Accent);
            scope.Colour(ImGuiCol.CheckMark, p.Accent);
            scope.Colour(ImGuiCol.SliderGrab, p.AccentDim);
            scope.Colour(ImGuiCol.SliderGrabActive, p.Accent);
            scope.Colour(ImGuiCol.Separator, p.Border);
            scope.Colour(ImGuiCol.ScrollbarBg, Fade(p.Panel, 0.4f));
            scope.Colour(ImGuiCol.ScrollbarGrab, p.PanelRaised);
            scope.Colour(ImGuiCol.ScrollbarGrabHovered, p.PanelHover);
            scope.Colour(ImGuiCol.ScrollbarGrabActive, p.AccentDim);

            // Curve smoothness has no PushStyleVar, so it has to be assigned on the
            // shared style and put back afterwards. Leaving it changed would alter
            // how every other plugin's rounded corners tessellate.
            scope.CaptureCircleTessellation(p.CircleTessellationMaxError);

            return scope;
        }

        /// <summary>
        /// Counts pushes so they always unwind exactly. Leaking a push corrupts the
        /// style stack for every plugin drawing after you, which is the single most
        /// common way ImGui theming breaks.
        /// </summary>
        public sealed class StyleScope : IDisposable
        {
            private int vars;
            private int colours;
            private float? previousCircleTessellation;

            /// <summary>
            /// Sets a style field that ImGui offers no push/pop for, remembering the
            /// old value so Dispose can restore it.
            /// </summary>
            public void CaptureCircleTessellation(float value)
            {
                var style = ImGui.GetStyle();
                previousCircleTessellation = style.CircleTessellationMaxError;
                style.CircleTessellationMaxError = value;
            }

            public void Var(ImGuiStyleVar id, float value)
            {
                ImGui.PushStyleVar(id, value);
                vars++;
            }

            public void Var(ImGuiStyleVar id, Vector2 value)
            {
                ImGui.PushStyleVar(id, value);
                vars++;
            }

            public void Colour(ImGuiCol id, Vector4 value)
            {
                ImGui.PushStyleColor(id, value);
                colours++;
            }

            public void Dispose()
            {
                if (colours > 0) ImGui.PopStyleColor(colours);
                if (vars > 0) ImGui.PopStyleVar(vars);
                colours = 0;
                vars = 0;

                if (previousCircleTessellation.HasValue)
                {
                    ImGui.GetStyle().CircleTessellationMaxError = previousCircleTessellation.Value;
                    previousCircleTessellation = null;
                }
            }
        }
    }
}

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace UiKit
{
    /// <summary>
    /// Window base that applies the kit's theme to the window frame itself.
    /// <para>
    /// Styling has to happen in PreDraw/PostDraw rather than inside Draw, because
    /// Dalamud's WindowSystem calls ImGui.Begin before Draw. Anything pushed inside
    /// Draw cannot affect the background, rounding or title bar.
    /// </para>
    /// <para>
    /// If you override PreDraw or PostDraw, call the base implementation.
    /// </para>
    /// </summary>
    public abstract class ThemedWindow : Window
    {
        private UiTheme.StyleScope? scope;

        protected ThemedWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
            : base(name, flags)
        {
        }

        public override void PreDraw()
        {
            scope = UiTheme.Push();
        }

        public override void PostDraw()
        {
            scope?.Dispose();
            scope = null;
        }
    }
}

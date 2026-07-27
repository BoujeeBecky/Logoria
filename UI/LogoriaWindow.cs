using Dalamud.Bindings.ImGui;
using UiKit;

namespace Logoria.UI
{
    /// <summary>
    /// Base for every Logoria window. All the theming behaviour lives in the shared
    /// kit's <see cref="ThemedWindow"/>; this exists so Logoria's windows have one
    /// local base to hang plugin-specific behaviour off later.
    /// </summary>
    public abstract class LogoriaWindow : ThemedWindow
    {
        private System.IDisposable? fontScope;

        protected LogoriaWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
            : base(name, flags)
        {
        }

        /// <summary>Set once at startup so every window can reach the font.</summary>
        public static Services.FontService? Fonts { get; set; }

        public override void PreDraw()
        {
            base.PreDraw();

            // Alongside the theme, for the same reason: the font has to be active
            // before ImGui.Begin so the title bar and sizing use it too.
            fontScope = Fonts?.Push();
        }

        public override void PostDraw()
        {
            fontScope?.Dispose();
            fontScope = null;

            base.PostDraw();
        }
    }
}

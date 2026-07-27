using System;
using System.IO;
using System.Linq;
using Dalamud.Interface.ManagedFontAtlas;

namespace Logoria.Services
{
    /// <summary>
    /// Optional custom UI font.
    /// <para>
    /// Drop a <c>.ttf</c> or <c>.otf</c> into <c>assets/fonts/</c> and every Logoria
    /// window uses it. With no file present this does nothing at all: no handle is
    /// created, no atlas rebuild is triggered, and the UI keeps Dalamud's default.
    /// </para>
    /// <para>
    /// Deliberately fail-quiet. A font that will not load must not take the plugin
    /// down with it, so any failure falls back to the default and logs once.
    /// </para>
    /// </summary>
    public sealed class FontService : IDisposable
    {
        private IFontHandle? handle;

        public FontService(float sizePx)
        {
            TryLoad(sizePx);
        }

        /// <summary>Filename of the loaded font, or null when using the default.</summary>
        public string? LoadedName { get; private set; }

        public bool IsCustom => handle != null;

        private void TryLoad(float sizePx)
        {
            try
            {
                var pluginDirectory = Service.PluginInterface.AssemblyLocation.Directory?.FullName
                                      ?? AppContext.BaseDirectory;
                var fontDirectory = Path.Combine(pluginDirectory, "assets", "fonts");

                if (!Directory.Exists(fontDirectory)) return;

                var file = Directory.EnumerateFiles(fontDirectory, "*.*")
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                                || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (file == null) return;

                handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                    e => e.OnPreBuild(tk => tk.AddFontFromFile(
                        file, new SafeFontConfig { SizePx = Math.Clamp(sizePx, 8f, 48f) })));

                LoadedName = Path.GetFileName(file);
                Service.Log.Information($"Logoria: using custom font {LoadedName} at {sizePx}px.");
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Logoria: custom font failed to load, using the default.");
                handle = null;
                LoadedName = null;
            }
        }

        /// <summary>
        /// Pushes the font for the caller's scope, or returns null when there is no
        /// custom font. Callers must dispose whatever they get back.
        /// </summary>
        public IDisposable? Push()
        {
            try
            {
                // Pushing before the atlas has finished building throws, and the
                // build is asynchronous, so the first frames legitimately have none.
                return handle is { Available: true } ? handle.Push() : null;
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Logoria: failed to push the custom font.");
                return null;
            }
        }

        public void Dispose()
        {
            handle?.Dispose();
            handle = null;
        }
    }
}

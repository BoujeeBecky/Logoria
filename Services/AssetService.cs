using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.Textures.TextureWraps;

namespace Logoria.Services
{
    /// <summary>
    /// Loads Logoria's own art from the plugin folder.
    /// <para>
    /// Textures are fetched through <c>ITextureProvider</c>, which loads
    /// asynchronously and caches for us, so a miss simply means "not ready yet"
    /// rather than an error. Every caller must cope with a null wrap and fall back
    /// to the procedural drawing.
    /// </para>
    /// </summary>
    public class AssetService
    {
        private readonly string assetDirectory;
        private readonly HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);

        public AssetService()
        {
            var pluginDirectory = Service.PluginInterface.AssemblyLocation.Directory?.FullName
                                  ?? AppContext.BaseDirectory;
            assetDirectory = Path.Combine(pluginDirectory, "assets");

            if (!Directory.Exists(assetDirectory))
                Service.Log.Warning($"Logoria: asset folder not found at {assetDirectory}");
        }

        /// <summary>The texture, or null while it loads or if the file is absent.</summary>
        public IDalamudTextureWrap? Get(string fileName)
        {
            if (missing.Contains(fileName)) return null;

            try
            {
                var path = Path.Combine(assetDirectory, fileName);
                if (!File.Exists(path))
                {
                    // Log once. This runs every frame, so repeating would flood the log.
                    missing.Add(fileName);
                    Service.Log.Warning($"Logoria: missing asset {fileName}");
                    return null;
                }

                return Service.TextureProvider.GetFromFile(path).GetWrapOrDefault();
            }
            catch (Exception ex)
            {
                missing.Add(fileName);
                Service.Log.Error(ex, $"Logoria: failed to load asset {fileName}");
                return null;
            }
        }

        // No Logo property: logo.png is not shipped, since nothing in the UI draws
        // the full wordmark. It lives in the repo for the README and plugin listing.
        public IDalamudTextureWrap? LogoMark => Get("logo_mark.png");
        public IDalamudTextureWrap? Banner => Get("banner.jpg");
        public IDalamudTextureWrap? Noise => Get("noise.png");
        public IDalamudTextureWrap? Shadow => Get("shadow.png");
        public IDalamudTextureWrap? Watermark => Get("watermark.png");
        public IDalamudTextureWrap? GlowBorder => Get("glowing_border.png");

        // No NavHighlight property: the nav selection is drawn procedurally, so
        // nav_highlight.png is neither shipped nor loaded. Asking for it here would
        // log a missing-asset warning for a file we deliberately dropped.
    }
}

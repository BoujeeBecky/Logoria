using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Logoria.Data;
using Logoria.Services;

namespace Logoria.UI
{
    public static class UIHelpers
    {
        // Deliberately no unicode glyphs anywhere in this plugin. The default
        // Dalamud font has no coverage for the likes of U+26A1 or U+25CF, so they
        // render as tofu boxes. Status marks are drawn as shapes instead.
        public static readonly Vector4 Gold = new(0.92f, 0.78f, 0.35f, 1.00f);
        public static readonly Vector4 ObtainedGreen = new(0.35f, 0.85f, 0.45f, 1.00f);
        public static readonly Vector4 ReadyCyan = new(0.35f, 0.85f, 0.95f, 1.00f);
        public static readonly Vector4 UnknownGrey = new(0.45f, 0.45f, 0.50f, 1.00f);
        public static readonly Vector4 Dim = new(0.62f, 0.62f, 0.66f, 1.00f);
        public static readonly Vector4 ShortMneme = new(0.85f, 0.42f, 0.42f, 1.00f);

        public static Vector4 ColorFor(DexState state) => state switch
        {
            DexState.Obtained => ObtainedGreen,
            DexState.Ready => ReadyCyan,
            _ => UnknownGrey,
        };

        public static string LabelFor(DexState state) => state switch
        {
            DexState.Obtained => "Obtained",
            DexState.Ready => "READY",
            _ => "Unknown",
        };

        public static void DrawActionIcon(LogosAction action, float size = 32f) =>
            DrawGameIcon(action.IconId, size);

        /// <summary>
        /// The mneme's own item icon. Recipes read far faster with the art beside
        /// the name, and it costs nothing: the ids are already in the database and
        /// the art is the game's own.
        /// </summary>
        public static void DrawMnemeIcon(uint itemId, float size = 18f)
        {
            var mneme = MnemeDatabase.ById(itemId);
            if (mneme == null)
            {
                ImGui.Dummy(new Vector2(size, size));
                return;
            }

            DrawGameIcon(mneme.IconId, size);
        }

        /// <summary>
        /// Draws a game icon, reserving the space either way so a texture that has
        /// not finished loading cannot shift the layout around it.
        /// </summary>
        private static void DrawGameIcon(uint iconId, float size)
        {
            try
            {
                var lookup = new GameIconLookup(iconId, hiRes: false);
                if (Service.TextureProvider.TryGetFromGameIcon(lookup, out var tex) && tex != null)
                {
                    var wrap = tex.GetWrapOrDefault();
                    if (wrap != null)
                    {
                        ImGui.Image(wrap.Handle, new Vector2(size, size));
                        return;
                    }
                }
            }
            catch
            {
                // Fall through to the placeholder.
            }

            ImGui.Dummy(new Vector2(size, size));
        }

        // DrawDexBadge lived here until the dex table moved to Theme.StatusDot.
        // Removed rather than kept "just in case": an unused drawing helper is a
        // second place for the status colours to drift out of sync.

        /// <summary>Renders one recipe as "2x Cure L (3)" lines, coloured by shortfall.</summary>
        public static void DrawRecipe(
            LogosRecipe recipe,
            MnemeInventoryService inventory,
            GameTextService text,
            bool compact = false)
        {
            foreach (var slot in recipe.Slots)
            {
                var have = inventory.CountOf(slot.ItemId);
                var enough = have >= slot.Count;
                var color = enough ? ObtainedGreen : ShortMneme;

                var iconSize = ImGui.GetTextLineHeight();
                DrawMnemeIcon(slot.ItemId, iconSize);
                ImGui.SameLine(0f, 5f);

                Theme.TextColored(color, $"{slot.Count}x {text.MnemeName(slot.ItemId)}");

                if (compact) continue;

                // Counts in the monospace face so they line up down the column.
                ImGui.SameLine(0f, 6f);
                Theme.TextMono($"{have}/{slot.Count}", enough ? Dim : ShortMneme);
            }
        }

        /// <summary>Success odds fall as a recipe uses more mnemes.</summary>
        public static string OddsHint(LogosRecipe recipe) => recipe.TotalMnemes switch
        {
            1 => "1 mneme, best odds",
            2 => "2 mnemes, lower odds",
            _ => $"{recipe.TotalMnemes} mnemes, lowest odds",
        };

        // Forwards to the kit rather than keeping a second copy of the same six
        // lines. They had already drifted once: the kit's version was skipping the
        // text shadow.
        public static void CentredText(string label, Vector4 color) =>
            UiKit.Ui.CentredText(label, color);
    }
}

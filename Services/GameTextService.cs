using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;
using Logoria.Data;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Logoria.Services
{
    /// <summary>
    /// Resolves display text from the game's own sheets, so the dex is in the
    /// player's client language without us shipping any translations.
    /// Results are cached because sheet lookups are not free per-frame per-row.
    /// </summary>
    public class GameTextService
    {
        private readonly Dictionary<uint, string> actionNames = new();
        private readonly Dictionary<uint, string> actionDescriptions = new();
        private readonly Dictionary<uint, string> itemNames = new();

        public string ActionName(LogosAction action)
        {
            if (actionNames.TryGetValue(action.ActionId, out var cached)) return cached;

            var name = action.FallbackName;
            try
            {
                var row = Service.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(action.ActionId);
                if (row.HasValue)
                {
                    var text = row.Value.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(text)) name = text;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Debug(ex, $"Name lookup failed for action {action.ActionId}.");
            }

            actionNames[action.ActionId] = name;
            return name;
        }

        public string ActionDescription(LogosAction action)
        {
            if (actionDescriptions.TryGetValue(action.ActionId, out var cached)) return cached;

            var description = string.Empty;
            try
            {
                var row = Service.DataManager.GetExcelSheet<ActionTransient>()?.GetRowOrDefault(action.ActionId);
                if (row.HasValue) description = row.Value.Description.ExtractText();
            }
            catch (Exception ex)
            {
                Service.Log.Debug(ex, $"Description lookup failed for action {action.ActionId}.");
            }

            actionDescriptions[action.ActionId] = description;
            return description;
        }

        public string MnemeName(uint itemId)
        {
            if (itemNames.TryGetValue(itemId, out var cached)) return cached;

            var name = MnemeDatabase.ById(itemId)?.FallbackName ?? $"Item #{itemId}";
            try
            {
                var row = Service.DataManager.GetExcelSheet<Item>()?.GetRowOrDefault(itemId);
                if (row.HasValue)
                {
                    var text = row.Value.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(text)) name = text;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Debug(ex, $"Name lookup failed for item {itemId}.");
            }

            itemNames[itemId] = name;
            return name;
        }
    }
}

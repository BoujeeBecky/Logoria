using System;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>
    /// Opens the in-game map on a farming spot.
    /// <para>
    /// Uses <see cref="MapLinkPayload"/> rather than the raw world-position overload
    /// of OpenMapWithMapLink. The payload takes map coordinates directly, which is
    /// what the wikis publish, so there is no map-to-world conversion to get wrong.
    /// </para>
    /// </summary>
    public class MapService
    {
        /// <summary>
        /// Places a marker and opens the map. Returns false if the link could not be
        /// built, which is not worth surfacing beyond the log: it means bad data, not
        /// anything the player did.
        /// </summary>
        public bool OpenMap(MobLocation location)
        {
            try
            {
                var zone = EurekaLocations.ZoneInfo(location.Zone);

                var link = new MapLinkPayload(
                    zone.TerritoryId,
                    zone.MapId,
                    location.X,
                    location.Y);

                Service.GameGui.OpenMapWithMapLink(link);
                return true;
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Could not open the map for {location.Mob}.");
                return false;
            }
        }
    }
}

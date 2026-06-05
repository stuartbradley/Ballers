namespace Ballers.API.Models
{
    // Single-row table holding league-wide toggles.
    public class LeagueSetting
    {
        public int Id { get; set; }

        // When true, managers can no longer add new players to their squad.
        public bool PlayersLocked { get; set; }
    }
}

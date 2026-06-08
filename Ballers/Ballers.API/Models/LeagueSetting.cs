namespace Ballers.API.Models
{
    // Single-row table holding league-wide toggles.
    public class LeagueSetting
    {
        public int Id { get; set; }

        // When true, managers can no longer add new players to their squad.
        public bool PlayersLocked { get; set; }

        // When true, all fixtures are locked for editing (no stats/squad/schedule changes).
        public bool FixturesLocked { get; set; }

        // When true, fixtures are hidden from the public site (schedule pages show nothing).
        public bool FixturesHidden { get; set; }
    }
}

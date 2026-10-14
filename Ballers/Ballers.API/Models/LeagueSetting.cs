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

        // When false, players can no longer sign themselves up. A kill switch for
        // the one anonymous write endpoint on the site.
        public bool PlayerSignupEnabled { get; set; } = true;

        // --- Card points economy -------------------------------------------------
        // Editable rather than hardcoded because the right numbers aren't knowable
        // until there's a season of real data behind them.

        // Points handed to a player when their account is approved, to spend across
        // the six attributes however they like.
        public int SignupPointsGrant { get; set; } = 80;

        // No single attribute may go above this.
        public int AttributeCap { get; set; } = 99;

        public int PointsPerAppearance { get; set; } = 1;
        public int PointsPerWin { get; set; } = 1;
        public int PointsPerGoal { get; set; } = 3;
        public int PointsPerAssist { get; set; } = 2;
        public int PointsPerCleanSheet { get; set; } = 3;
        public int PointsPerMotm { get; set; } = 5;

        // Overall rating at which a card moves up to silver, then gold. Low on
        // purpose: 80 points across six attributes is an overall of about 13, so
        // FIFA-like thresholds would leave every card bronze for years.
        public int SilverOverallThreshold { get; set; } = 20;
        public int GoldOverallThreshold { get; set; } = 35;

        // Last time the points payout ran to completion. Lets the background
        // service tell a missed window from an already-done one after a restart.
        public DateTime? LastPointsRunAt { get; set; }
    }
}

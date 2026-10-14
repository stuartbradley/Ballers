namespace Ballers.API.Models
{
    /// <summary>
    /// One row per player per fixture recording the points that fixture earned them.
    /// The unique index on (PlayerId, FixtureId) is what makes the payout run safe to
    /// repeat: a fixture already in the ledger is never paid twice, so the job can be
    /// re-run by hand, after a crash, or on a late-entered result without harm.
    /// </summary>
    public class PlayerPointsAward
    {
        public int Id { get; set; }

        public int PlayerId { get; set; }
        public Player? Player { get; set; }

        public int FixtureId { get; set; }
        public Fixture? Fixture { get; set; }

        public DateTime AwardedAt { get; set; }

        // Total credited, and the breakdown behind it so a player can be shown why.
        public int Points { get; set; }
        public int AppearancePoints { get; set; }
        public int WinPoints { get; set; }
        public int GoalPoints { get; set; }
        public int AssistPoints { get; set; }
        public int CleanSheetPoints { get; set; }
        public int MotmPoints { get; set; }
    }
}

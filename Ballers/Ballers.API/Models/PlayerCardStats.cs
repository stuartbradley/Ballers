namespace Ballers.API.Models
{
    /// <summary>
    /// A player's card attributes and their unspent points balance. Created when a
    /// player account is approved, so a roster row with no login has no row here and
    /// its card keeps rendering the old way rather than showing a rating of zero.
    /// </summary>
    public class PlayerCardStats
    {
        public int Id { get; set; }

        public int PlayerId { get; set; }
        public Player? Player { get; set; }

        public int Finishing { get; set; }
        public int Tackling { get; set; }
        public int Passing { get; set; }
        public int Dribbling { get; set; }
        public int Pace { get; set; }
        public int Strength { get; set; }

        // Points earned but not yet put into an attribute. Visible to the player
        // who owns the card and to admins, never on the public card.
        public int UnspentPoints { get; set; }

        // Lifetime total ever earned, including the signup grant and everything
        // already spent. Kept for display and for auditing a disputed balance.
        public int TotalPointsEarned { get; set; }
    }
}

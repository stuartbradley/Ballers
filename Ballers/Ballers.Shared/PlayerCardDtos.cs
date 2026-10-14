namespace Ballers.Shared
{
    /// <summary>
    /// The six card attributes and the rating they add up to. Safe to show to
    /// anyone — it carries no points balance.
    /// </summary>
    public class PlayerAttributesDto
    {
        public int Finishing { get; set; }
        public int Tackling { get; set; }
        public int Passing { get; set; }
        public int Dribbling { get; set; }
        public int Pace { get; set; }
        public int Strength { get; set; }

        public int Overall { get; set; }
    }

    /// <summary>
    /// A player's own view of their card, including the balance they have left to
    /// spend. Only ever returned to the player who owns it, or to an admin.
    /// </summary>
    public class MyPlayerCardDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "MID";
        public string? TeamName { get; set; }
        public string? ImageUrl { get; set; }

        public PlayerAttributesDto Attributes { get; set; } = new();

        public int UnspentPoints { get; set; }
        public int TotalPointsEarned { get; set; }

        // Sent so the allocation UI can enforce the same ceiling the server does
        // instead of hardcoding it.
        public int AttributeCap { get; set; }

        // Where the card's overall stands among every active player with a card,
        // across the league and among players in the same position. Ties share
        // a rank (two joint-first, then third).
        public int OverallRank { get; set; }
        public int OverallOf { get; set; }
        public int PositionRank { get; set; }
        public int PositionOf { get; set; }
    }

    /// <summary>
    /// How many points to add to each attribute. Additions only — points cannot be
    /// taken back out of an attribute once spent.
    /// </summary>
    public class AllocatePointsRequest
    {
        public int Finishing { get; set; }
        public int Tackling { get; set; }
        public int Passing { get; set; }
        public int Dribbling { get; set; }
        public int Pace { get; set; }
        public int Strength { get; set; }

        public int Total => Finishing + Tackling + Passing + Dribbling + Pace + Strength;
    }

    /// <summary>
    /// The admin-editable points economy. Deliberately configurable: how generous
    /// these need to be only becomes clear after a season of real results.
    /// </summary>
    public class PointsSettingsDto
    {
        public bool PlayerSignupEnabled { get; set; }
        public int SignupPointsGrant { get; set; }
        public int AttributeCap { get; set; }
        public int PointsPerAppearance { get; set; }
        public int PointsPerWin { get; set; }
        public int PointsPerGoal { get; set; }
        public int PointsPerAssist { get; set; }
        public int PointsPerCleanSheet { get; set; }
        public int PointsPerMotm { get; set; }
        public int SilverOverallThreshold { get; set; }
        public int GoldOverallThreshold { get; set; }
        public DateTime? LastPointsRunAt { get; set; }
    }

    /// <summary>
    /// The overall ratings that lift a card to silver and gold. Public, unlike the
    /// rest of <see cref="PointsSettingsDto"/>: the home page explains the tiers
    /// to visitors who are not signed in.
    /// </summary>
    public class CardTierThresholdsDto
    {
        public int SilverOverallThreshold { get; set; }
        public int GoldOverallThreshold { get; set; }

        /// <summary>The most any one stat can reach, so a stat can be drawn as a share of it.</summary>
        public int AttributeCap { get; set; }
    }

    /// <summary>
    /// The home page's card of the week: whoever earned the most card points from
    /// the latest week's fixtures, with their season so far for the back of the card.
    /// Built only from what is already public on team pages and public cards.
    /// </summary>
    public class FeaturedCardDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "";
        public string? TeamName { get; set; }
        public string? ImageUrl { get; set; }
        public PlayerAttributesDto Attributes { get; set; } = new();
        public string Tier { get; set; } = "bronze";

        public int WeekPoints { get; set; }
        public DateTime WeekEnding { get; set; }

        public int? SeasonNumber { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int CleanSheets { get; set; }
        public int GoalPoints { get; set; }
        public int AssistPoints { get; set; }
        public int CleanSheetPoints { get; set; }
        public int SeasonPoints { get; set; }
    }

    /// <summary>One fixture's worth of earned points, for the player's history.</summary>
    public class PlayerPointsAwardDto
    {
        public int FixtureId { get; set; }
        public string? Opponent { get; set; }
        public DateTime? Kickoff { get; set; }
        public int Points { get; set; }
        public int AppearancePoints { get; set; }
        public int WinPoints { get; set; }
        public int GoalPoints { get; set; }
        public int AssistPoints { get; set; }
        public int CleanSheetPoints { get; set; }
        public int MotmPoints { get; set; }
    }
}

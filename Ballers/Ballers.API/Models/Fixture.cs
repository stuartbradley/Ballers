using Ballers.API.Events;

namespace Ballers.API.Models
{
    public class Fixture : EntityWithEvents
    {
        public int Id { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId {  get; set; }
        public DateTime? Kickoff { get; set; }
        public string? Location { get; set; } = "";
        public string? Postcode { get; set; }
        // A result only counts once both teams have submitted their stats. Until
        // then the fixture is not "played", so a side that submits first cannot
        // show up in the league table with a scoreline missing the opponent's goals.
        public bool IsPlayed { get; set; } = false;
        public bool HomeStatsSubmitted { get; set; } = false;
        public bool AwayStatsSubmitted { get; set; } = false;
        public int SeasonId {  get; set; }
        public Season? Season { get; set; }
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }
        public int MatchNumber { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? RefereeId { get; set; }
        public Referee? Referee { get; set; }

        public int? HomeCaptainId { get; set; }
        public Player? HomeCaptain { get; set; }
        public int? HomeViceCaptainId { get; set; }
        public Player? HomeViceCaptain { get; set; }
        public int? AwayCaptainId { get; set; }
        public Player? AwayCaptain { get; set; }
        public int? AwayViceCaptainId { get; set; }
        public Player? AwayViceCaptain { get; set; }

        public ICollection<FixturePlayerStat> FixturePlayerStats { get; set; } = new List<FixturePlayerStat>();

        // The referee names a man of the match for each team in the fixture stats,
        // then picks one of those two as the match's true man of the match. That
        // pick is admin-only and must never be exposed to players or managers.
        public int? TrueMotmPlayerId { get; set; }
        public Player? TrueMotmPlayer { get; set; }

        public bool IsKnockout { get; set; }
        public string? KnockoutTournament { get; set; }
        public string? KnockoutRound { get; set; }
        public int KnockoutSlot { get; set; }

        /// <summary>
        /// Records that a side has sent its stats; a null team means the whole
        /// fixture was submitted at once, which counts as both. The result is only
        /// final, and only counts towards the table, player stats and
        /// head-to-head, once both have: otherwise a team that reports first
        /// appears to have won 1-0 while the opponent's goals are still missing.
        /// </summary>
        public void MarkStatsSubmitted(int? teamId)
        {
            if (teamId == HomeTeamId) HomeStatsSubmitted = true;
            else if (teamId == AwayTeamId) AwayStatsSubmitted = true;
            else if (teamId is null) HomeStatsSubmitted = AwayStatsSubmitted = true;

            IsPlayed = HomeStatsSubmitted && AwayStatsSubmitted;
            Raise(new FixtureStatsSubmitted(Id, HomeTeamId, AwayTeamId, teamId));
        }

        public void Reschedule(string? location, string? postcode, DateTime? kickoff)
        {
            Location = location;
            Postcode = postcode;
            Kickoff = kickoff;
            Raise(new FixtureRescheduled(Id, HomeTeamId, AwayTeamId, kickoff, location));
        }

        /// <summary>
        /// Swaps the home and away sides. Each team keeps its own captains (and
        /// its picked squad, which is stored per player); the time, venue and
        /// referee stay as they are for the new home side to change. Only before
        /// the game, with no stats in. Returns why it can't be swapped, or null
        /// once it has been.
        /// </summary>
        public string? SwapSides(DateTime now, bool byAdmin)
        {
            if (IsKnockout)
                return "Knockout games can't be swapped; the bracket decides who's at home.";
            if (IsPlayed || HomeStatsSubmitted || AwayStatsSubmitted)
                return "Stats are already in for this game, so it can't be swapped.";
            if ((Kickoff ?? WindowEnd) <= now)
                return "This game has already kicked off, so it can't be swapped.";

            (HomeTeamId, AwayTeamId) = (AwayTeamId, HomeTeamId);
            (HomeCaptainId, AwayCaptainId) = (AwayCaptainId, HomeCaptainId);
            (HomeViceCaptainId, AwayViceCaptainId) = (AwayViceCaptainId, HomeViceCaptainId);
            (HomeScore, AwayScore) = (AwayScore, HomeScore);

            Raise(new FixtureSidesSwapped(Id, HomeTeamId, AwayTeamId, byAdmin));
            return null;
        }

        /// <summary>
        /// Sets the score from each side's goals. An own goal is credited to the
        /// other side, so a team's score is its own players' goals plus the
        /// opposition's own goals.
        /// </summary>
        public void SetScoreFromStats(int homeGoals, int homeOwnGoals, int awayGoals, int awayOwnGoals)
        {
            HomeScore = homeGoals + awayOwnGoals;
            AwayScore = awayGoals + homeOwnGoals;
        }

        /// <summary>
        /// The true man of the match must be one of the nominations, so a
        /// resubmission that moves a team's man of the match clears the pick and
        /// it has to be made again.
        /// </summary>
        public void KeepTrueMotmOnlyIfNominated(IEnumerable<int> nominatedPlayerIds)
        {
            if (TrueMotmPlayerId is { } id && !nominatedPlayerIds.Contains(id))
                TrueMotmPlayerId = null;
        }
    }
}

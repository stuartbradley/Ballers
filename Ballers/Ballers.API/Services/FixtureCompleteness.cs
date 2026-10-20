using Ballers.API.Data;
using Ballers.API.Models;

namespace Ballers.API.Services
{
    /// <summary>
    /// What is still outstanding on a fixture once stats start coming in. The
    /// admin overview and the manager dashboard both read these rules, so a
    /// gap flagged to one is flagged to the other.
    /// </summary>
    public record FixtureCompleteness(
        int FixtureId, bool IsPlayed, int HomeTeamId, int AwayTeamId,
        bool HomeStatsSubmitted, bool AwayStatsSubmitted,
        bool HomeMotmNamed, bool AwayMotmNamed,
        int FairplayRatings, bool HasMotmNominations, bool TrueMotmPicked)
    {
        // A side that has sent its stats must have named its man of the match.
        public bool HomeNeedsMotm => HomeStatsSubmitted && !HomeMotmNamed;
        public bool AwayNeedsMotm => AwayStatsSubmitted && !AwayMotmNamed;

        public bool NeedsMotm(int teamId)
            => teamId == HomeTeamId ? HomeNeedsMotm
             : teamId == AwayTeamId && AwayNeedsMotm;

        // Once the game is played the referee rates both sides, and the admin
        // picks the true man of the match from the nominations.
        public bool NeedsFairplay => IsPlayed && FairplayRatings < 2;
        public bool NeedsTrueMotm => IsPlayed && HasMotmNominations && !TrueMotmPicked;

        public bool IsComplete => !HomeNeedsMotm && !AwayNeedsMotm && !NeedsFairplay && !NeedsTrueMotm;

        /// <summary>
        /// The completeness of each of <paramref name="fixtures"/> that has any
        /// stats in; a fixture with none has nothing to finish yet.
        /// </summary>
        public static IQueryable<FixtureCompleteness> Query(ApplicationDbContext db, IQueryable<Fixture> fixtures)
            => fixtures
                .Where(f => f.IsPlayed || f.HomeStatsSubmitted || f.AwayStatsSubmitted)
                .Select(f => new FixtureCompleteness(
                    f.Id, f.IsPlayed, f.HomeTeamId, f.AwayTeamId,
                    f.HomeStatsSubmitted, f.AwayStatsSubmitted,
                    f.FixturePlayerStats.Any(s => s.ManOfTheMatch && s.Player!.TeamId == f.HomeTeamId),
                    f.FixturePlayerStats.Any(s => s.ManOfTheMatch && s.Player!.TeamId == f.AwayTeamId),
                    db.FairplayRatings.Count(r => r.FixtureId == f.Id
                        && (r.TeamId == f.HomeTeamId || r.TeamId == f.AwayTeamId)),
                    f.FixturePlayerStats.Any(s => s.ManOfTheMatch),
                    f.TrueMotmPlayerId != null));
    }
}

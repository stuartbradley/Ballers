using Ballers.API.Models;
using Ballers.API.Services;

namespace Ballers.Tests
{
    public class FixtureRulesTests
    {
        private static Fixture NewFixture() => new() { Id = 1, HomeTeamId = 10, AwayTeamId = 20 };

        [Fact]
        public void MarkStatsSubmitted_OnlyPlayedOnceBothSidesIn()
        {
            var f = NewFixture();

            f.MarkStatsSubmitted(10);
            Assert.Equal((true, false, false), (f.HomeStatsSubmitted, f.AwayStatsSubmitted, f.IsPlayed));

            f.MarkStatsSubmitted(20);
            Assert.True(f.IsPlayed);
        }

        [Fact]
        public void MarkStatsSubmitted_NullTeamCountsAsBoth()
        {
            var f = NewFixture();
            f.MarkStatsSubmitted(null);
            Assert.True(f.HomeStatsSubmitted && f.AwayStatsSubmitted && f.IsPlayed);
        }

        [Fact]
        public void SetScoreFromStats_CreditsOwnGoalsToTheOtherSide()
        {
            var f = NewFixture();
            f.SetScoreFromStats(homeGoals: 2, homeOwnGoals: 1, awayGoals: 1, awayOwnGoals: 0);
            Assert.Equal((2, 2), (f.HomeScore, f.AwayScore));
        }

        [Fact]
        public void KeepTrueMotmOnlyIfNominated_ClearsAStalePick()
        {
            var f = NewFixture();
            f.TrueMotmPlayerId = 5;

            f.KeepTrueMotmOnlyIfNominated(new[] { 5, 6 });
            Assert.Equal(5, f.TrueMotmPlayerId);

            f.KeepTrueMotmOnlyIfNominated(new[] { 6 });
            Assert.Null(f.TrueMotmPlayerId);
        }

        [Fact]
        public void Completeness_AsksForMotmOnlyFromSidesWithStatsIn()
        {
            var c = new FixtureCompleteness(1, IsPlayed: false, 10, 20,
                HomeStatsSubmitted: true, AwayStatsSubmitted: false,
                HomeMotmNamed: false, AwayMotmNamed: false,
                FairplayRatings: 0, HasMotmNominations: false, TrueMotmPicked: false);

            Assert.True(c.NeedsMotm(10));
            Assert.False(c.NeedsMotm(20));
            // Not played yet: fairplay and the true MOTM aren't due.
            Assert.False(c.NeedsFairplay || c.NeedsTrueMotm);
            Assert.False(c.IsComplete);
        }

        [Fact]
        public void Completeness_PlayedGameNeedsFairplayAndTrueMotm()
        {
            var c = new FixtureCompleteness(1, IsPlayed: true, 10, 20,
                HomeStatsSubmitted: true, AwayStatsSubmitted: true,
                HomeMotmNamed: true, AwayMotmNamed: true,
                FairplayRatings: 1, HasMotmNominations: true, TrueMotmPicked: false);

            Assert.True(c.NeedsFairplay && c.NeedsTrueMotm);
            Assert.True((c with { FairplayRatings = 2, TrueMotmPicked = true }).IsComplete);
        }
    }
}

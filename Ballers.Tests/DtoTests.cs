using Ballers.Models;

namespace Ballers.Tests
{
    public class DtoTests
    {
        // ── LeagueTableRowDto ────────────────────────────────────────────

        [Theory]
        [InlineData(3, 1, 0, 10)]
        [InlineData(0, 0, 5, 0)]
        [InlineData(2, 2, 1, 8)]
        [InlineData(1, 0, 0, 3)]
        [InlineData(0, 1, 0, 1)]
        public void LeagueTableRowDto_Points_WonTimesThrePlusDrawn(int won, int drawn, int lost, int expected)
        {
            var row = new LeagueTableRowDto { Won = won, Drawn = drawn, Lost = lost };
            Assert.Equal(expected, row.Points);
        }

        [Theory]
        [InlineData(10, 5, 5)]
        [InlineData(5, 10, -5)]
        [InlineData(0, 0, 0)]
        [InlineData(7, 7, 0)]
        public void LeagueTableRowDto_GoalDifference_ForMinusAgainst(int goalsFor, int goalsAgainst, int expected)
        {
            var row = new LeagueTableRowDto { GoalsFor = goalsFor, GoalsAgainst = goalsAgainst };
            Assert.Equal(expected, row.GoalDifference);
        }

        // ── PenaltyTableRowDto ───────────────────────────────────────────

        [Theory]
        [InlineData(2, 1, 0, 7)]
        [InlineData(0, 3, 2, 3)]
        [InlineData(0, 0, 0, 0)]
        public void PenaltyTableRowDto_Points_WonTimesThrePlusDrawn(int won, int drawn, int lost, int expected)
        {
            var row = new PenaltyTableRowDto { Won = won, Drawn = drawn, Lost = lost };
            Assert.Equal(expected, row.Points);
        }

        [Theory]
        [InlineData(15, 10, 5)]
        [InlineData(8, 12, -4)]
        [InlineData(0, 0, 0)]
        public void PenaltyTableRowDto_PenaltyDifference_ForMinusAgainst(int pf, int pa, int expected)
        {
            var row = new PenaltyTableRowDto { PenaltiesFor = pf, PenaltiesAgainst = pa };
            Assert.Equal(expected, row.PenaltyDifference);
        }

        // ── FairplayTableRowDto ──────────────────────────────────────────

        [Fact]
        public void FairplayTableRowDto_DefaultValues_AreZero()
        {
            var row = new FairplayTableRowDto();
            Assert.Equal(0, row.Rated);
            Assert.Equal(0, row.TotalRating);
            Assert.Equal(0.0, row.AverageRating);
        }
    }
}

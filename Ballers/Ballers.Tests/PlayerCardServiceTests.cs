using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class PlayerCardServiceTests
    {
        private const string PlayerUserId = "user-player";

        private static async Task<ApplicationDbContext> SeedBase(
            string dbName,
            int unspent = 80,
            int cap = 99)
        {
            var db = DbContextFactory.Create(dbName);

            db.Teams.Add(new Team { Id = 1, Name = "Team A" });
            db.Players.Add(new Player { Id = 1, Name = "Alex", Number = 9, TeamId = 1, Position = "FWD" });
            db.Users.Add(new ApplicationUser
            {
                Id = PlayerUserId,
                UserName = "alex@ballers.com",
                Email = "alex@ballers.com",
                PlayerId = 1
            });
            db.PlayerCards.Add(new PlayerCardStats
            {
                Id = 1,
                PlayerId = 1,
                UnspentPoints = unspent,
                TotalPointsEarned = unspent
            });
            db.LeagueSettings.Add(new LeagueSetting
            {
                Id = 1,
                AttributeCap = cap,
                SilverOverallThreshold = 20,
                GoldOverallThreshold = 35
            });

            await db.SaveChangesAsync();
            return db;
        }

        private static PlayerCardService Service(ApplicationDbContext db)
            => new(db, new LeagueSettingsService(db));

        // ── Overall rating ──────────────────────────────────────────────

        [Fact]
        public async Task NewCard_HasFullBalanceAndZeroOverall()
        {
            var db = await SeedBase(nameof(NewCard_HasFullBalanceAndZeroOverall));

            var card = await Service(db).GetMyCardAsync(PlayerUserId);

            Assert.Equal(80, card.UnspentPoints);
            Assert.Equal(0, card.Attributes.Overall);
        }

        [Fact]
        public async Task Overall_IsTotalDividedBySix()
        {
            var db = await SeedBase(nameof(Overall_IsTotalDividedBySix));
            var svc = Service(db);

            // 12 + 12 + 12 + 12 + 16 + 16 = 80, so an overall of 13 (80 / 6).
            await svc.AllocateAsync(PlayerUserId, new AllocatePointsRequest
            {
                Finishing = 12, Tackling = 12, Passing = 12,
                Dribbling = 12, Pace = 16, Strength = 16
            });

            var card = await svc.GetMyCardAsync(PlayerUserId);

            Assert.Equal(13, card.Attributes.Overall);
            Assert.Equal(0, card.UnspentPoints);
        }

        [Fact]
        public void Tier_FollowsOverallThresholds()
        {
            var bronze = new PlayerCardStats { Finishing = 10 };
            var silver = new PlayerCardStats { Finishing = 120 };
            var gold = new PlayerCardStats { Finishing = 210 };

            Assert.Equal("bronze", PlayerAttributes.Tier(PlayerAttributes.ComputeOverall(bronze), 20, 35));
            Assert.Equal("silver", PlayerAttributes.Tier(PlayerAttributes.ComputeOverall(silver), 20, 35));
            Assert.Equal("gold", PlayerAttributes.Tier(PlayerAttributes.ComputeOverall(gold), 20, 35));
        }

        // ── Allocation ──────────────────────────────────────────────────

        [Fact]
        public async Task Allocate_AddsToAttributesAndDeductsBalance()
        {
            var db = await SeedBase(nameof(Allocate_AddsToAttributesAndDeductsBalance));

            var card = await Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest
            {
                Finishing = 30, Pace = 20
            });

            Assert.Equal(30, card.Attributes.Finishing);
            Assert.Equal(20, card.Attributes.Pace);
            Assert.Equal(30, card.UnspentPoints);
        }

        [Fact]
        public async Task Allocate_RejectsSpendingMoreThanTheBalance()
        {
            var db = await SeedBase(nameof(Allocate_RejectsSpendingMoreThanTheBalance));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest { Finishing = 81 }));
        }

        [Fact]
        public async Task Allocate_RejectsNegativeAmounts()
        {
            var db = await SeedBase(nameof(Allocate_RejectsNegativeAmounts));

            // A negative would be a way to pull points back out of an attribute,
            // which is exactly what "spends are permanent" forbids.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest
                {
                    Finishing = 20, Tackling = -20
                }));
        }

        [Fact]
        public async Task Allocate_RejectsGoingOverTheCap()
        {
            var db = await SeedBase(nameof(Allocate_RejectsGoingOverTheCap), unspent: 500, cap: 99);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest { Finishing = 100 }));
        }

        [Fact]
        public async Task Allocate_AllowsLandingExactlyOnTheCap()
        {
            var db = await SeedBase(nameof(Allocate_AllowsLandingExactlyOnTheCap), unspent: 500, cap: 99);

            var card = await Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest { Finishing = 99 });

            Assert.Equal(99, card.Attributes.Finishing);
        }

        [Fact]
        public async Task Allocate_RejectsAnEmptyAllocation()
        {
            var db = await SeedBase(nameof(Allocate_RejectsAnEmptyAllocation));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(db).AllocateAsync(PlayerUserId, new AllocatePointsRequest()));
        }

        [Fact]
        public async Task Allocate_NothingIsSpentWhenTheRequestIsRejected()
        {
            var db = await SeedBase(nameof(Allocate_NothingIsSpentWhenTheRequestIsRejected), unspent: 500, cap: 99);
            var svc = Service(db);

            // The second attribute breaks the cap, so the first must not be banked.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.AllocateAsync(PlayerUserId, new AllocatePointsRequest { Pace = 10, Finishing = 200 }));

            var card = await svc.GetMyCardAsync(PlayerUserId);

            Assert.Equal(0, card.Attributes.Pace);
            Assert.Equal(0, card.Attributes.Finishing);
            Assert.Equal(500, card.UnspentPoints);
        }

        [Fact]
        public async Task Allocate_TwoSpendsCannotExceedTheBalanceBetweenThem()
        {
            var db = await SeedBase(nameof(Allocate_TwoSpendsCannotExceedTheBalanceBetweenThem));
            var svc = Service(db);

            await svc.AllocateAsync(PlayerUserId, new AllocatePointsRequest { Finishing = 50 });

            // 50 of the 80 are gone, so a second 50 must be refused rather than
            // leaving the player on a negative balance.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.AllocateAsync(PlayerUserId, new AllocatePointsRequest { Pace = 50 }));

            var card = await svc.GetMyCardAsync(PlayerUserId);
            Assert.Equal(30, card.UnspentPoints);
        }

        // ── Account resolution ──────────────────────────────────────────

        [Fact]
        public async Task Allocate_RefusesAnAccountWithNoPlayerLinked()
        {
            var db = await SeedBase(nameof(Allocate_RefusesAnAccountWithNoPlayerLinked));
            db.Users.Add(new ApplicationUser { Id = "pending", UserName = "p@b.com", Email = "p@b.com" });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(db).AllocateAsync("pending", new AllocatePointsRequest { Pace = 1 }));
        }

        // ── Public cards ────────────────────────────────────────────────

        [Fact]
        public async Task PublicCard_IsNullForAPlayerWithNoAccount()
        {
            var db = await SeedBase(nameof(PublicCard_IsNullForAPlayerWithNoAccount));
            db.Players.Add(new Player { Id = 2, Name = "No Login", Number = 4, TeamId = 1 });
            await db.SaveChangesAsync();

            Assert.Null(await Service(db).GetPublicCardAsync(2));
        }

        [Fact]
        public async Task PublicCards_OnlyIncludePlayersWhoHaveACard()
        {
            var db = await SeedBase(nameof(PublicCards_OnlyIncludePlayersWhoHaveACard));
            db.Players.Add(new Player { Id = 2, Name = "No Login", Number = 4, TeamId = 1 });
            await db.SaveChangesAsync();

            var cards = await Service(db).GetPublicCardsAsync(new[] { 1, 2 });

            Assert.True(cards.ContainsKey(1));
            Assert.False(cards.ContainsKey(2));
        }
    }
}

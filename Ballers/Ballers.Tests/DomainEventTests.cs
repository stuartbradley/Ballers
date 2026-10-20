using Ballers.API.Data;
using Ballers.API.Events;
using Ballers.API.Models;
using Ballers.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballers.Tests
{
    public class DomainEventTests
    {
        private static Fixture NewFixture() => new()
        {
            Id = 1, HomeTeamId = 10, AwayTeamId = 20, WindowEnd = DateTime.UtcNow.AddDays(7)
        };

        // ── Raised by the fixture ──────────────────────────────────────────

        [Fact]
        public void SwapSides_RaisesWithTheNewSides()
        {
            var f = NewFixture();

            Assert.Null(f.SwapSides(DateTime.UtcNow, byAdmin: true));

            var e = Assert.IsType<FixtureSidesSwapped>(Assert.Single(f.TakeDomainEvents()));
            Assert.Equal((20, 10, true), (e.HomeTeamId, e.AwayTeamId, e.ByAdmin));
        }

        [Fact]
        public void SwapSides_RefusedOnceStatsAreIn_AndRaisesNothing()
        {
            var f = NewFixture();
            f.MarkStatsSubmitted(10);
            f.TakeDomainEvents();

            Assert.NotNull(f.SwapSides(DateTime.UtcNow, byAdmin: false));
            Assert.Equal((10, 20), (f.HomeTeamId, f.AwayTeamId));
            Assert.Empty(f.TakeDomainEvents());
        }

        [Fact]
        public void SwapSides_RefusedAfterKickoff()
        {
            var f = NewFixture();
            f.Kickoff = DateTime.UtcNow.AddHours(-1);

            Assert.Contains("kicked off", f.SwapSides(DateTime.UtcNow, byAdmin: false));
        }

        [Fact]
        public void TakeDomainEvents_HandsEachEventOutOnce()
        {
            var f = NewFixture();
            f.Reschedule("Bloomfield Road", null, null);

            Assert.IsType<FixtureRescheduled>(Assert.Single(f.TakeDomainEvents()));
            Assert.Empty(f.TakeDomainEvents());
        }

        // ── Published on save ──────────────────────────────────────────────

        private sealed class RecordingDispatcher : IDomainEventDispatcher
        {
            public List<IDomainEvent> Published { get; } = new();

            public Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
            {
                Published.AddRange(events);
                return Task.CompletedTask;
            }
        }

        private static ApplicationDbContext Context(string name, IDomainEventDispatcher dispatcher)
            => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options, dispatcher);

        [Fact]
        public async Task SaveChanges_PublishesWhatTheSavedEntitiesRaised()
        {
            var dispatcher = new RecordingDispatcher();
            await using var db = Context(nameof(SaveChanges_PublishesWhatTheSavedEntitiesRaised), dispatcher);
            var f = NewFixture();
            db.Fixtures.Add(f);
            await db.SaveChangesAsync();
            Assert.Empty(dispatcher.Published);

            f.Reschedule("Stanley Park", "FY3 9HU", DateTime.UtcNow.AddDays(2));
            await db.SaveChangesAsync();

            var e = Assert.IsType<FixtureRescheduled>(Assert.Single(dispatcher.Published));
            Assert.Equal("Stanley Park", e.Location);

            // Saving again does not publish it twice.
            await db.SaveChangesAsync();
            Assert.Single(dispatcher.Published);
        }

        // ── Handled: the managers are told ─────────────────────────────────

        private static async Task<(ServiceProvider Services, Mock<INotificationService> Notifications)> App(string name)
        {
            var notifications = new Mock<INotificationService>();
            var services = new ServiceCollection()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name)
                    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)))
                .AddScoped(_ => notifications.Object)
                .AddScoped<IKnockoutService>(_ => Mock.Of<IKnockoutService>())
                .AddScoped<FixtureService>()
                .AddDomainEvents()
                .BuildServiceProvider();

            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Teams.AddRange(new Team { Id = 10, Name = "Rovers" }, new Team { Id = 20, Name = "Unity" });
            db.Fixtures.Add(NewFixture());
            await db.SaveChangesAsync();
            return (services, notifications);
        }

        [Fact]
        public async Task Reschedule_TellsTheAwaySide()
        {
            var (services, notifications) = await App(nameof(Reschedule_TellsTheAwaySide));

            await services.GetRequiredService<FixtureService>()
                .UpdateScheduleAsync(1, "Bloomfield Road", null, new DateTime(2026, 11, 1, 10, 30, 0));

            notifications.Verify(n => n.CreateForTeamAsync(
                20, NotificationType.FixtureUpdated,
                "Fixture vs Rovers has been updated — 01 Nov 10:30 at Bloomfield Road.",
                "/fixture/1", null), Times.Once);
            notifications.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Swap_ByAnAdmin_TellsBothSides_InTheOldOrder()
        {
            var (services, notifications) = await App(nameof(Swap_ByAnAdmin_TellsBothSides_InTheOldOrder));

            await services.GetRequiredService<FixtureService>().SwapHomeAwayAsync(1, DateTime.UtcNow, byAdmin: true);

            notifications.Verify(n => n.CreateForTeamAsync(
                20, NotificationType.FixtureUpdated,
                It.Is<string>(m => m.StartsWith("Rovers v Unity is now Unity v Rovers. You're at home now")),
                "/fixture/1?tab=setup", 1), Times.Once);
            notifications.Verify(n => n.CreateForTeamAsync(
                10, NotificationType.FixtureUpdated,
                It.Is<string>(m => m.Contains("An admin swapped it")),
                "/fixture/1", 1), Times.Once);
        }

        [Fact]
        public async Task Swap_ByTheHomeManager_OnlyTellsTheOtherSide()
        {
            var (services, notifications) = await App(nameof(Swap_ByTheHomeManager_OnlyTellsTheOtherSide));

            await services.GetRequiredService<FixtureService>().SwapHomeAwayAsync(1, DateTime.UtcNow);

            notifications.Verify(n => n.CreateForTeamAsync(
                10, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task StatsSubmitted_TellsBothSides()
        {
            var (services, notifications) = await App(nameof(StatsSubmitted_TellsBothSides));

            await services.GetRequiredService<FixtureService>().SubmitStatsAsync(1, new List<PlayerStatDto>(), 10);

            foreach (var team in new[] { 10, 20 })
                notifications.Verify(n => n.CreateForTeamAsync(
                    team, NotificationType.ResultSubmitted, "Stats submitted for Rovers vs Unity.", "/fixture/1", 1), Times.Once);
        }
    }
}

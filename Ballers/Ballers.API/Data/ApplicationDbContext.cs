using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Data
{
    public class ApplicationDbContext:IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions dbContextOptions) : base(dbContextOptions)
        {
            
        }

        public DbSet<Team> Teams  => Set<Team>();
        public DbSet<Season> Seasons => Set<Season>();
        public DbSet<Fixture> Fixtures => Set<Fixture>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<FixturePlayerStat> FixturePlayerStats => Set<FixturePlayerStat>();
        public DbSet<FixturePlayer> FixturePlayers => Set<FixturePlayer>();
        public DbSet<PenaltyShootout> PenaltyShootouts => Set<PenaltyShootout>();
        public DbSet<PenaltyKick> PenaltyKicks => Set<PenaltyKick>();
        public DbSet<FairplayRating> FairplayRatings => Set<FairplayRating>();
        public DbSet<Referee> Referees => Set<Referee>();
        public DbSet<KnockoutFixture> KnockoutFixtures => Set<KnockoutFixture>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
        public DbSet<LeagueSetting> LeagueSettings => Set<LeagueSetting>();
        public DbSet<MatchOfTheDayPost> MatchOfTheDayPosts => Set<MatchOfTheDayPost>();
        public DbSet<MatchOfTheDayPhoto> MatchOfTheDayPhotos => Set<MatchOfTheDayPhoto>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<NotificationSetting>().HasKey(s => s.Type);

            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // HOME TEAM RELATIONSHIP
            builder.Entity<Fixture>()
                .HasOne(f => f.HomeTeam)
                .WithMany()
                .HasForeignKey(f => f.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // AWAY TEAM RELATIONSHIP
            builder.Entity<Fixture>()
                .HasOne(f => f.AwayTeam)
                .WithMany()
                .HasForeignKey(f => f.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // SEASON RELATIONSHIP
            builder.Entity<Fixture>()
                .HasOne(f => f.Season)
                .WithMany(s => s.Fixtures)
                .HasForeignKey(f => f.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Player>()
                .HasOne(p => p.Team)
                .WithMany()
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.Entity<FixturePlayerStat>()
                .HasOne(s => s.Player)
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<FixturePlayerStat>()
                .HasOne(s => s.Fixture)
                .WithMany(f => f.FixturePlayerStats)
                .HasForeignKey(s => s.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PenaltyShootout>()
                .HasOne(s => s.Fixture)
                .WithOne()
                .HasForeignKey<PenaltyShootout>(s => s.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PenaltyKick>()
                .HasOne(k => k.Shootout)
                .WithMany(s => s.Kicks)
                .HasForeignKey(k => k.ShootoutId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PenaltyKick>()
                .HasOne(k => k.Player)
                .WithMany()
                .HasForeignKey(k => k.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PenaltyKick>()
                .HasOne(k => k.Team)
                .WithMany()
                .HasForeignKey(k => k.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FixturePlayer>()
                .HasIndex(fp => new { fp.FixtureId, fp.PlayerId })
                .IsUnique();

            builder.Entity<FairplayRating>()
                .HasOne(r => r.Fixture)
                .WithMany()
                .HasForeignKey(r => r.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FairplayRating>()
                .HasOne(r => r.Team)
                .WithMany()
                .HasForeignKey(r => r.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FairplayRating>()
                .HasIndex(r => new { r.FixtureId, r.TeamId })
                .IsUnique();

            builder.Entity<Fixture>()
                .HasOne(f => f.Referee)
                .WithMany(r => r.Fixtures)
                .HasForeignKey(f => f.RefereeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<KnockoutFixture>()
                .HasOne(k => k.Season)
                .WithMany()
                .HasForeignKey(k => k.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<KnockoutFixture>()
                .HasOne(k => k.HomeTeam)
                .WithMany()
                .HasForeignKey(k => k.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<KnockoutFixture>()
                .HasOne(k => k.AwayTeam)
                .WithMany()
                .HasForeignKey(k => k.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<KnockoutFixture>()
                .HasOne(k => k.LinkedFixture)
                .WithMany()
                .HasForeignKey(k => k.LinkedFixtureId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Fixture>()
                .HasOne(f => f.HomeCaptain).WithMany()
                .HasForeignKey(f => f.HomeCaptainId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Fixture>()
                .HasOne(f => f.HomeViceCaptain).WithMany()
                .HasForeignKey(f => f.HomeViceCaptainId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Fixture>()
                .HasOne(f => f.AwayCaptain).WithMany()
                .HasForeignKey(f => f.AwayCaptainId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Fixture>()
                .HasOne(f => f.AwayViceCaptain).WithMany()
                .HasForeignKey(f => f.AwayViceCaptainId).OnDelete(DeleteBehavior.NoAction);

            builder.Entity<MatchOfTheDayPost>()
                .HasOne(p => p.Fixture)
                .WithMany()
                .HasForeignKey(p => p.FixtureId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MatchOfTheDayPhoto>()
                .HasOne(ph => ph.Post)
                .WithMany(p => p.Photos)
                .HasForeignKey(ph => ph.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}

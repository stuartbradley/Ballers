using Ballers.API.Models;
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
                .WithMany()
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
        }

    }
}

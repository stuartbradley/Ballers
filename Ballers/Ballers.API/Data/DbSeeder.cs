using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Ballers.API.Data
{
    public static class DbSeeder
    {
        public static async Task Seed(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var db = serviceProvider.GetRequiredService<Ballers.API.Data.ApplicationDbContext>();

            await SeedNotificationSettings(db);
            await SeedLeagueSettings(db);

            string[] roles = ["Admin", "Manager", "Referee"];

            foreach (var role in roles)
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            var adminEmail = "Admin@ballers.com";

            var admin = await userManager.FindByNameAsync(adminEmail);

            if (admin == null)
            {
                var adminPassword = config["AdminSeedPassword"]
                    ?? throw new InvalidOperationException("AdminSeedPassword is not configured.");

                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    IsAdmin = true,
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        private static async Task SeedLeagueSettings(Ballers.API.Data.ApplicationDbContext db)
        {
            if (!db.LeagueSettings.Any())
            {
                db.LeagueSettings.Add(new LeagueSetting { PlayersLocked = false });
                await db.SaveChangesAsync();
            }
        }

        private static async Task SeedNotificationSettings(Ballers.API.Data.ApplicationDbContext db)
        {
            var defaults = new[]
            {
                new NotificationSetting { Type = NotificationType.FixtureUpdated,      DisplayName = "Fixture Time/Location Updated", Description = "Notify away team when a fixture's time or location is changed." },
                new NotificationSetting { Type = NotificationType.NoSquadSet,          DisplayName = "No Squad Set (24h Warning)",    Description = "Notify teams that haven't set their squad 24 hours before kickoff." },
                new NotificationSetting { Type = NotificationType.NoRefereeSet,        DisplayName = "No Referee Set (48h Warning)",  Description = "Notify home team when no referee is assigned 48 hours before kickoff." },
                new NotificationSetting { Type = NotificationType.NoLocationSet,       DisplayName = "No Location Set (72h Warning)", Description = "Notify home team when no location is set 72 hours before kickoff." },
                new NotificationSetting { Type = NotificationType.NoStatsEntered,      DisplayName = "Stats Overdue (48h After)",     Description = "Notify both teams when stats haven't been entered 48 hours after a fixture." },
                new NotificationSetting { Type = NotificationType.NewFixtureScheduled, DisplayName = "New Fixture Scheduled",         Description = "Notify both teams when a new fixture is scheduled." },
                new NotificationSetting { Type = NotificationType.ResultSubmitted,     DisplayName = "Result Submitted",              Description = "Notify both teams when fixture stats/result are submitted." },
            };

            foreach (var setting in defaults)
            {
                if (!db.NotificationSettings.Any(s => s.Type == setting.Type))
                    db.NotificationSettings.Add(setting);
            }
            await db.SaveChangesAsync();
        }
    }
}

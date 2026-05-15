using Ballers.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Ballers.API.Data
{
    public static class DbSeeder
    {
        public static async Task Seed(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            string[] roles = ["Admin", "Manager"];

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
                await userManager.CreateAsync(admin, adminPassword);
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}

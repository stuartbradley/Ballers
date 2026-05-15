using Ballers.API.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ballers.Tests.Helpers
{
    public static class DbContextFactory
    {
        public static ApplicationDbContext Create(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        public static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
        {
            var store = new Mock<IUserStore<TUser>>();
#pragma warning disable CS8625
            return new Mock<UserManager<TUser>>(
                store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        }
    }
}

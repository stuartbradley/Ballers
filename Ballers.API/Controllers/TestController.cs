using Ballers.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/test")]
    [AllowAnonymous]
    public class TestController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp;

        public TestController(IWebHostEnvironment env, ApplicationDbContext db, IServiceProvider sp)
        {
            _env = env;
            _db = db;
            _sp = sp;
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset()
        {
            if (!_env.IsEnvironment("Testing"))
                return NotFound();

            await _db.Database.EnsureDeletedAsync();
            await _db.Database.MigrateAsync();
            await DbSeeder.Seed(_sp);
            await DevSeeder.SeedAsync(_sp);

            return Ok(new { message = "BallersAutoTest reset and seeded." });
        }
    }
}

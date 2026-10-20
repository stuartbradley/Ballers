using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    // When, where and who: kickoff, venue, referee, and swapping home and away.
    public partial class FixtureService
    {
        public async Task AssignRefereeAsync(int fixtureId, int? refereeId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return;
            fixture.RefereeId = refereeId;
            await _db.SaveChangesAsync();
        }

        // See Fixture.SwapSides for the rules. Returns why it can't be swapped,
        // or null once it has been.
        public async Task<string?> SwapHomeAwayAsync(int fixtureId, DateTime now, bool byAdmin = false)
        {
            var f = await _db.Fixtures.FindAsync(fixtureId)
                ?? throw new KeyNotFoundException("Fixture not found.");

            var problem = f.SwapSides(now, byAdmin);
            if (problem != null) return problem;

            await _db.SaveChangesAsync();
            return null;
        }

        public async Task<bool> UpdateScheduleAsync(int fixtureId, string? location, string? postcode, DateTime? kickoff)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return false;

            fixture.Reschedule(location, postcode, kickoff);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}

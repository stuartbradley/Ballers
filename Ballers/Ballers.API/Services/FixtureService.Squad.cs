using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    // Who plays: the players to pick from, the squad, and the captains.
    public partial class FixtureService
    {
        public async Task<List<PlayerDto>?> GetPlayersAsync(int fixtureId, bool isAdmin, int? userTeamId, int? requestedTeamId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return null;

            bool isHome = userTeamId == fixture.HomeTeamId;
            int resolvedTeamId = isAdmin
                ? (requestedTeamId ?? fixture.HomeTeamId)
                : (isHome ? fixture.HomeTeamId : fixture.AwayTeamId);

            return await _db.Players.AsNoTracking()
                .Where(p => p.TeamId == resolvedTeamId && p.IsActive)
                .OrderBy(p => p.Number)
                .Select(p => new PlayerDto { Id = p.Id, Name = p.Name, Number = p.Number, Position = p.Position, TeamId = p.TeamId })
                .ToListAsync();
        }

        public async Task<List<FixtureSquadPlayerDto>> GetSquadAsync(int fixtureId)
        {
            return await _db.FixturePlayers.AsNoTracking()
                .Include(fp => fp.Player)
                .Where(fp => fp.FixtureId == fixtureId)
                .Select(fp => new FixtureSquadPlayerDto { PlayerId = fp.PlayerId, Name = fp.Player!.Name })
                .ToListAsync();
        }

        public async Task UpdateSquadAsync(int fixtureId, List<int> playerIds, int? teamId)
        {
            if (teamId.HasValue)
            {
                var teamPlayerIds = await _db.Players
                    .Where(p => p.TeamId == teamId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                var existing = _db.FixturePlayers
                    .Where(fp => fp.FixtureId == fixtureId && teamPlayerIds.Contains(fp.PlayerId));
                _db.FixturePlayers.RemoveRange(existing);

                foreach (var id in playerIds.Where(id => teamPlayerIds.Contains(id)))
                    _db.FixturePlayers.Add(new FixturePlayer { FixtureId = fixtureId, PlayerId = id });
            }
            else
            {
                var existing = _db.FixturePlayers.Where(fp => fp.FixtureId == fixtureId);
                _db.FixturePlayers.RemoveRange(existing);

                foreach (var playerId in playerIds)
                    _db.FixturePlayers.Add(new FixturePlayer { FixtureId = fixtureId, PlayerId = playerId });
            }

            await _db.SaveChangesAsync();
        }

        public async Task SaveCaptaincyAsync(int fixtureId, int teamId, int? captainId, int? viceId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return;

            bool isHome = fixture.HomeTeamId == teamId;
            if (isHome)
            {
                fixture.HomeCaptainId     = captainId;
                fixture.HomeViceCaptainId = viceId;
            }
            else
            {
                fixture.AwayCaptainId     = captainId;
                fixture.AwayViceCaptainId = viceId;
            }
            await _db.SaveChangesAsync();
        }
    }
}

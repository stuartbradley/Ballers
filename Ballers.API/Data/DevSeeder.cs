using Ballers.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Data
{
    public static class DevSeeder
    {
        private static readonly Random _rng = new(42);

        private static readonly string[] TeamNames =
        [
            "Northfield United", "Southgate City", "Eastbrook Rovers",
            "Westfield Athletic", "Riverside FC", "Hilltop Town",
            "Parkside Warriors", "Lakeside Dynamos", "Bridgend Rangers", "Moorland Celtic"
        ];

        private static readonly string[] FirstNames =
        [
            "James", "Jack", "Tom", "Luke", "Ryan", "Daniel", "Samuel", "Chris", "Adam", "Ben",
            "Matt", "Josh", "Alex", "Joe", "Mark", "Dave", "Rob", "Mike", "Scott", "Lee",
            "Harry", "Kyle", "Aaron", "Sean", "Jamie"
        ];

        private static readonly string[] LastNames =
        [
            "Smith", "Jones", "Williams", "Brown", "Taylor", "Wilson", "Davies", "Evans", "Thomas", "Roberts",
            "Johnson", "Lewis", "Walker", "Robinson", "White", "Harris", "Martin", "Thompson", "Clarke", "Hall",
            "Wood", "Jackson", "Hughes", "Young", "Green"
        ];

        private static readonly string[] Locations =
        [
            "Ashton Gate Park", "Victoria Road Pitch", "Kings Meadow",
            "Central Sports Ground", "The Rec", "Riverside Complex",
            "Memorial Ground", "Elmwood Park"
        ];

        private static readonly string[] PositionTemplate =
        [
            "GK",
            "DEF", "DEF", "DEF", "DEF",
            "MID", "MID", "MID", "MID", "MID",
            "FWD", "FWD", "FWD", "FWD", "FWD"
        ];

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            if (await db.Teams.AnyAsync()) return;

            var teams = await SeedTeams(db, userManager);
            var players = await SeedPlayers(db, teams);
            await SeedSeason(db, teams, players);
        }

        private static async Task<List<Team>> SeedTeams(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            var teams = TeamNames.Select(name => new Team { Name = name }).ToList();
            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();

            for (int i = 0; i < teams.Count; i++)
            {
                var email = $"manager{i + 1}@ballers.com";
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    TeamId = teams[i].Id,
                    IsAdmin = false
                };
                var result = await userManager.CreateAsync(user, "Manager123!");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Manager");
            }

            return teams;
        }

        private static async Task<List<Player>> SeedPlayers(ApplicationDbContext db, List<Team> teams)
        {
            var names = (from fn in FirstNames from ln in LastNames select $"{fn} {ln}")
                .OrderBy(_ => _rng.Next())
                .Take(teams.Count * 15)
                .ToList();

            var players = new List<Player>();
            int nameIdx = 0;

            foreach (var team in teams)
            {
                for (int i = 0; i < 15; i++)
                {
                    players.Add(new Player
                    {
                        TeamId = team.Id,
                        Name = names[nameIdx++],
                        Number = i + 1,
                        Position = PositionTemplate[i],
                        IsActive = true
                    });
                }
            }

            db.Players.AddRange(players);
            await db.SaveChangesAsync();
            return players;
        }

        private static async Task SeedSeason(ApplicationDbContext db, List<Team> teams, List<Player> players)
        {
            var season = new Season
            {
                SeasonNumber = 1,
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc),
                IsActive = true
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync();

            // Standard circle-method round-robin for 10 teams (9 rounds per leg, 5 matches per round)
            var rotation = teams.ToList();
            int n = rotation.Count;
            var firstLeg = new List<(Team home, Team away, int round)>();

            for (int r = 0; r < n - 1; r++)
            {
                firstLeg.Add((rotation[0], rotation[n - 1], r));
                for (int i = 1; i < n / 2; i++)
                    firstLeg.Add((rotation[i], rotation[n - 1 - i], r));

                var last = rotation[n - 1];
                for (int i = n - 1; i > 1; i--)
                    rotation[i] = rotation[i - 1];
                rotation[1] = last;
            }

            var allMatchups = firstLeg
                .Concat(firstLeg.Select(f => (home: f.away, away: f.home, round: f.round + n - 1)))
                .ToList();

            int matchNumber = 1;
            var fixtures = new List<Fixture>();

            foreach (var (home, away, round) in allMatchups)
            {
                var windowStart = season.StartDate.AddDays(round * 14);
                var windowEnd = windowStart.AddDays(13);
                var kickoff = windowStart.AddDays(6).Date + TimeSpan.FromHours(14);

                fixtures.Add(new Fixture
                {
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    SeasonId = season.Id,
                    MatchNumber = matchNumber++,
                    WindowStart = windowStart,
                    WindowEnd = windowEnd,
                    Kickoff = kickoff,
                    Location = Locations[_rng.Next(Locations.Length)],
                    IsPlayed = true
                });
            }

            db.Fixtures.AddRange(fixtures);
            await db.SaveChangesAsync();

            var playersByTeam = players.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.ToList());
            var fixturePlayers = new List<FixturePlayer>();
            var fixtureStats = new List<FixturePlayerStat>();

            foreach (var fixture in fixtures)
            {
                var homeSquad = playersByTeam[fixture.HomeTeamId].OrderBy(_ => _rng.Next()).Take(11).ToList();
                var awaySquad = playersByTeam[fixture.AwayTeamId].OrderBy(_ => _rng.Next()).Take(11).ToList();

                var homeGoals = WeightedGoals();
                var awayGoals = WeightedGoals();

                fixture.HomeScore = homeGoals;
                fixture.AwayScore = awayGoals;

                foreach (var p in homeSquad.Concat(awaySquad))
                    fixturePlayers.Add(new FixturePlayer { FixtureId = fixture.Id, PlayerId = p.Id });

                var homeGoalStats = DistributeGoals(homeSquad, homeGoals);
                var awayGoalStats = DistributeGoals(awaySquad, awayGoals);

                var motmPool = homeGoals > awayGoals ? homeSquad :
                               awayGoals > homeGoals ? awaySquad :
                               homeSquad.Concat(awaySquad).ToList();
                var motmId = motmPool[_rng.Next(motmPool.Count)].Id;

                foreach (var p in homeSquad)
                {
                    var (g, a) = homeGoalStats[p.Id];
                    fixtureStats.Add(new FixturePlayerStat
                    {
                        FixtureId = fixture.Id,
                        PlayerId = p.Id,
                        Goals = g,
                        Assists = a,
                        ManOfTheMatch = p.Id == motmId,
                        YellowCards = _rng.Next(12) == 0,
                        RedCard = _rng.Next(70) == 0
                    });
                }

                foreach (var p in awaySquad)
                {
                    var (g, a) = awayGoalStats[p.Id];
                    fixtureStats.Add(new FixturePlayerStat
                    {
                        FixtureId = fixture.Id,
                        PlayerId = p.Id,
                        Goals = g,
                        Assists = a,
                        ManOfTheMatch = p.Id == motmId,
                        YellowCards = _rng.Next(12) == 0,
                        RedCard = _rng.Next(70) == 0
                    });
                }
            }

            db.FixturePlayers.AddRange(fixturePlayers);
            db.FixturePlayerStats.AddRange(fixtureStats);
            db.Fixtures.UpdateRange(fixtures);
            await db.SaveChangesAsync();
        }

        private static int WeightedGoals() => _rng.Next(100) switch
        {
            < 15 => 0,
            < 38 => 1,
            < 62 => 2,
            < 82 => 3,
            < 93 => 4,
            _ => 5
        };

        private static Dictionary<int, (int goals, int assists)> DistributeGoals(List<Player> squad, int total)
        {
            var stats = squad.ToDictionary(p => p.Id, _ => (goals: 0, assists: 0));
            var outfield = squad.Where(p => p.Position != "GK").ToList();
            if (outfield.Count == 0) outfield = squad;

            for (int i = 0; i < total; i++)
            {
                var scorer = outfield[_rng.Next(outfield.Count)];
                stats[scorer.Id] = (stats[scorer.Id].goals + 1, stats[scorer.Id].assists);

                if (_rng.Next(2) == 0)
                {
                    var eligible = squad.Where(p => p.Id != scorer.Id).ToList();
                    if (eligible.Count > 0)
                    {
                        var assister = eligible[_rng.Next(eligible.Count)];
                        stats[assister.Id] = (stats[assister.Id].goals, stats[assister.Id].assists + 1);
                    }
                }
            }

            return stats;
        }
    }
}

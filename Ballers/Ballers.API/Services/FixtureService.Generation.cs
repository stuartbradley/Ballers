using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    // Making a season: drawing up fixtures, or importing them from a CSV.
    public partial class FixtureService
    {
        public async Task GenerateFixturesAsync(List<int> teamIds, DateTime startDate)
        {
            if (teamIds == null || teamIds.Count < 2)
                throw new ArgumentException("At least two teams required.");

            var teams = await _db.Teams
                .Where(t => teamIds.Contains(t.Id))
                .ToListAsync();

            if (teams.Count < 2)
                throw new ArgumentException("Not enough teams.");

            bool isOdd = teams.Count % 2 != 0;
            int roundsPerLeg = isOdd ? teams.Count : teams.Count - 1;
            var endDate = startDate.AddDays(roundsPerLeg * 2 * 14);

            bool overlaps = await _db.Seasons.AnyAsync(s =>
                s.StartDate < endDate && s.EndDate > startDate);
            if (overlaps)
                throw new InvalidOperationException("A season already exists that overlaps with this date range.");

            var lastSeason = await _db.Seasons
                .OrderByDescending(x => x.SeasonNumber)
                .FirstOrDefaultAsync();

            var season = new Season
            {
                SeasonNumber = lastSeason == null ? 1 : lastSeason.SeasonNumber + 1,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = false,
            };
            _db.Seasons.Add(season);

            var random = new Random();
            List<List<(int HomeId, int AwayId)>> firstLegRounds;
            List<List<(int HomeId, int AwayId)>> secondLegRounds = new();

            if (isOdd)
            {
                var shuffled = teams.OrderBy(_ => random.Next()).ToList();
                int n = shuffled.Count;

                firstLegRounds = Enumerable.Range(0, n).Select(r =>
                    Enumerable.Range(1, (n - 1) / 2).Select(i =>
                    {
                        int a = (r + i) % n;
                        int b = (r - i + n) % n;
                        return random.Next(2) == 0
                            ? (HomeId: shuffled[a].Id, AwayId: shuffled[b].Id)
                            : (HomeId: shuffled[b].Id, AwayId: shuffled[a].Id);
                    }).ToList()
                ).ToList();

                int attempts = 0;
                do
                {
                    var order = Enumerable.Range(0, n).OrderBy(_ => random.Next()).ToArray();
                    secondLegRounds = order
                        .Select(r => firstLegRounds[r]
                            .Select(p => (HomeId: p.AwayId, AwayId: p.HomeId)).ToList())
                        .ToList();
                    attempts++;
                }
                while (AnyRoundMirrored(firstLegRounds, secondLegRounds) && attempts < 100);
            }
            else
            {
                int fixturesPerRound = teams.Count / 2;
                var pairs = new List<(int HomeId, int AwayId)>();
                for (int i = 0; i < teams.Count; i++)
                    for (int j = i + 1; j < teams.Count; j++)
                        pairs.Add((teams[i].Id, teams[j].Id));

                firstLegRounds = ChunkIntoRounds(
                    pairs.OrderBy(_ => random.Next()).ToList(), fixturesPerRound);

                var reversedPairs = pairs.Select(p => (HomeId: p.AwayId, AwayId: p.HomeId)).ToList();
                int attempts = 0;
                do
                {
                    secondLegRounds = ChunkIntoRounds(
                        reversedPairs.OrderBy(_ => random.Next()).ToList(), fixturesPerRound);
                    attempts++;
                }
                while (AnyRoundMirrored(firstLegRounds, secondLegRounds) && attempts < 100);
            }

            int matchNumber = 1;
            foreach (var round in firstLegRounds.Concat(secondLegRounds))
            {
                var windowStart = season.StartDate.AddDays((matchNumber - 1) * 14);
                foreach (var (homeId, awayId) in round)
                {
                    _db.Fixtures.Add(new Fixture
                    {
                        Season = season,
                        HomeTeamId = homeId,
                        AwayTeamId = awayId,
                        MatchNumber = matchNumber,
                        WindowStart = windowStart,
                        WindowEnd = windowStart.AddDays(13),
                    });
                }
                matchNumber++;
            }

            await _db.SaveChangesAsync();
        }

        public async Task<ImportFixturesResult> ImportFixturesAsync(Stream csv, int seasonNumber, DateTime startDate, bool makeActive)
        {
            var errors = new List<string>();

            string text;
            using (var reader = new StreamReader(csv))
                text = await reader.ReadToEndAsync();

            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n').Where(l => l.Trim().Length > 0).ToList();
            if (lines.Count < 2)
                return new ImportFixturesResult(0, new() { "CSV is empty or has no data rows." });

            // header → column index
            var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
            int Col(string name) => header.IndexOf(name);
            int cMatch = Col("matchnumber"), cHome = Col("hometeam"), cAway = Col("awayteam");
            int cKick = Col("kickoff"), cLoc = Col("location"), cPost = Col("postcode"), cRef = Col("referee");

            if (cMatch < 0 || cHome < 0 || cAway < 0)
                return new ImportFixturesResult(0, new() { "CSV must have headers: MatchNumber, HomeTeam, AwayTeam (plus optional Kickoff, Location, Postcode, Referee)." });

            if (await _db.Seasons.AnyAsync(s => s.SeasonNumber == seasonNumber))
                return new ImportFixturesResult(0, new() { $"Season {seasonNumber} already exists." });

            var teamsByName = await _db.Teams.ToDictionaryAsync(t => t.Name.Trim().ToLowerInvariant(), t => t.Id);
            var refsByName = await _db.Referees.ToDictionaryAsync(r => r.Name.Trim().ToLowerInvariant(), r => r.Id);

            var season = new Season { SeasonNumber = seasonNumber, StartDate = startDate, IsActive = makeActive };
            var fixtures = new List<Fixture>();

            for (int i = 1; i < lines.Count; i++)
            {
                int rowNum = i + 1; // 1-based incl header
                var cells = SplitCsvLine(lines[i]);
                string Get(int idx) => idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";

                if (!int.TryParse(Get(cMatch), out var matchNumber) || matchNumber < 1)
                { errors.Add($"Row {rowNum}: invalid MatchNumber '{Get(cMatch)}'."); continue; }

                var homeName = Get(cHome); var awayName = Get(cAway);
                if (!teamsByName.TryGetValue(homeName.ToLowerInvariant(), out var homeId))
                { errors.Add($"Row {rowNum}: home team '{homeName}' not found."); continue; }
                if (!teamsByName.TryGetValue(awayName.ToLowerInvariant(), out var awayId))
                { errors.Add($"Row {rowNum}: away team '{awayName}' not found."); continue; }
                if (homeId == awayId)
                { errors.Add($"Row {rowNum}: home and away team are the same ('{homeName}')."); continue; }

                DateTime? kickoff = null;
                var kickStr = Get(cKick);
                if (!string.IsNullOrWhiteSpace(kickStr))
                {
                    if (TryParseKickoff(kickStr, out var k)) kickoff = k;
                    else { errors.Add($"Row {rowNum}: invalid Kickoff '{kickStr}' (use yyyy-MM-dd HH:mm)."); continue; }
                }

                int? refId = null;
                var refName = Get(cRef);
                if (!string.IsNullOrWhiteSpace(refName))
                {
                    if (refsByName.TryGetValue(refName.ToLowerInvariant(), out var rid)) refId = rid;
                    else { errors.Add($"Row {rowNum}: referee '{refName}' not found."); continue; }
                }

                var windowStart = startDate.AddDays((matchNumber - 1) * 14);
                fixtures.Add(new Fixture
                {
                    Season = season,
                    HomeTeamId = homeId,
                    AwayTeamId = awayId,
                    MatchNumber = matchNumber,
                    WindowStart = windowStart,
                    WindowEnd = windowStart.AddDays(13),
                    Kickoff = kickoff,
                    Location = string.IsNullOrWhiteSpace(Get(cLoc)) ? null : Get(cLoc),
                    Postcode = string.IsNullOrWhiteSpace(Get(cPost)) ? null : Get(cPost),
                    RefereeId = refId,
                });
            }

            if (fixtures.Count == 0 && errors.Count == 0)
                errors.Add("No fixture rows found.");
            if (errors.Count > 0)
                return new ImportFixturesResult(0, errors);

            season.EndDate = fixtures.Max(f => f.WindowEnd);

            using var tx = await _db.Database.BeginTransactionAsync();
            _db.Seasons.Add(season);
            _db.Fixtures.AddRange(fixtures);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new ImportFixturesResult(fixtures.Count, errors);
        }

        private static bool TryParseKickoff(string s, out DateTime result)
        {
            string[] formats =
            {
                "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy H:mm", "yyyy-MM-dd", "dd/MM/yyyy"
            };
            if (DateTime.TryParseExact(s, formats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result))
                return true;
            return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }

        // Minimal CSV line parser supporting double-quoted fields.
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        private static List<List<(int HomeId, int AwayId)>> ChunkIntoRounds(
            List<(int HomeId, int AwayId)> pairs, int size) =>
            Enumerable.Range(0, pairs.Count / size)
                .Select(r => pairs.Skip(r * size).Take(size).ToList())
                .ToList();

        private static bool AnyRoundMirrored(
            List<List<(int HomeId, int AwayId)>> firstLeg,
            List<List<(int HomeId, int AwayId)>> secondLeg)
        {
            for (int r = 0; r < firstLeg.Count; r++)
            {
                var f = firstLeg[r]
                    .Select(p => (Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId)))
                    .ToHashSet();
                var s = secondLeg[r]
                    .Select(p => (Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId)))
                    .ToHashSet();
                if (f.SetEquals(s)) return true;
            }
            return false;
        }
    }
}

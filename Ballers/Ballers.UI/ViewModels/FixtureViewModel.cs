using Ballers.Client.Auth;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The fixture page: set it up (when, where, who's reffing), pick the squad
    /// any time before kickoff, then send the result and the penalties after
    /// the game. One step on screen at a time, opening on the one that's due.
    /// </summary>
    public class FixtureViewModel : ViewModelBase
    {
        private readonly FixtureService _fixtures;
        private readonly GalleryService _galleries;
        private readonly AuthService _auth;
        private readonly PenaltyService _penalties;
        private readonly FairplayService _fairplay;
        private readonly RefereeService _referees;
        private readonly LeagueSettingsService _settings;

        public FixtureViewModel(
            FixtureService fixtures, GalleryService galleries, AuthService auth, PenaltyService penalties,
            FairplayService fairplay, RefereeService referees, LeagueSettingsService settings)
        {
            _fixtures = fixtures;
            _galleries = galleries;
            _auth = auth;
            _penalties = penalties;
            _fairplay = fairplay;
            _referees = referees;
            _settings = settings;
        }

        // A squad counts as picked at this many, here and on the HQ.
        public const int SquadMinimum = LeagueRules.SquadMinimum;

        private int _id;

        public FixtureDetailsDto? Fixture { get; private set; }

        public bool IsLoading { get; private set; } = true;
        public bool Busy { get; private set; }
        public string Step { get; private set; } = "squad";
        public bool Reviewing { get; set; }
        public bool SquadSelected { get; private set; }
        public bool MyKicksSaved { get; private set; }

        public List<TeamPlayerRow> TeamPlayers { get; private set; } = new();
        public List<PlayerStatRow> Players { get; private set; } = new();

        public int PreviewHomeScore { get; private set; }
        public int PreviewAwayScore { get; private set; }

        // The opponent's saved contributions, split so the live preview can respond to
        // own goals: their goals count against us, their own goals count for us.
        private int _opponentSavedGoals;
        private int _opponentSavedOwnGoals;

        // The team this page is showing and editing. A manager only ever sees their
        // own team; an admin or referee has no team of their own, so they choose a
        // side and default to the home team.
        public int ActingTeamId { get; private set; }
        public bool ViewingHome => Fixture != null && ActingTeamId == Fixture.HomeTeamId;

        // Sent on writes so the API knows which side an admin is acting for. Null for
        // managers, whose team the API takes from their own account regardless.
        private int? AdminTeamId => IsOfficial ? ActingTeamId : null;

        // Exactly one side has reported, so the result is not counted anywhere yet.
        public bool AwaitingOtherTeamStats =>
            Fixture != null
            && !Fixture.IsKnockout
            && Fixture.HomeStatsSubmitted != Fixture.AwayStatsSubmitted;

        public string TeamYetToSubmit =>
            Fixture!.HomeStatsSubmitted ? Fixture.AwayTeam : Fixture.HomeTeam;

        public List<PenaltyKickRow> PenaltyKicks { get; private set; } = new();
        public List<PenaltyKickResultDto> OpponentKicks { get; private set; } = new();

        // An admin or a referee: they work on either side of the fixture.
        public bool IsOfficial { get; private set; }
        // The true man of the match is kept from referees as well as players, so it
        // needs the stricter flag.
        public bool IsSiteAdmin { get; private set; }
        public bool EditingLocked { get; private set; }
        public int FairplayHomeRating { get; set; } = 5;
        public int FairplayAwayRating { get; set; } = 5;

        public TrueMotmDto? TrueMotm { get; private set; }
        public int? TrueMotmSelection { get; set; }
        public string? TrueMotmMessage { get; private set; }

        // Null when this fixture has no gallery, which is what hides the link.
        public int? GalleryId { get; private set; }

        public bool ViewingOpponent { get; set; }
        public List<OpponentPlayerStatDto> OpponentStats { get; private set; } = new();
        private bool _opponentStatsLoaded;
        public string MyTeamLabel => ViewingHome ? Fixture!.HomeTeam : Fixture!.AwayTeam;
        public string OpponentTeamName => ViewingHome ? Fixture!.AwayTeam : Fixture!.HomeTeam;

        public List<HeadToHeadResultDto> HeadToHead { get; private set; } = new();
        private bool _h2hLoaded;

        public int? CaptainPlayerId { get; private set; }
        public int? VicePlayerId { get; private set; }
        public string? MyCaptainName => ViewingHome ? Fixture?.HomeCaptainName : Fixture?.AwayCaptainName;
        public string? MyViceName => ViewingHome ? Fixture?.HomeViceCaptainName : Fixture?.AwayViceCaptainName;

        public string ScheduleLocation { get; set; } = "";
        public string? SchedulePostcode { get; set; }
        public DateOnly? ScheduleDate { get; set; }
        public TimeOnly? ScheduleTime { get; set; }

        public List<RefereeDto> AllReferees { get; private set; } = new();
        public int? SelectedRefereeId { get; set; }

        public bool ConfirmingSwap { get; set; }

        // ── Derived state ──

        public bool ReadOnly => EditingLocked;

        // The home side (or an admin/referee) sets up a league game; the admins
        // set up knockout games elsewhere.
        public bool CanEditSetup => (IsOfficial || ViewingHome) && !Fixture!.IsKnockout;

        // The home manager (or an admin) can hand hosting to the other side, but
        // only before the game, with no stats in, and never for a knockout game.
        public bool CanSwap =>
            !Fixture!.IsKnockout && !Fixture.IsPlayed && !Fixture.HomeStatsSubmitted && !Fixture.AwayStatsSubmitted
            && !KickedOff && !ReadOnly
            && (IsSiteAdmin || (!IsOfficial && ViewingHome));

        public bool SetupDone => Fixture!.Kickoff != null && !string.IsNullOrWhiteSpace(Fixture.Location) && Fixture.RefereeId != null;
        public bool MyStatsSent => ViewingHome ? Fixture!.HomeStatsSubmitted : Fixture!.AwayStatsSubmitted;
        public bool OpponentStatsSent => ViewingHome ? Fixture!.AwayStatsSubmitted : Fixture!.HomeStatsSubmitted;
        public bool KickedOff => (Fixture!.Kickoff ?? Fixture.WindowEnd) <= DateTime.Now;

        public bool ShowScore => Fixture!.IsPlayed || MyStatsSent || OpponentStatsSent || PreviewHomeScore > 0 || PreviewAwayScore > 0;

        public int MyScore => Players.Sum(x => x.Goals) + _opponentSavedOwnGoals;
        public int TheirScore => _opponentSavedGoals + Players.Sum(x => x.OwnGoals);

        public int PickedCount => TeamPlayers.Count(p => p.IsSelected);

        public record StepInfo(string Key, int Number, string Label, string Hint, bool Done, bool Locked);

        public IEnumerable<StepInfo> Steps => new[]
        {
            new StepInfo("setup", 1, "Setup", "When, where, ref", SetupDone, false),
            new StepInfo("squad", 2, "Squad", "Before kickoff", SquadSelected && CaptainPlayerId != null, false),
            new StepInfo("result", 3, "Result", "After the game", MyStatsSent, !SquadSelected),
            new StepInfo("pens", 4, "Pens", "Last of all", MyKicksSaved, !Fixture!.IsPlayed),
        };

        public string StatusText =>
            Fixture!.IsPlayed ? "Full time"
            : MyStatsSent && !OpponentStatsSent ? $"Waiting for {OpponentTeamName}"
            : KickedOff && !MyStatsSent ? "Send your stats"
            : !SquadSelected ? $"Squad {PickedCount}/{SquadMinimum}"
            : Fixture.RefereeId == null ? "Needs a referee"
            : Fixture.Kickoff == null ? "Needs a kickoff time"
            : "All set";

        public string StatusTone =>
            Fixture!.IsPlayed ? "done"
            : (KickedOff && !MyStatsSent) || (!SquadSelected && !KickedOff) ? "red"
            : StatusText == "All set" ? "green"
            : "amber";

        public string Countdown
        {
            get
            {
                if (Fixture!.IsPlayed) return "🏁 Played";
                var when = Fixture.Kickoff ?? Fixture.WindowStart;
                var days = (when.Date - DateTime.Today).Days;
                if (days < 0) return "⏰ Kicked off";
                if (days == 0) return Fixture.Kickoff != null ? $"⏰ Today, {Fixture.Kickoff:HH:mm}" : "⏰ This week";
                return days == 1 ? "⏰ Tomorrow" : $"⏰ {days} days to go";
            }
        }

        public RefereeDto? CurrentReferee => AllReferees.FirstOrDefault(r => r.Id == Fixture!.RefereeId);

        public string? MapUrl => Format.MapUrl(Fixture!.Location, Fixture.Postcode);

        public bool IsBooked(RefereeDto r) => r.UpcomingFixtures.Any(x =>
            x.FixtureId != Fixture!.Id && x.WindowStart <= Fixture.WindowEnd && x.WindowEnd >= Fixture.WindowStart);

        public record PositionGroup(string Name, List<TeamPlayerRow> Players);

        private static readonly (string Code, string Name)[] Positions =
            { ("GK", "Goalkeepers"), ("DEF", "Defenders"), ("MID", "Midfielders"), ("FWD", "Forwards") };

        public IEnumerable<PositionGroup> PositionGroups
        {
            get
            {
                foreach (var (code, name) in Positions)
                {
                    var list = TeamPlayers.Where(p => p.Position == code).OrderBy(p => p.Number).ThenBy(p => p.Name).ToList();
                    if (list.Any()) yield return new(name, list);
                }
                var rest = TeamPlayers.Where(p => !Positions.Any(x => x.Code == p.Position)).OrderBy(p => p.Name).ToList();
                if (rest.Any()) yield return new("Others", rest);
            }
        }

        // ── Rows ──

        public class PenaltyKickRow
        {
            public int Order { get; set; }
            public int PlayerId { get; set; }
            public bool Scored { get; set; }
        }

        public class PlayerStatRow
        {
            public int TeamId { get; set; }
            public int PlayerId { get; set; }
            public string Name { get; set; } = "";
            private int goals;
            public int Goals
            {
                get => goals;
                set { goals = value; OnGoalsChanged?.Invoke(); }
            }
            private int ownGoals;
            public int OwnGoals
            {
                get => ownGoals;
                set { ownGoals = value; OnGoalsChanged?.Invoke(); }
            }
            public Action? OnGoalsChanged;
            public int Assists { get; set; }
            public bool IsMotm { get; set; }
            public bool HasYellowCard { get; set; }
            public bool HasRedCard { get; set; }
        }

        public class TeamPlayerRow
        {
            public int PlayerId { get; set; }
            public string Name { get; set; } = "";
            public int Number { get; set; }
            public string Position { get; set; } = "";
            public bool IsSelected { get; set; }
        }

        // ── Loading ──

        /// <param name="tab">Opens straight on a step: setup, squad, stats (the
        /// result) or pens. The manager's HQ links to squad and stats.</param>
        public async Task InitializeAsync(int id, string? tab)
        {
            _id = id;
            await LoadFixture();
            if (Fixture != null) Step = StartingStep(tab);
            NotifyChanged();
            // Not needed to draw the page; it fills in the side panel when it lands.
            await LoadHeadToHead();
        }

        // The step asked for, else the one that's due: after kickoff the result
        // (then pens); before it, setup if it's yours and not done, else the squad.
        private string StartingStep(string? tab)
        {
            var asked = tab switch { "stats" => "result", "overview" => "setup", { } t => t, _ => null };
            if (asked is "setup" or "squad" or "pens") return asked;
            if (asked == "result") return SquadSelected ? "result" : "squad";

            if (Fixture!.IsPlayed || KickedOff)
                return !SquadSelected ? "squad" : !MyStatsSent ? "result" : Fixture.IsPlayed && !MyKicksSaved ? "pens" : "result";
            if (CanEditSetup && !SetupDone) return "setup";
            return "squad";
        }

        public void GoTo(string key)
        {
            var target = Steps.First(s => s.Key == key);
            if (target.Locked)
            {
                _ = ShowToast(key == "result" ? $"Pick at least {SquadMinimum} players first." : "Penalties open once both teams have sent their stats.", success: false);
                return;
            }
            Step = key;
            Reviewing = false;
            ViewingOpponent = false;
        }

        private async Task LoadFixture()
        {
            // Everything that only needs the fixture's id goes in one parallel
            // batch; what depends on who's looking (their side, admin extras)
            // follows in a second. One after another this was a dozen round trips.
            var fixtureTask = _fixtures.GetFixture(_id);
            var statsTask = _fixtures.GetFixtureStats(_id);
            var userTask = _auth.GetCurrentUser();
            var galleryTask = _galleries.GetGalleryIdForFixture(_id);
            var settingsTask = _settings.Get();
            var squadTask = _fixtures.GetFixtureSquad(_id);
            var shootoutTask = _penalties.GetShootout(_id);
            // The referee list doesn't change while the page is open.
            var refereesTask = AllReferees.Any() ? Task.FromResult(AllReferees) : _referees.GetAll();
            await Task.WhenAll(fixtureTask, statsTask, userTask, galleryTask, settingsTask, squadTask, shootoutTask, refereesTask);

            Fixture = fixtureTask.Result;
            IsLoading = false;

            if (Fixture == null) return;

            var savedStats = statsTask.Result;
            var user = userTask.Result;
            AllReferees = refereesTask.Result;

            IsOfficial = (user?.IsAdmin ?? false) || (user?.IsReferee ?? false);
            IsSiteAdmin = user?.IsAdmin ?? false;

            // An admin or referee has no team of their own, so without an explicit
            // choice the page would fall through to the away team's data while
            // labelling it as the home team's. Default them to the home side and let
            // them switch; a manager is pinned to whichever side they manage.
            if (IsOfficial)
            {
                if (ActingTeamId != Fixture.HomeTeamId && ActingTeamId != Fixture.AwayTeamId)
                    ActingTeamId = Fixture.HomeTeamId;
            }
            else
            {
                ActingTeamId = user?.TeamId == Fixture.AwayTeamId
                    ? Fixture.AwayTeamId
                    : Fixture.HomeTeamId;
            }

            GalleryId = galleryTask.Result;
            // Global admin "shut down editing" lock — only a true admin bypasses it.
            EditingLocked = !(user?.IsAdmin ?? false) && settingsTask.Result.FixturesLocked;

            // Second batch: this side's players, and the admin/referee extras.
            var myPlayersTask = _fixtures.GetFixturePlayers(Fixture.Id, IsOfficial ? ActingTeamId : null);
            var trueMotmTask = IsSiteAdmin ? _fixtures.GetTrueMotm(Fixture.Id) : Task.FromResult<TrueMotmDto?>(null);
            var fairplayTask = IsOfficial ? _fairplay.GetRatings(Fixture.Id) : null;
            await Task.WhenAll(myPlayersTask, trueMotmTask, fairplayTask ?? Task.CompletedTask);

            if (IsSiteAdmin)
            {
                TrueMotm = trueMotmTask.Result;
                TrueMotmSelection = TrueMotm?.SelectedPlayerId;
            }

            if (fairplayTask != null)
            {
                // Fairplay ratings stay admin/referee only.
                var fairplay = await fairplayTask;
                FairplayHomeRating = fairplay?.HomeRating ?? 5;
                FairplayAwayRating = fairplay?.AwayRating ?? 5;
            }

            var squadIds = squadTask.Result.Select(p => p.PlayerId).ToHashSet();
            TeamPlayers = myPlayersTask.Result.Select(p => new TeamPlayerRow
            {
                PlayerId = p.Id,
                Name = p.Name,
                Number = p.Number,
                Position = p.Position,
                IsSelected = squadIds.Contains(p.Id)
            }).ToList();

            SquadSelected = PickedCount >= SquadMinimum;
            BuildStatRows(savedStats);

            PreviewHomeScore = Fixture.HomeScore;
            PreviewAwayScore = Fixture.AwayScore;

            var shootout = shootoutTask.Result;
            var myKicks = ViewingHome ? shootout?.HomeKicks : shootout?.AwayKicks;
            OpponentKicks = (ViewingHome ? shootout?.AwayKicks : shootout?.HomeKicks) ?? new();
            MyKicksSaved = myKicks?.Count >= 5;

            PenaltyKicks = Enumerable.Range(1, 5).Select(i =>
            {
                var existing = myKicks?.FirstOrDefault(k => k.Order == i);
                return new PenaltyKickRow
                {
                    Order = i,
                    PlayerId = existing?.PlayerId ?? 0,
                    Scored = existing?.Scored ?? false
                };
            }).ToList();

            CaptainPlayerId = ViewingHome ? Fixture.HomeCaptainId : Fixture.AwayCaptainId;
            VicePlayerId = ViewingHome ? Fixture.HomeViceCaptainId : Fixture.AwayViceCaptainId;

            // Schedule + referee are editable by managers of the fixture too, so load
            // them for everyone who can reach this page.
            LoadSetupFields();
        }

        private void LoadSetupFields()
        {
            ScheduleLocation = Fixture!.Location ?? "";
            SchedulePostcode = Fixture.Postcode;
            ScheduleDate = Fixture.Kickoff.HasValue ? DateOnly.FromDateTime(Fixture.Kickoff.Value) : null;
            ScheduleTime = Fixture.Kickoff.HasValue ? TimeOnly.FromDateTime(Fixture.Kickoff.Value) : null;
            SelectedRefereeId = Fixture.RefereeId;
        }

        // One stat row per picked player, filled from anything already saved.
        private void BuildStatRows(List<PlayerStatDto> savedStats)
        {
            Players = TeamPlayers
                .Where(p => p.IsSelected)
                .Select(p =>
                {
                    var stat = savedStats.FirstOrDefault(x => x.PlayerId == p.PlayerId);
                    return new PlayerStatRow
                    {
                        PlayerId = p.PlayerId,
                        Name = p.Name,
                        TeamId = ActingTeamId,
                        Goals = stat?.Goals ?? 0,
                        OwnGoals = stat?.OwnGoals ?? 0,
                        Assists = stat?.Assists ?? 0,
                        IsMotm = stat?.IsManOfTheMatch ?? false,
                        HasYellowCard = stat?.HadYellowCard ?? false,
                        HasRedCard = stat?.HadRedCard ?? false
                    };
                }).ToList();

            CaptureOpponentSavedGoals(savedStats);
            foreach (var p in Players)
                p.OnGoalsChanged = UpdateScorePreview;
        }

        private async Task LoadHeadToHead()
        {
            if (!_h2hLoaded && Fixture != null && !Fixture.IsKnockout)
            {
                HeadToHead = await _fixtures.GetHeadToHead(Fixture.Id);
                _h2hLoaded = true;
                NotifyChanged();
            }
        }

        // ── Setup ──

        // Time, venue and referee in one go.
        public async Task SaveSetup()
        {
            Busy = true;
            try
            {
                DateTime? kickoff = ScheduleDate.HasValue
                    ? ScheduleDate.Value.ToDateTime(ScheduleTime ?? TimeOnly.MinValue)
                    : null;
                await _fixtures.UpdateFixtureSchedule(Fixture!.Id, ScheduleLocation, SchedulePostcode, kickoff);
                if (SelectedRefereeId != Fixture.RefereeId)
                    await _referees.AssignToFixture(Fixture.Id, SelectedRefereeId);
                Fixture = (await _fixtures.GetFixture(Fixture.Id))!;
                LoadSetupFields();
            }
            catch (Exception ex)
            {
                Busy = false;
                await ShowToast($"Couldn't save: {ex.Message}", success: false);
                return;
            }
            Busy = false;

            // Done here, so move on.
            if (SetupDone) Step = "squad";
            await ShowToast(SetupDone ? "Setup saved. On to the squad!" : "Saved. Fill in the rest when you can.");
        }

        public async Task SwapHomeAway()
        {
            Busy = true;
            try
            {
                await _fixtures.SwapHomeAway(Fixture!.Id);
                await LoadFixture();
            }
            catch (Exception ex)
            {
                Busy = false;
                ConfirmingSwap = false;
                await ShowToast(ex.Message, success: false);
                return;
            }
            Busy = false;
            ConfirmingSwap = false;
            await ShowToast($"Swapped! {Fixture!.HomeTeam} are at home now.");
        }

        public async Task ClearSchedule()
        {
            Busy = true;
            await _fixtures.UpdateFixtureSchedule(Fixture!.Id, null, null, null);
            Fixture = (await _fixtures.GetFixture(Fixture.Id))!;
            LoadSetupFields();
            Busy = false;
            await ShowToast("Time and venue cleared.");
        }

        // ── Squad ──

        public void TogglePick(TeamPlayerRow p)
        {
            p.IsSelected = !p.IsSelected;
            // A captain has to be in the squad.
            if (!p.IsSelected)
            {
                if (CaptainPlayerId == p.PlayerId) CaptainPlayerId = null;
                if (VicePlayerId == p.PlayerId) VicePlayerId = null;
            }
        }

        public void SetCaptain(int playerId)
        {
            CaptainPlayerId = CaptainPlayerId == playerId ? null : playerId;
            if (CaptainPlayerId.HasValue && VicePlayerId == CaptainPlayerId) VicePlayerId = null;
        }

        public void SetVice(int playerId)
        {
            VicePlayerId = VicePlayerId == playerId ? null : playerId;
            if (VicePlayerId.HasValue && CaptainPlayerId == VicePlayerId) CaptainPlayerId = null;
        }

        // The squad and its captains in one go.
        public async Task SaveSquad()
        {
            Busy = true;
            try
            {
                var selected = TeamPlayers.Where(p => p.IsSelected).Select(p => p.PlayerId).ToList();
                await _fixtures.SaveSquad(Fixture!.Id, selected, AdminTeamId);
                await _fixtures.SaveCaptaincy(Fixture.Id, CaptainPlayerId, VicePlayerId, AdminTeamId);

                SquadSelected = selected.Count >= SquadMinimum;
                BuildStatRows(await _fixtures.GetFixtureStats(Fixture.Id));
                Fixture = (await _fixtures.GetFixture(Fixture.Id))!;
            }
            catch (Exception ex)
            {
                Busy = false;
                await ShowToast($"Couldn't save the squad: {ex.Message}", success: false);
                return;
            }
            Busy = false;

            await ShowToast(!SquadSelected
                ? $"Saved. You need {SquadMinimum} for a full squad."
                : CaptainPlayerId == null ? "Squad saved. Don't forget a captain!" : "Squad saved. Good luck! 🍀");
        }

        // ── Result ──

        public void SelectMotm(PlayerStatRow selected)
        {
            var wasSelected = selected.IsMotm;
            foreach (var p in Players) p.IsMotm = false;
            if (!wasSelected) selected.IsMotm = true;
        }

        public async Task StartReview()
        {
            if (!Players.Any(p => p.IsMotm))
            {
                await ShowToast("Pick a Man of the Match first ⭐", success: false);
                return;
            }
            Reviewing = true;
        }

        public async Task SubmitStats()
        {
            var stats = Players.Select(p => new FixturePlayerStatInput
            {
                PlayerId = p.PlayerId,
                Goals = p.Goals,
                OwnGoals = p.OwnGoals,
                Assists = p.Assists,
                IsManOfTheMatch = p.IsMotm,
                HadYellowCard = p.HasYellowCard,
                HadRedCard = p.HasRedCard
            }).ToList();

            Busy = true;
            try
            {
                await _fixtures.SubmitStats(Fixture!.Id, stats, AdminTeamId);
                await LoadFixture();
            }
            catch (Exception ex)
            {
                Busy = false;
                await ShowToast($"Couldn't send: {ex.Message}", success: false);
                return;
            }
            Busy = false;
            Reviewing = false;

            await ShowToast(Fixture!.IsPlayed ? "Stats sent. Result confirmed! 🎉" : $"Stats sent. Now waiting on {OpponentTeamName}.");
        }

        public async Task LoadOpponentStats()
        {
            if (!_opponentStatsLoaded)
            {
                // "Opponent" is relative to the side being viewed, so switching to the
                // away team must show the home team's stats rather than always away.
                int? overrideTeamId = IsOfficial
                    ? (ViewingHome ? Fixture!.AwayTeamId : Fixture!.HomeTeamId)
                    : null;
                OpponentStats = await _fixtures.GetOpponentStats(Fixture!.Id, overrideTeamId);
                _opponentStatsLoaded = true;
            }
            ViewingOpponent = true;
        }

        /// <summary>Splits the fixture's saved stats into the opponent's goals and own
        /// goals. Rows for anyone outside this team's squad belong to the opposition —
        /// the stats feed covers the whole fixture, not just our side.</summary>
        private void CaptureOpponentSavedGoals(List<PlayerStatDto> savedStats)
        {
            var mySquadIds = TeamPlayers.Select(p => p.PlayerId).ToHashSet();
            var opponentRows = savedStats.Where(s => !mySquadIds.Contains(s.PlayerId)).ToList();
            _opponentSavedGoals = opponentRows.Sum(s => s.Goals);
            _opponentSavedOwnGoals = opponentRows.Sum(s => s.OwnGoals);
        }

        private void UpdateScorePreview()
        {
            if (Fixture == null) return;
            if (ViewingHome) { PreviewHomeScore = MyScore; PreviewAwayScore = TheirScore; }
            else { PreviewHomeScore = TheirScore; PreviewAwayScore = MyScore; }
        }

        // ── Penalties ──

        public async Task SubmitPenalties()
        {
            var kicks = PenaltyKicks
                .Where(k => k.PlayerId > 0)
                .Select(k => new PenaltyKickEntry { PlayerId = k.PlayerId, Order = k.Order, Scored = k.Scored })
                .ToList();

            if (kicks.Count != 5)
            {
                await ShowToast("Pick a taker for all 5 kicks.", success: false);
                return;
            }

            Busy = true;
            await _penalties.SubmitKicks(Fixture!.Id, kicks);
            await LoadFixture();
            Busy = false;
            await ShowToast("Penalties sent. That's this one done! 🏁");
        }

        // ── Admin / referee ──

        // Reloads the page's data for the other side of the fixture: squad, stats,
        // captaincy and penalties all follow the choice.
        public async Task SwitchActingTeam(int teamId)
        {
            if (ActingTeamId == teamId) return;

            ActingTeamId = teamId;

            // The opponent is now the other team, so the cached stats are stale.
            _opponentStatsLoaded = false;
            OpponentStats = new();
            ViewingOpponent = false;
            Reviewing = false;

            await LoadFixture();
        }

        public async Task SaveFairplay()
        {
            if (FairplayHomeRating < 1 || FairplayHomeRating > 10 ||
                FairplayAwayRating < 1 || FairplayAwayRating > 10)
            {
                await ShowToast("Ratings must be between 1 and 10.", success: false);
                return;
            }
            await _fairplay.SubmitRatings(Fixture!.Id, FairplayHomeRating, FairplayAwayRating);
            await ShowToast("Fairplay ratings saved.");
        }

        public async Task SaveTrueMotm()
        {
            if (!TrueMotmSelection.HasValue)
            {
                await ShowToast("Pick one of the two men of the match first.", success: false);
                return;
            }
            await PersistTrueMotm(TrueMotmSelection, "True man of the match saved.");
        }

        public Task ClearTrueMotm()
            => PersistTrueMotm(null, "True man of the match cleared.");

        private async Task PersistTrueMotm(int? playerId, string successMessage)
        {
            try
            {
                await _fixtures.SaveTrueMotm(Fixture!.Id, playerId);
            }
            catch (ApiException ex)
            {
                await ShowToast(ex.Message, success: false);
                return;
            }

            TrueMotmSelection = playerId;
            if (TrueMotm != null) TrueMotm.SelectedPlayerId = playerId;

            // Reflected in the panel as well as the toast, since the toast disappears
            // and this is the only place the pick is visible.
            TrueMotmMessage = successMessage;
            await ShowToast(successMessage);
        }
    }
}

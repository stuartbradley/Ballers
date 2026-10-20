using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for player cards: how many points each thing earns, the card
    /// rules (cap, tier thresholds), and running the points payout.
    /// </summary>
    public class AdminCardsViewModel : ViewModelBase
    {
        private readonly AdminService _admin;

        public AdminCardsViewModel(AdminService admin) => _admin = admin;

        public PointsSettingsDto? Settings { get; private set; }
        public bool Dirty { get; set; }
        public bool Saving { get; private set; }
        public bool Running { get; private set; }
        public bool IsError { get; private set; }
        public string? Message { get; private set; }

        public record NumberField(string Label, string Hint, Func<int> Get, Action<int> Set);

        public IEnumerable<NumberField> EarnFields
        {
            get
            {
                var s = Settings!;
                yield return new("Appearance", "Featuring in a played fixture", () => s.PointsPerAppearance, v => s.PointsPerAppearance = v);
                yield return new("Win", "Being on the winning side", () => s.PointsPerWin, v => s.PointsPerWin = v);
                yield return new("Goal", "Each goal scored", () => s.PointsPerGoal, v => s.PointsPerGoal = v);
                yield return new("Assist", "Each assist", () => s.PointsPerAssist, v => s.PointsPerAssist = v);
                yield return new("Clean sheet", "Keepers and defenders only", () => s.PointsPerCleanSheet, v => s.PointsPerCleanSheet = v);
                yield return new("Man of the Match", "Their team's nomination", () => s.PointsPerMotm, v => s.PointsPerMotm = v);
            }
        }

        public IEnumerable<NumberField> CardFields
        {
            get
            {
                var s = Settings!;
                yield return new("Sign-up grant", "Points a new player starts with", () => s.SignupPointsGrant, v => s.SignupPointsGrant = v);
                yield return new("Attribute cap", "Highest any one attribute can go", () => s.AttributeCap, v => s.AttributeCap = v);
                yield return new("Silver from", "Overall a silver card starts at", () => s.SilverOverallThreshold, v => s.SilverOverallThreshold = v);
                yield return new("Gold from", "Overall a gold card starts at", () => s.GoldOverallThreshold, v => s.GoldOverallThreshold = v);
            }
        }

        /// <summary>Sets a field from what was typed; anything that isn't a number
        /// leaves it as it was, and nothing goes below zero.</summary>
        public void SetField(NumberField f, string? typed)
        {
            f.Set(int.TryParse(typed, out var v) ? Math.Max(0, v) : f.Get());
            Dirty = true;
        }

        public async Task InitializeAsync() => Settings = await _admin.GetPointsSettings();

        public async Task Save()
        {
            if (Settings == null) return;
            Saving = true;
            Message = null;
            try
            {
                await _admin.UpdatePointsSettings(Settings);
                Settings = await _admin.GetPointsSettings();
                Dirty = false;
                IsError = false;
                Message = "Saved.";
            }
            catch (Exception ex)
            {
                IsError = true;
                Message = ex.Message;
            }
            finally
            {
                Saving = false;
            }
        }

        public async Task RunPayout()
        {
            Running = true;
            Message = null;
            try
            {
                var (fixtures, players, points) = await _admin.RunPlayerPoints();
                IsError = false;
                Message = points == 0
                    ? "Nothing to pay out: every played fixture is already settled."
                    : $"Credited {points} point(s) to {players} player(s) across {fixtures} fixture(s).";
                var fresh = await _admin.GetPointsSettings();
                if (fresh != null && Settings != null) Settings.LastPointsRunAt = fresh.LastPointsRunAt;
            }
            catch (Exception ex)
            {
                IsError = true;
                Message = ex.Message;
            }
            finally
            {
                Running = false;
            }
        }
    }
}

using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A player's own card: the points they've earned, and spending them on
    /// attributes. Moving up a tier on save plays the upgrade animation.
    /// </summary>
    public class MyCardViewModel : ViewModelBase
    {
        private readonly PlayerCardService _cards;

        public MyCardViewModel(PlayerCardService cards) => _cards = cards;

        public MyPlayerCardDto? Card { get; private set; }
        public List<PlayerPointsAwardDto> Awards { get; private set; } = new();
        public List<AttributeRow> Rows { get; private set; } = new();

        public bool Loading { get; private set; } = true;
        public bool Saving { get; private set; }
        public bool Saved { get; private set; }
        public string? Error { get; private set; }
        public string? PreviewTier { get; private set; }

        // The tier the card art shows: the saved one. It only changes on Save, and
        // a move up plays the upgrade (see PlayUpgrade).
        public string? DisplayTier { get; private set; }
        public bool Upgrading { get; private set; }
        public bool ShowBanner { get; private set; }
        public string? UpgradeFrom { get; private set; }
        public string? UpgradeTo { get; private set; }

        // The league's own tier thresholds, so the preview tier is right. Falls
        // back to the defaults if they could not be fetched.
        private int _silverAt = 20;
        private int _goldAt = 35;

        public int Allocated => Rows.Sum(r => r.Added);
        public int Remaining => (Card?.UnspentPoints ?? 0) - Allocated;

        public sealed class AttributeRow
        {
            public required string Name { get; init; }
            public required string Label { get; init; }
            public int Current { get; set; }
            public int Added { get; set; }
        }

        public async Task InitializeAsync()
        {
            var thresholds = await _cards.GetTierThresholds();
            if (thresholds != null)
            {
                _silverAt = thresholds.SilverOverallThreshold;
                _goldAt = thresholds.GoldOverallThreshold;
            }
            await Load();
        }

        public string PositionPlural => Card?.Position switch
        {
            "GK" => "goalkeepers",
            "DEF" => "defenders",
            "MID" => "midfielders",
            "FWD" => "forwards",
            var other => $"{other} players"
        };

        // A stat's share of the league's cap, as a bar width.
        public string Share(int value)
            => Math.Clamp(value * 100.0 / Math.Max(1, Card?.AttributeCap ?? 99), 0, 100)
                .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        // Only the first load shows the spinner. A reload after a failed save keeps
        // the page in place: swapping it out would redraw the panels, and the
        // scroll-in reveal has already been spent on the first ones.
        private async Task Load()
        {
            if (Card == null) Loading = true;
            Card = await _cards.GetMyCard();
            Awards = await _cards.GetMyAwards();
            BuildRows();
            if (Card != null) DisplayTier = TierFor(Card.Attributes.Overall);
            Loading = false;
        }

        private string TierFor(int overall)
            => overall >= _goldAt ? "gold" : overall >= _silverAt ? "silver" : "bronze";

        // Spending as planned would move the card up a tier.
        public bool PreviewIsUpgrade => TierRank(PreviewTier) > TierRank(DisplayTier);

        private static int TierRank(string? tier) => tier switch
        {
            "gold" => 2,
            "silver" => 1,
            "bronze" => 0,
            _ => -1
        };

        // The card turns away, swaps its art at the edge-on moment, and turns back
        // in a burst of light. The timings match the keyframes in MyCard.razor.css:
        // the swap lands at a third of the 1.2s turn.
        private async Task PlayUpgrade(string from, string to)
        {
            UpgradeFrom = from;
            UpgradeTo = to;
            Upgrading = true;
            ShowBanner = true;
            NotifyChanged();

            await Delay(TimeSpan.FromMilliseconds(400));
            DisplayTier = to;
            NotifyChanged();

            await Delay(TimeSpan.FromMilliseconds(1000));
            Upgrading = false;
            NotifyChanged();

            // The banner stays a moment longer so it can be read, without holding
            // up the buttons while it does.
            _ = HideBannerLater();
        }

        private async Task HideBannerLater()
        {
            await Delay(TimeSpan.FromMilliseconds(2600));
            ShowBanner = false;
            NotifyChanged();
        }

        private void BuildRows()
        {
            if (Card == null) return;

            var a = Card.Attributes;
            Rows = new()
            {
                new() { Name = nameof(a.Finishing), Label = "Finishing", Current = a.Finishing },
                new() { Name = nameof(a.Tackling),  Label = "Tackling",  Current = a.Tackling },
                new() { Name = nameof(a.Passing),   Label = "Passing",   Current = a.Passing },
                new() { Name = nameof(a.Dribbling), Label = "Dribbling", Current = a.Dribbling },
                new() { Name = nameof(a.Pace),      Label = "Pace",      Current = a.Pace },
                new() { Name = nameof(a.Strength),  Label = "Strength",  Current = a.Strength },
            };

            UpdatePreview();
        }

        // Mirrors the server's rules so a spend that would be refused cannot be built
        // in the first place. The server still checks — this only saves a round trip.
        public bool CanAdd(AttributeRow row)
            => Remaining > 0 && row.Current + row.Added < (Card?.AttributeCap ?? 99);

        public void Step(AttributeRow row, int delta)
        {
            if (delta > 0 && !CanAdd(row)) return;
            if (delta < 0 && row.Added == 0) return;

            row.Added += delta;
            Saved = false;
            Error = null;
            UpdatePreview();
        }

        public void Reset()
        {
            foreach (var row in Rows) row.Added = 0;
            Saved = false;
            Error = null;
            UpdatePreview();
        }

        public PlayerAttributesDto PreviewAttributes => new()
        {
            Finishing = Value(nameof(PlayerAttributesDto.Finishing)),
            Tackling = Value(nameof(PlayerAttributesDto.Tackling)),
            Passing = Value(nameof(PlayerAttributesDto.Passing)),
            Dribbling = Value(nameof(PlayerAttributesDto.Dribbling)),
            Pace = Value(nameof(PlayerAttributesDto.Pace)),
            Strength = Value(nameof(PlayerAttributesDto.Strength)),
            Overall = PreviewOverall
        };

        private int Value(string name)
        {
            var row = Rows.FirstOrDefault(r => r.Name == name);
            return row == null ? 0 : row.Current + row.Added;
        }

        public int PreviewOverall => Rows.Count == 0 ? 0 : Rows.Sum(r => r.Current + r.Added) / 6;

        // The tier the card would be once saved, against the league's thresholds.
        private void UpdatePreview()
            => PreviewTier = Card?.Attributes == null ? null : TierFor(PreviewOverall);

        public async Task Save()
        {
            if (Card == null || Allocated == 0) return;

            Saving = true;
            Error = null;
            Saved = false;

            try
            {
                Card = await _cards.Allocate(new AllocatePointsRequest
                {
                    Finishing = Added(nameof(AllocatePointsRequest.Finishing)),
                    Tackling = Added(nameof(AllocatePointsRequest.Tackling)),
                    Passing = Added(nameof(AllocatePointsRequest.Passing)),
                    Dribbling = Added(nameof(AllocatePointsRequest.Dribbling)),
                    Pace = Added(nameof(AllocatePointsRequest.Pace)),
                    Strength = Added(nameof(AllocatePointsRequest.Strength))
                });

                BuildRows();
                Saved = true;

                var before = DisplayTier;
                var after = TierFor(Card!.Attributes.Overall);
                if (TierRank(after) > TierRank(before))
                    await PlayUpgrade(before ?? "bronze", after);
                else
                    DisplayTier = after;
            }
            catch (Exception ex)
            {
                // Never swallowed: a spend that quietly failed would look to the
                // player like their points had vanished.
                Error = ex.Message;
                await Load();
            }
            finally
            {
                Saving = false;
            }
        }

        private int Added(string name) => Rows.FirstOrDefault(r => r.Name == name)?.Added ?? 0;

        public static string Breakdown(PlayerPointsAwardDto a)
        {
            var parts = new List<string>();
            if (a.AppearancePoints > 0) parts.Add($"{a.AppearancePoints} played");
            if (a.WinPoints > 0) parts.Add($"{a.WinPoints} win");
            if (a.GoalPoints > 0) parts.Add($"{a.GoalPoints} goals");
            if (a.AssistPoints > 0) parts.Add($"{a.AssistPoints} assists");
            if (a.CleanSheetPoints > 0) parts.Add($"{a.CleanSheetPoints} clean sheet");
            if (a.MotmPoints > 0) parts.Add($"{a.MotmPoints} man of the match");
            return parts.Count == 0 ? "—" : string.Join(" · ", parts);
        }
    }
}

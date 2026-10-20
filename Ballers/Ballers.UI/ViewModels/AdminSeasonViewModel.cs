using Ballers.Models;
using Ballers.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for seasons: a wizard to create one (generated fixtures, or an
    /// imported CSV), and making one active, drawing its knockout or deleting it.
    /// The page asks for confirmation before calling the season actions.
    /// </summary>
    public class AdminSeasonViewModel : ViewModelBase
    {
        public static readonly string[] StepLabels = { "Method", "Details", "Review" };

        private readonly AdminService _admin;
        private readonly KnockoutService _knockout;

        public AdminSeasonViewModel(AdminService admin, KnockoutService knockout)
        {
            _admin = admin;
            _knockout = knockout;
        }

        public List<SeasonDto> Seasons { get; private set; } = new();
        public List<TeamDto> Teams { get; private set; } = new();
        public bool Loading { get; private set; } = true;
        public string? SeasonMessage { get; private set; }
        public bool SeasonIsError { get; private set; }
        public int DeleteId { get; set; }

        // Wizard
        public bool WizardOpen { get; private set; }
        public int Step { get; set; }
        public string Method { get; set; } = "generate";
        public HashSet<int> GenTeams { get; private set; } = new();
        public DateTime GenStart { get; set; } = DateTime.Today;
        public int ImportNumber { get; set; } = 1;
        public DateTime ImportStart { get; set; } = DateTime.Today;
        public bool ImportActive { get; set; }
        public IBrowserFile? ImportFile { get; private set; }
        public string? ImportPreview { get; private set; }
        public int ImportRows { get; private set; }
        public bool Creating { get; private set; }
        public string? WizardMessage { get; private set; }
        public bool WizardIsError { get; private set; }
        public List<string> ImportErrors { get; private set; } = new();

        public int NextSeasonNumber => Seasons.Any() ? Seasons.Max(s => s.SeasonNumber) + 1 : 1;

        public SeasonDto? ActiveSeason => Seasons.FirstOrDefault(x => x.IsActive);

        public bool CanContinue => Step switch
        {
            0 => true,
            1 when Method == "generate" => GenTeams.Count >= 2,
            1 => ImportFile != null && ImportNumber >= 1,
            _ => false
        };

        public async Task InitializeAsync()
        {
            var seasonsTask = _admin.GetSeasons();
            var teamsTask = _admin.GetTeams();
            await Task.WhenAll(seasonsTask, teamsTask);
            Seasons = seasonsTask.Result;
            Teams = teamsTask.Result;
            Loading = false;
        }

        public void OpenWizard()
        {
            WizardOpen = true;
            Step = 0;
            Method = "generate";
            GenTeams = Teams.Select(t => t.Id).ToHashSet();
            GenStart = DateTime.Today;
            ImportNumber = NextSeasonNumber;
            ImportStart = DateTime.Today;
            ImportActive = false;
            ImportFile = null;
            ImportPreview = null;
            WizardMessage = null;
            ImportErrors.Clear();
        }

        public void CloseWizard() => WizardOpen = false;

        public void SelectAllTeams() => GenTeams = Teams.Select(t => t.Id).ToHashSet();

        public void ToggleTeam(int id, bool on)
        {
            if (on) GenTeams.Add(id); else GenTeams.Remove(id);
        }

        // Reads the file once to show what's in it before anything is created.
        public async Task SelectImportFile(IBrowserFile file)
        {
            ImportFile = file;
            ImportPreview = null;
            ImportRows = 0;
            try
            {
                using var reader = new StreamReader(file.OpenReadStream(5 * 1024 * 1024));
                var lines = (await reader.ReadToEndAsync())
                    .Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                ImportRows = Math.Max(0, lines.Count - 1);
                ImportPreview = string.Join("\n", lines.Take(6));
            }
            catch (Exception ex)
            {
                ImportPreview = $"Couldn't read the file: {ex.Message}";
            }
        }

        public async Task Create()
        {
            Creating = true;
            WizardMessage = null;
            ImportErrors.Clear();
            try
            {
                if (Method == "generate")
                {
                    await _admin.GenerateFixtures(GenTeams.ToList(), GenStart);
                    WizardMessage = $"Season {NextSeasonNumber} created. Make it active when you're ready.";
                }
                else
                {
                    // The file was read for the preview; Blazor lets it be read again.
                    var (created, errors) = await _admin.ImportFixtures(ImportFile!, ImportNumber, ImportStart, ImportActive);
                    if (errors.Any())
                    {
                        WizardIsError = true;
                        WizardMessage = "The import didn't go through. Fix these rows and try again:";
                        ImportErrors = errors;
                        return;
                    }
                    WizardMessage = $"Imported {created} fixtures into Season {ImportNumber}.";
                }
                WizardIsError = false;
                Seasons = await _admin.GetSeasons();
                WizardOpen = false;
                SeasonIsError = false;
                SeasonMessage = WizardMessage;
            }
            catch (Exception ex)
            {
                WizardIsError = true;
                WizardMessage = ex.Message;
            }
            finally
            {
                Creating = false;
            }
        }

        public Task ActivateSeason(SeasonDto s) => Run(async () =>
        {
            await _admin.ActivateSeason(s.Id);
            return $"Season {s.SeasonNumber} is now active.";
        });

        public Task DrawKnockout(SeasonDto s) => Run(async () =>
        {
            await _knockout.GenerateKnockoutAsync(s.Id);
            return $"Season {s.SeasonNumber} knockout drawn.";
        });

        public SeasonDto? SeasonToDelete => Seasons.FirstOrDefault(x => x.Id == DeleteId);

        public Task DeleteSeason(SeasonDto s) => Run(async () =>
        {
            await _admin.DeleteSeason(s.Id);
            DeleteId = 0;
            return $"Season {s.SeasonNumber} deleted.";
        });

        private async Task Run(Func<Task<string>> action)
        {
            SeasonMessage = null;
            try
            {
                SeasonMessage = await action();
                SeasonIsError = false;
                Seasons = await _admin.GetSeasons();
            }
            catch (Exception ex)
            {
                SeasonIsError = true;
                SeasonMessage = ex.Message;
            }
        }
    }
}

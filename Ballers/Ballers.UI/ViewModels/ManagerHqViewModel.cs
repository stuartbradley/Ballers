using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The manager's HQ: their to-do list, next match, recent results, and the
    /// players waiting to join their squad.
    /// </summary>
    public class ManagerHqViewModel : ViewModelBase
    {
        private readonly ManagerService _manager;
        private readonly PlayerAccountService _accounts;

        public ManagerHqViewModel(ManagerService manager, PlayerAccountService accounts)
        {
            _manager = manager;
            _accounts = accounts;
        }

        public ManagerDashboardDto? Hq { get; private set; }
        public bool Loading { get; private set; } = true;
        public List<PlayerAccountRequestDto> Signups { get; private set; } = new();
        // Sign-ups with an approve or decline in flight.
        public HashSet<int> Busy { get; } = new();
        public string? SignupMessage { get; private set; }
        public bool SignupIsError { get; private set; }

        public async Task InitializeAsync()
        {
            var hqTask = _manager.GetDashboard();
            var signupTask = _accounts.GetRequests();
            await Task.WhenAll(hqTask, signupTask);
            Hq = hqTask.Result;
            Signups = signupTask.Result.Where(r => r.Status == PlayerAccountRequestStatusDto.Pending).ToList();
            Loading = false;
        }

        public async Task Resolve(PlayerAccountRequestDto r, bool approve)
        {
            Busy.Add(r.Id);
            SignupMessage = null;
            try
            {
                if (approve) await _accounts.Approve(r.Id); else await _accounts.Reject(r.Id);
                Signups.Remove(r);
                SignupIsError = false;
                SignupMessage = approve ? $"🎉 {r.RequestedName} is in! Their card's ready." : $"{r.RequestedName} declined.";

                // The to-do list counts these; refresh it.
                Hq = await _manager.GetDashboard() ?? Hq;
            }
            catch (Exception ex)
            {
                SignupIsError = true;
                SignupMessage = ex.Message;
            }
            finally
            {
                Busy.Remove(r.Id);
            }
        }

        public static string BubbleText(ManagerDashboardDto h, List<ManagerTodoDto> todo)
        {
            var urgent = todo.Count(t => t.Urgent);
            if (urgent > 0)
                return $"📣 {urgent} urgent job{(urgent == 1 ? "" : "s")}, gaffer! Sort {(urgent == 1 ? "it" : "them")} first.";
            if (todo.Count > 0)
                return $"📋 {todo.Count} thing{(todo.Count == 1 ? "" : "s")} on your list{(h.NextMatch != null ? $" before {h.NextMatch.Opponent}" : "")}.";
            return "🙌 All sorted, gaffer! Nothing on your list right now.";
        }

        public static string Countdown(ManagerMatchDto m)
        {
            var when = m.Kickoff ?? m.WindowStart;
            var days = (when.ToLocalTime().Date - DateTime.Now.Date).Days;
            return days switch
            {
                <= 0 when m.Kickoff != null => "⏰ It's matchday!",
                <= 0 => "⏰ This week!",
                1 => "⏰ Tomorrow!",
                < 7 => $"⏰ {days} days to go",
                _ => $"⏰ {days} days to go. Plenty of time."
            };
        }
    }
}

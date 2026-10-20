namespace Ballers.ViewModels
{
    /// <summary>
    /// A page's state and behaviour, kept out of the markup so it can be tested
    /// without rendering. The page re-renders whenever <see cref="Changed"/>
    /// fires; Blazor already re-renders after an event handler finishes, so a
    /// view model only needs to call <see cref="NotifyChanged"/> when its state
    /// moves on part way through work (a load landing, a toast clearing).
    /// </summary>
    public abstract class ViewModelBase
    {
        public event Action? Changed;

        protected void NotifyChanged() => Changed?.Invoke();

        // ── Toast ──
        // Most pages flash a short message after a save; it lives here so every
        // view model shows it the same way.

        public bool ToastVisible { get; private set; }
        public bool ToastSuccess { get; private set; } = true;
        public string ToastMessage { get; private set; } = "";

        // How long a toast stays up.
        public TimeSpan ToastDuration { get; set; } = TimeSpan.FromSeconds(3);

        // Every pause a view model takes (a toast, an animation step) goes through
        // here, so tests can skip the waiting.
        public Func<TimeSpan, Task> Delay { get; set; } = Task.Delay;

        public async Task ShowToast(string message, bool success = true)
        {
            ToastMessage = message;
            ToastSuccess = success;
            ToastVisible = true;
            NotifyChanged();
            await Delay(ToastDuration);
            ToastVisible = false;
            NotifyChanged();
        }
    }
}

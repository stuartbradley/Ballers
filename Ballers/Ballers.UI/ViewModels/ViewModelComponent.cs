using Microsoft.AspNetCore.Components;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Base for a page or component backed by a view model: injects a fresh
    /// <typeparamref name="TViewModel"/> and re-renders whenever it changes.
    /// Use with <c>@inherits ViewModelComponent&lt;FooViewModel&gt;</c> and read
    /// state from <c>Vm</c> in the markup. A page that overrides
    /// <see cref="OnInitialized"/> must call the base.
    /// </summary>
    public abstract class ViewModelComponent<TViewModel> : ComponentBase, IDisposable
        where TViewModel : ViewModelBase
    {
        [Inject] protected TViewModel Vm { get; set; } = default!;

        protected override void OnInitialized() => Vm.Changed += OnViewModelChanged;

        private void OnViewModelChanged() => InvokeAsync(StateHasChanged);

        public virtual void Dispose()
        {
            Vm.Changed -= OnViewModelChanged;
            GC.SuppressFinalize(this);
        }
    }
}

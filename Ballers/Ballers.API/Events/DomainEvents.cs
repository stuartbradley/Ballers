namespace Ballers.API.Events
{
    /// <summary>Something that happened to an entity that other parts of the app react to.</summary>
    public interface IDomainEvent { }

    /// <summary>
    /// An entity that records events as it changes. They are handed out once
    /// the change is saved, so nothing reacts to a change that never landed.
    /// </summary>
    public interface IHasDomainEvents
    {
        IReadOnlyList<IDomainEvent> TakeDomainEvents();
    }

    /// <summary>Base for entities that raise events; keeps the list out of the EF model.</summary>
    public abstract class EntityWithEvents : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _events = new();

        protected void Raise(IDomainEvent e) => _events.Add(e);

        public IReadOnlyList<IDomainEvent> TakeDomainEvents()
        {
            var taken = _events.ToList();
            _events.Clear();
            return taken;
        }
    }

    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        Task HandleAsync(TEvent e, CancellationToken ct);
    }

    public interface IDomainEventDispatcher
    {
        Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    }

    /// <summary>
    /// Runs every registered handler for each event, in the caller's scope so
    /// handlers share its DbContext. Handlers are looked up when an event is
    /// published rather than injected, because they themselves depend on the
    /// DbContext that depends on this dispatcher.
    /// </summary>
    public sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider _services;

        public DomainEventDispatcher(IServiceProvider services) => _services = services;

        public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
        {
            foreach (var e in events)
            {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(e.GetType());
                var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

                foreach (var handler in _services.GetServices(handlerType))
                    await (Task)handle.Invoke(handler, [e, ct])!;
            }
        }
    }
}

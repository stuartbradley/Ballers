namespace Ballers.API.Events
{
    public static class DomainEventsSetup
    {
        /// <summary>
        /// Registers the dispatcher and every handler in this assembly. One class
        /// can handle several events; each of its interfaces resolves to the same
        /// scoped instance.
        /// </summary>
        public static IServiceCollection AddDomainEvents(this IServiceCollection services)
        {
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            var handlers = typeof(DomainEventsSetup).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false });

            foreach (var type in handlers)
            {
                var handles = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                    .ToList();
                if (handles.Count == 0) continue;

                services.AddScoped(type);
                foreach (var handlerInterface in handles)
                    services.AddScoped(handlerInterface, sp => sp.GetRequiredService(type));
            }

            return services;
        }
    }
}

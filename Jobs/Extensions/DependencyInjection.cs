using System.Reflection;
using Jobs.Abstractions;
using Jobs.BackgroundServices;
using Jobs.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobs.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddJobQueue(
        this IServiceCollection services,
        Action<QueueOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new QueueOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);

        services.TryAddSingleton(serviceProvider => new JobChannelStore(serviceProvider.GetServices<QueueOptions>()));
        services.TryAddSingleton<IJobChannelStore>(serviceProvider => serviceProvider.GetRequiredService<JobChannelStore>());
        services.TryAddSingleton<IJobBus>(serviceProvider => new JobBus(serviceProvider.GetRequiredService<JobChannelStore>()));

        // AddSingleton<IHostedService> instead of AddHostedService because AddHostedService
        // uses TryAddEnumerable internally (deduplicates by implementation type), which would
        // silently drop the second queue's worker when multiple queues are registered.
        services.AddSingleton<IHostedService>(serviceProvider => new QueueHostedService(
            options,
            serviceProvider.GetRequiredService<JobChannelStore>(),
            new QueueWorker(
                options,
                serviceProvider.GetRequiredService<JobChannelStore>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                serviceProvider.GetRequiredService<ILogger<QueueWorker>>()
            )
        ));

        return services;
    }

    public static IServiceCollection AddJobHandlersFromAssemblyContainingType<TType>(
        this IServiceCollection services
    ) => services.AddJobHandlersFromAssemblyContainingType(typeof(TType));

    public static IServiceCollection AddJobHandlersFromAssemblyContainingType(
        this IServiceCollection services,
        Type type
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        return services.AddJobHandlersFromAssemblies(type.Assembly);
    }

    public static IServiceCollection AddJobHandlersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies
    )
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var openHandlerType = typeof(IJobHandler<>);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != openHandlerType)
                    {
                        continue;
                    }

                    services.TryAddScoped(iface, type);
                }
            }
        }

        return services;
    }
}

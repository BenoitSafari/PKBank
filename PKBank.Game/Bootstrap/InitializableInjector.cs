using Microsoft.Extensions.DependencyInjection;

namespace PKBank.Game.Bootstrap;

public static class InitializableInjector
{
    public static IServiceCollection AddInitializable<T>(this IServiceCollection services)
        where T : class, IInitializable
    {
        services.AddSingleton<T>();
        services.AddSingleton<IInitializable>(sp => sp.GetRequiredService<T>());
        return services;
    }

    public static IServiceProvider UseInitializable(this IServiceProvider services)
    {
        foreach (var service in services.GetServices<IInitializable>())
            service.Initialize();

        return services;
    }
}

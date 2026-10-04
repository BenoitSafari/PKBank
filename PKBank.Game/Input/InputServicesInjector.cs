using Microsoft.Extensions.DependencyInjection;
using PKBank.Game.Bootstrap;

namespace PKBank.Game.Input;

public static class InputServicesInjector
{
    public static IServiceCollection AddInputServices(this IServiceCollection services) =>
        services
            .AddSingleton<InputEvents>()
            .AddInitializable<InputManager>();
}

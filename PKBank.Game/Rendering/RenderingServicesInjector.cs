using Microsoft.Extensions.DependencyInjection;
using PKBank.Game.Bootstrap;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace PKBank.Game.Rendering;

public static class RenderingServicesInjector
{
    // GL is created lazily on first resolve: it must only be resolved after the window Load event.
    public static IServiceCollection AddRenderingServices(this IServiceCollection services) =>
        services
            .AddSingleton(sp => sp.GetRequiredService<IWindow>().CreateOpenGL())
            .AddInitializable<Renderer>();
}

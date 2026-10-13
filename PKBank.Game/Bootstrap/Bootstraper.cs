using Microsoft.Extensions.DependencyInjection;
using PKBank.Game.Input;
using PKBank.Game.Rendering;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace PKBank.Game.Bootstrap;

public static class Bootstrapper
{
    public static void UseSdl()
    {
        SdlWindowing.Use();
        SdlInput.Use();
    }

    public static IWindow BuildWindow()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1920, 1080),
            Title = "PKBank"
        };
        return Window.Create(options);
    }

    public static ServiceProvider BuildServiceProvider(IWindow window) =>
        new ServiceCollection()
            .AddSingleton(window)
            .AddInputServices()
            .AddRenderingServices()
            .BuildServiceProvider();
}

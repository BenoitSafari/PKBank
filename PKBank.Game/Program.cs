using Microsoft.Extensions.DependencyInjection;
using PKBank.Game.Bootstrap;
using PKBank.Game.Input;
using PKBank.Game.Logging;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace PKBank.Game;

public class Program
{
    private static ServiceProvider _services;

    public static void Main(string[] args)
    {
        Logger.Trace($"App started with args: {args}");

        SdlWindowing.Use();
        SdlInput.Use();
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1920, 1080),
            Title = "PKBank"
        };

        var window = Window.Create(options);

        _services = new ServiceCollection()
            .AddSingleton(window)
            .AddInputServices()
            .BuildServiceProvider();

        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Run();
    }

    private static void OnLoad() => _services.UseInitializable();

    private static void OnUpdate(double deltaTime)
    {
    }

    private static void OnRender(double deltaTime)
    {
    }
}

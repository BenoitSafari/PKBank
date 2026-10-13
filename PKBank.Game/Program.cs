using Microsoft.Extensions.DependencyInjection;
using PKBank.Game.Bootstrap;
using PKBank.Game.Logging;
using Silk.NET.Windowing;

namespace PKBank.Game;

public class Program
{
    private static ServiceProvider _services;
    private static IWindow _window;

    public static void Main(string[] args)
    {
        Logger.Trace($"App started with args: {args}");

        Bootstrapper.UseSdl();
        _window = Bootstrapper.BuildWindow();
        _services = Bootstrapper.BuildServiceProvider(_window);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Run();
    }

    private static void OnLoad() => _services.UseInitializable();

    private static void OnUpdate(double deltaTime)
    {
    }
}

using System.Drawing;
using PKBank.Game.Bootstrap;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace PKBank.Game.Rendering;

public class Renderer(
    IWindow window,
    GL gl
) : IInitializable
{
    private Mesh? _quad;

    public void Initialize()
    {
        gl.ClearColor(Color.CornflowerBlue);

        _quad = new Mesh(
            gl,
            [
                0.5f, 0.5f, 0.0f,
                0.5f, -0.5f, 0.0f,
                -0.5f, -0.5f, 0.0f,
                -0.5f, 0.5f, 0.0f
            ],
            [
                0u, 1u, 3u,
                1u, 2u, 3u
            ]
        );

        window.Render += OnRender;
        window.Closing += OnClosing;
    }

    // TODO: call _quad.Draw() once a shader program is bound.
    private void OnRender(double deltaTime) => gl.Clear(ClearBufferMask.ColorBufferBit);

    // GL resources must be released while the context is still alive.
    private void OnClosing()
    {
        window.Render -= OnRender;
        window.Closing -= OnClosing;
        _quad?.Dispose();
        _quad = null;
    }
}

using Silk.NET.OpenGL;

namespace PKBank.Game.Rendering;

public class Mesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly uint _indexCount;

    public unsafe Mesh(GL gl, float[] vertices, uint[] indices)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;

        // The VAO must be bound first: it records the EBO binding and the attribute layout below.
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* buf = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                buf,
                BufferUsageARB.StaticDraw
            );
        }

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* buf = indices)
        {
            _gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                buf,
                BufferUsageARB.StaticDraw
            );
        }

        // Attribute 0: vertex position (3 floats per vertex).
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);

        // Unbind the VAO first so the EBO stays attached to it.
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
    }
}

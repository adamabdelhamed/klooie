using Veldrid;

namespace klooie;

public interface IShape3D : IDisposable
{
    void EnsureResources(GraphicsDevice gd, ResourceFactory factory);
    DeviceBuffer VertexBuffer { get; }
    DeviceBuffer IndexBuffer { get; }
    IndexFormat IndexFormat { get; }
    uint IndexCount { get; }
    VertexLayoutDescription VertexLayout { get; }
}

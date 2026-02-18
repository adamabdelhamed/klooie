using System.Numerics;
using Veldrid;

namespace klooie;

public sealed class Cuboid : IShape3D
{
    private DeviceBuffer vb;
    private DeviceBuffer ib;
    private bool created;

    private readonly float width;
    private readonly float height;
    private readonly float depth;
    private readonly float rotationZ;

    private readonly struct ShapeVertex
    {
        public const uint SizeInBytes = 24;
        public readonly Vector3 Pos;
        public readonly Vector3 Nrm;

        public ShapeVertex(Vector3 pos, Vector3 nrm)
        {
            Pos = pos;
            Nrm = nrm;
        }
    }

    public Cuboid(float width, float height, float depth, float rotationZ = 0f)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.rotationZ = rotationZ;
    }

    public VertexLayoutDescription VertexLayout => new VertexLayoutDescription(
        new VertexElementDescription("Pos", VertexElementSemantic.Position, VertexElementFormat.Float3),
        new VertexElementDescription("Nrm", VertexElementSemantic.Normal, VertexElementFormat.Float3))
    {
        Stride = ShapeVertex.SizeInBytes,
        InstanceStepRate = 0
    };

    public DeviceBuffer VertexBuffer => vb;
    public DeviceBuffer IndexBuffer => ib;
    public IndexFormat IndexFormat => IndexFormat.UInt16;
    public uint IndexCount => 36;

    public void EnsureResources(GraphicsDevice gd, ResourceFactory factory)
    {
        if (created) return;
        created = true;

        var hx = width * 0.5f;
        var hy = height * 0.5f;
        var hz = depth * 0.5f;

        var p000 = new Vector3(-hx, -hy, -hz);
        var p001 = new Vector3(-hx, -hy, hz);
        var p010 = new Vector3(-hx, hy, -hz);
        var p011 = new Vector3(-hx, hy, hz);
        var p100 = new Vector3(hx, -hy, -hz);
        var p101 = new Vector3(hx, -hy, hz);
        var p110 = new Vector3(hx, hy, -hz);
        var p111 = new Vector3(hx, hy, hz);

        var nx = new Vector3(-1, 0, 0);
        var px = new Vector3(1, 0, 0);
        var ny = new Vector3(0, -1, 0);
        var py = new Vector3(0, 1, 0);
        var nz = new Vector3(0, 0, -1);
        var pz = new Vector3(0, 0, 1);

        var verts = new ShapeVertex[]
        {
            new(p000, nx), new(p001, nx), new(p011, nx), new(p010, nx),
            new(p100, px), new(p110, px), new(p111, px), new(p101, px),
            new(p000, ny), new(p100, ny), new(p101, ny), new(p001, ny),
            new(p010, py), new(p011, py), new(p111, py), new(p110, py),
            new(p000, nz), new(p010, nz), new(p110, nz), new(p100, nz),
            new(p001, pz), new(p101, pz), new(p111, pz), new(p011, pz),
        };

        if (Math.Abs(rotationZ) > 0.0001f)
        {
            var rot = Matrix4x4.CreateRotationZ(rotationZ);
            for (var i = 0; i < verts.Length; i++)
            {
                verts[i] = new ShapeVertex(
                    Vector3.Transform(verts[i].Pos, rot),
                    Vector3.Normalize(Vector3.TransformNormal(verts[i].Nrm, rot)));
            }
        }

        var idx = new ushort[]
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
        };

        vb = factory.CreateBuffer(new BufferDescription((uint)(verts.Length * ShapeVertex.SizeInBytes), BufferUsage.VertexBuffer));
        ib = factory.CreateBuffer(new BufferDescription((uint)(idx.Length * sizeof(ushort)), BufferUsage.IndexBuffer));

        gd.UpdateBuffer(vb, 0, verts);
        gd.UpdateBuffer(ib, 0, idx);
    }

    public void Dispose()
    {
        vb?.Dispose(); vb = null;
        ib?.Dispose(); ib = null;
        created = false;
    }
}

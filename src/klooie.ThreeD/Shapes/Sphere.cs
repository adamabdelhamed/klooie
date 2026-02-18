using System.Numerics;
using Veldrid;

namespace klooie;

/// <summary>
/// A simple UV-sphere centered at origin. Radius is expressed in "shape-local" units,
/// and then the renderer applies its per-instance scale in the vertex shader.
/// </summary>
public sealed class Sphere : IShape3D
{
    private DeviceBuffer vb;
    private DeviceBuffer ib;
    private bool created;

    private readonly float radius;
    private readonly int slices;
    private readonly int stacks;
    private uint indexCount;

    private readonly struct ShapeVertex
    {
        public const uint SizeInBytes = 24;
        public readonly Vector3 Pos;
        public readonly Vector3 Nrm;
        public ShapeVertex(Vector3 pos, Vector3 nrm) { Pos = pos; Nrm = nrm; }
    }

    public Sphere(float radius, int slices = 16, int stacks = 12)
    {
        this.radius = radius;
        this.slices = Math.Max(3, slices);
        this.stacks = Math.Max(2, stacks);
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
    public uint IndexCount => indexCount;

    public void EnsureResources(GraphicsDevice gd, ResourceFactory factory)
    {
        if (created) return;
        created = true;

        // UV sphere: (stacks+1) rings, (slices+1) verts per ring (duplicate seam).
        // Indices: 2 triangles per quad.
        var vertCount = (stacks + 1) * (slices + 1);
        var verts = new ShapeVertex[vertCount];

        var vi = 0;
        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = (float)stack / stacks;                 // 0..1
            var phi = (v * MathF.PI) - (MathF.PI * 0.5f);  // -pi/2 .. +pi/2
            var cphi = MathF.Cos(phi);
            var sphi = MathF.Sin(phi);

            for (var slice = 0; slice <= slices; slice++)
            {
                var u = (float)slice / slices;             // 0..1
                var theta = u * MathF.Tau;                 // 0..2pi
                var cth = MathF.Cos(theta);
                var sth = MathF.Sin(theta);

                var n = new Vector3(cth * cphi, sth * cphi, sphi);
                var p = n * radius;
                verts[vi++] = new ShapeVertex(p, Vector3.Normalize(n));
            }
        }

        var quadCount = stacks * slices;
        var idx = new ushort[quadCount * 6];
        var ii = 0;

        for (var stack = 0; stack < stacks; stack++)
        {
            var row0 = stack * (slices + 1);
            var row1 = (stack + 1) * (slices + 1);

            for (var slice = 0; slice < slices; slice++)
            {
                var a = (ushort)(row0 + slice);
                var b = (ushort)(row1 + slice);
                var c = (ushort)(row1 + slice + 1);
                var d = (ushort)(row0 + slice + 1);

                idx[ii++] = a; idx[ii++] = b; idx[ii++] = c;
                idx[ii++] = a; idx[ii++] = c; idx[ii++] = d;
            }
        }

        indexCount = (uint)idx.Length;

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
        indexCount = 0;
    }
}

using System.Numerics;
using Veldrid;

namespace klooie;

public sealed class Torus : IShape3D
{
    private DeviceBuffer vb;
    private DeviceBuffer ib;
    private bool created;

    private readonly float majorRadius;
    private readonly float minorRadius;
    private readonly int majorSegments;
    private readonly int minorSegments;
    private readonly float yOffset;
    private uint indexCount;

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

    public Torus(float majorRadius, float minorRadius, int majorSegments = 24, int minorSegments = 16, float yOffset = 0f)
    {
        this.majorRadius = majorRadius;
        this.minorRadius = minorRadius;
        this.majorSegments = Math.Max(3, majorSegments);
        this.minorSegments = Math.Max(3, minorSegments);
        this.yOffset = yOffset;
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

        var verts = new ShapeVertex[(majorSegments + 1) * (minorSegments + 1)];
        var vi = 0;

        for (var major = 0; major <= majorSegments; major++)
        {
            var u = (float)major / majorSegments;
            var theta = u * MathF.Tau;
            var cosT = MathF.Cos(theta);
            var sinT = MathF.Sin(theta);

            for (var minor = 0; minor <= minorSegments; minor++)
            {
                var v = (float)minor / minorSegments;
                var phi = v * MathF.Tau;
                var cosP = MathF.Cos(phi);
                var sinP = MathF.Sin(phi);

                var radial = majorRadius + minorRadius * cosP;

                // NOTE:
                // pos.Y is the "up/down" axis in the ring plane.
                // If your cell-local +Y is UP, then subtracting yOffset moves it DOWN.
                // If your cell-local +Y is DOWN, flip the sign here.
                var pos = new Vector3(radial * cosT, radial * sinT - yOffset, minorRadius * sinP);

                var nrm = Vector3.Normalize(new Vector3(cosP * cosT, cosP * sinT, sinP));
                verts[vi++] = new ShapeVertex(pos, nrm);
            }
        }

        var idx = new ushort[majorSegments * minorSegments * 6];
        var ii = 0;

        for (var major = 0; major < majorSegments; major++)
        {
            var row0 = major * (minorSegments + 1);
            var row1 = (major + 1) * (minorSegments + 1);
            for (var minor = 0; minor < minorSegments; minor++)
            {
                var a = (ushort)(row0 + minor);
                var b = (ushort)(row1 + minor);
                var c = (ushort)(row1 + minor + 1);
                var d = (ushort)(row0 + minor + 1);

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
        indexCount = 0;
        created = false;
    }
}

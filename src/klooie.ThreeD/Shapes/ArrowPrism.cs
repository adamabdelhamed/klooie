using System.Numerics;
using Veldrid;

namespace klooie;

public sealed class ArrowPrism : IShape3D
{
    private DeviceBuffer vb;
    private DeviceBuffer ib;
    private bool created;
    private uint indexCount;

    private readonly float rotationZ;
    private readonly float depth;

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

    public ArrowPrism(float rotationZ, float depth = 0.20f)
    {
        this.rotationZ = rotationZ;
        this.depth = depth;
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

        var frontZ = depth * 0.5f;
        var backZ = -frontZ;

        // Notched arrow: rectangular shaft + concave head (matches your ASCII)
        const float shaftHalfW = 0.15f;
        const float headHalfW = 0.60f;
        const float yBottom = -0.52f;
        const float yHeadBase = 0.18f;  // outer base of the head (lower)
        const float yNeck = 0.20f;      // shaft continues into head (higher) => notch
        const float yTip = 0.7f;

        var points = new Vector2[]
        {
            new(-shaftHalfW, yBottom),
            new( shaftHalfW, yBottom),
            new( shaftHalfW, yNeck),       // shaft up into head (notch)
            new( headHalfW,  yHeadBase),   // step out to head width, but lower => concave corner
            new( 0.00f,      yTip),        // tip
            new(-headHalfW,  yHeadBase),
            new(-shaftHalfW, yNeck),
        };

        static float SignedArea(Vector2[] p)
        {
            float a = 0;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                a += (p[j].X * p[i].Y) - (p[i].X * p[j].Y);
            return a * 0.5f;
        }

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // barycentric (no switches)
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            var dot00 = Vector2.Dot(v0, v0);
            var dot01 = Vector2.Dot(v0, v1);
            var dot02 = Vector2.Dot(v0, v2);
            var dot11 = Vector2.Dot(v1, v1);
            var dot12 = Vector2.Dot(v1, v2);

            var invDen = 1f / (dot00 * dot11 - dot01 * dot01);
            var u = (dot11 * dot02 - dot01 * dot12) * invDen;
            var v = (dot00 * dot12 - dot01 * dot02) * invDen;

            return u >= 0 && v >= 0 && (u + v) <= 1;
        }

        static List<ushort> EarClip(Vector2[] poly)
        {
            // Ensure CCW
            if (SignedArea(poly) < 0) Array.Reverse(poly);

            var remaining = new List<int>(poly.Length);
            for (int i = 0; i < poly.Length; i++) remaining.Add(i);

            var tris = new List<ushort>((poly.Length - 2) * 3);

            bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
            {
                var ab = b - a;
                var bc = c - b;
                return (ab.X * bc.Y - ab.Y * bc.X) > 0;
            }

            int guard = 0;
            while (remaining.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    int i0 = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int i1 = remaining[i];
                    int i2 = remaining[(i + 1) % remaining.Count];

                    var a = poly[i0];
                    var b = poly[i1];
                    var c = poly[i2];

                    if (!IsConvex(a, b, c)) continue;

                    bool anyInside = false;
                    for (int k = 0; k < remaining.Count; k++)
                    {
                        int ik = remaining[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (PointInTri(poly[ik], a, b, c)) { anyInside = true; break; }
                    }
                    if (anyInside) continue;

                    tris.Add((ushort)i0);
                    tris.Add((ushort)i1);
                    tris.Add((ushort)i2);

                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped) break; // malformed/self-intersecting polygon
            }

            if (remaining.Count == 3)
            {
                tris.Add((ushort)remaining[0]);
                tris.Add((ushort)remaining[1]);
                tris.Add((ushort)remaining[2]);
            }

            return tris;
        }

        var verts = new List<ShapeVertex>(points.Length * 6);
        var idx = new List<ushort>();

        // Front face verts
        var frontStart = (ushort)verts.Count;
        for (var i = 0; i < points.Length; i++)
            verts.Add(new ShapeVertex(new Vector3(points[i], frontZ), Vector3.UnitZ));

        // Concave triangulation for front
        var frontTris = EarClip((Vector2[])points.Clone());
        for (int t = 0; t < frontTris.Count; t += 3)
        {
            idx.Add((ushort)(frontStart + frontTris[t + 0]));
            idx.Add((ushort)(frontStart + frontTris[t + 1]));
            idx.Add((ushort)(frontStart + frontTris[t + 2]));
        }

        // Back face verts
        var backStart = (ushort)verts.Count;
        for (var i = 0; i < points.Length; i++)
            verts.Add(new ShapeVertex(new Vector3(points[i], backZ), -Vector3.UnitZ));

        // Same triangles, reversed winding for back
        for (int t = 0; t < frontTris.Count; t += 3)
        {
            idx.Add((ushort)(backStart + frontTris[t + 0]));
            idx.Add((ushort)(backStart + frontTris[t + 2]));
            idx.Add((ushort)(backStart + frontTris[t + 1]));
        }

        // Side walls (unchanged)
        for (ushort i = 0; i < points.Length; i++)
        {
            var ni = (ushort)((i + 1) % points.Length);
            var edge = points[ni] - points[i];
            var nrm2 = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
            var nrm = new Vector3(nrm2, 0f);

            var sideStart = (ushort)verts.Count;
            verts.Add(new ShapeVertex(new Vector3(points[i], frontZ), nrm));
            verts.Add(new ShapeVertex(new Vector3(points[ni], frontZ), nrm));
            verts.Add(new ShapeVertex(new Vector3(points[ni], backZ), nrm));
            verts.Add(new ShapeVertex(new Vector3(points[i], backZ), nrm));

            idx.Add(sideStart);
            idx.Add((ushort)(sideStart + 1));
            idx.Add((ushort)(sideStart + 2));

            idx.Add(sideStart);
            idx.Add((ushort)(sideStart + 2));
            idx.Add((ushort)(sideStart + 3));
        }

        if (Math.Abs(rotationZ) > 0.0001f)
        {
            var rot = Matrix4x4.CreateRotationZ(rotationZ);
            for (var i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                verts[i] = new ShapeVertex(
                    Vector3.Transform(v.Pos, rot),
                    Vector3.Normalize(Vector3.TransformNormal(v.Nrm, rot)));
            }
        }

        indexCount = (uint)idx.Count;
        vb = factory.CreateBuffer(new BufferDescription((uint)(verts.Count * ShapeVertex.SizeInBytes), BufferUsage.VertexBuffer));
        ib = factory.CreateBuffer(new BufferDescription((uint)(idx.Count * sizeof(ushort)), BufferUsage.IndexBuffer));
        gd.UpdateBuffer(vb, 0, verts.ToArray());
        gd.UpdateBuffer(ib, 0, idx.ToArray());
    }

    public void Dispose()
    {
        vb?.Dispose(); vb = null;
        ib?.Dispose(); ib = null;
        indexCount = 0;
        created = false;
    }
}

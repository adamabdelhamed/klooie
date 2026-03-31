using klooie;
using PowerArgs;
using klooie.Gaming;
using System.Numerics;
using Veldrid;

namespace klooie;

public sealed partial class CellInstancedRenderer
{
    private DeviceBuffer ownerOffsetBuffer;    // vec2[maxOwnerId+1]
    private Vector2[] ownerOffsets = Array.Empty<Vector2>();
    private DeviceBuffer flatInstanceBuffer; // GPU buffer
    private DeviceBuffer threeDInstanceBuffer; // GPU buffer
    private FlatCellInstance[] flatInstances = Array.Empty<FlatCellInstance>(); // CPU staging

    private ThreeDCellInstance[] threeDInstances = Array.Empty<ThreeDCellInstance>(); // CPU staging only for now
    private int flatCount;
    private int threeDCount;
    private static uint PackRgb(RGB c) => (uint)(c.R | (c.G << 8) | (c.B << 16));

    private static int GetMaxOwnerId(int[] ownerIds, int cellCount)
    {
        var max = 0;
        for (int i = 0; i < cellCount; i++)
        {
            var v = ownerIds[i];
            if (v > max) max = v;
        }
        return max;
    }
    private void BuildOwnerOffsets(LayoutRootPanel root, int maxOwnerId)
    {
        Array.Clear(ownerOffsets, 0, maxOwnerId + 1);
        BuildOwnerOffsetsRecursive(root, maxOwnerId, Vector2.Zero, Vector2.Zero);
    }

    private void BuildOwnerOffsetsRecursive(Container parent, int maxOwnerId, Vector2 actualParentOrigin, Vector2 integerParentOrigin)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var control = parent.Children[i];
            var actualComposedOrigin = actualParentOrigin + GetActualRenderedOrigin(parent, control);
            var integerComposedOrigin = integerParentOrigin + GetIntegerRenderedOrigin(parent, control);
            var id = LayoutRootPanel.GetIdForPresentation(control);
            if ((uint)id <= (uint)maxOwnerId)
            {
                ownerOffsets[id] = actualComposedOrigin - integerComposedOrigin;
            }

            if (control is Container childContainer)
            {
                BuildOwnerOffsetsRecursive(childContainer, maxOwnerId, actualComposedOrigin, integerComposedOrigin);
            }
        }
    }

    private static Vector2 GetActualRenderedOrigin(Container parent, ConsoleControl control)
    {
        if (parent is Camera camera)
        {
            return new Vector2(control.Bounds.Left - camera.CameraLocation.Left, control.Bounds.Top - camera.CameraLocation.Top);
        }

        return new Vector2(control.Bounds.Left, control.Bounds.Top);
    }

    private static Vector2 GetIntegerRenderedOrigin(Container parent, ConsoleControl control)
    {
        var integerPosition = parent.Transform(control);
        return new Vector2(integerPosition.X, integerPosition.Y);
    }


    private void BuildInstances(ReadOnlySpan<ConsoleCharacter> pixels, int[] ownerIds, int w, int h)
    {
        flatCount = 0;
        threeDCount = 0;

        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var idx = row + x;

                var p = pixels[idx];
                var ownerId = (uint)ownerIds[idx];

                var lane = laneSelector.Select(p.Value);
                var fg = PackRgb(p.ForegroundColor);
                var bg = PackRgb(p.BackgroundColor);

                if (lane == GlyphLane.ThreeD)
                {
                    var shapeId = (uint)ShapeRegistry.Instance.ResolveId(p.Value);
                    threeDInstances[threeDCount++] = new ThreeDCellInstance(new Vector2(x, y), ownerId, fg, shapeId);


                    // Still draw BG for this cell via flat lane using a transparent glyph.
                    var space = flatMapper.Map(' ');
                    flatInstances[flatCount++] = new FlatCellInstance(new Vector2(x, y), ownerId, PackGlyph(space), fg, bg);
                    continue;
                }

                // Flat lane: needs glyph atlas ref + color.
                var g = flatMapper.Map(p.Value);
                var glyphPacked = PackGlyph(g);

                flatInstances[flatCount++] = new FlatCellInstance(new Vector2(x, y), ownerId, glyphPacked, fg, bg);
            }
        }
    }




    private void EnsureOwnerOffsetsCapacity(int needed)
    {
        if (ownerOffsets.Length == needed) return;
        ownerOffsets = new Vector2[needed];
    }

    private void EnsureFlatCapacity(int needed)
    {
        if (flatInstances.Length >= needed) return;
        flatInstances = new FlatCellInstance[needed];
    }


    private void EnsureThreeDCapacity(int needed)
    {
        if (threeDInstances.Length >= needed) return;
        threeDInstances = new ThreeDCellInstance[needed];
    }

    private void EnsureFlatInstanceBufferSize(uint neededBytes)
    {
        if (flatInstanceBuffer.SizeInBytes >= neededBytes) return;
        flatInstanceBuffer.Dispose();
        flatInstanceBuffer = factory.CreateBuffer(new BufferDescription(NextPow2(neededBytes), BufferUsage.VertexBuffer));
    }

    private void EnsureThreeDInstanceBufferSize(uint neededBytes)
    {
        if (threeDInstanceBuffer != null && threeDInstanceBuffer.SizeInBytes >= neededBytes) return;
        threeDInstanceBuffer?.Dispose();
        threeDInstanceBuffer = factory.CreateBuffer(new BufferDescription(NextPow2(neededBytes), BufferUsage.VertexBuffer));
    }



    private void EnsureOwnerOffsetBufferSize(uint ownerCount)
    {
        var neededBytes = ownerCount * 8u;
        if (ownerOffsetBuffer.SizeInBytes >= neededBytes) return;

        ownerOffsetBuffer.Dispose();
        ownerOffsetBuffer = factory.CreateBuffer(new BufferDescription(NextPow2(neededBytes), BufferUsage.StructuredBufferReadOnly, 8));

        resourceSet?.Dispose();
        resourceSet = null;

        set3d?.Dispose();
        set3d = null;
    }

    private static uint NextPow2(uint v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v++;
        return Math.Max(256u, v);
    }
}

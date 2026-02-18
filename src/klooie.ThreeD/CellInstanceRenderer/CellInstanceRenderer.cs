using klooie;
using klooie.Gaming;
using PowerArgs;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Windows.Forms;
using Veldrid;
using Veldrid.SPIRV;
using VdTexture = Veldrid.Texture;
using VdTextureView = Veldrid.TextureView;

namespace klooie;

public sealed partial class CellInstancedRenderer : IDisposable
{
    private readonly IGlyphLaneSelector laneSelector;
    private readonly IFlatGlyphMapper flatMapper;

 
    private readonly GraphicsDevice gd;
    private readonly ResourceFactory factory;

    private readonly Sampler sampler;
    private readonly Sampler glyphSampler;
    private readonly ResourceLayout layout;
    private readonly Pipeline pipeline;

    private DeviceBuffer quadVb;
    private DeviceBuffer quadIb;


    private ResourceSet resourceSet;           // binds frame tex + sampler + cameraUbo + ownerOffsetBuffer

    private ControlTex frameTex;

 

    private readonly struct QuadVertex
    {
        public const uint SizeInBytes = 8;
        public readonly Vector2 Corner;
        public QuadVertex(Vector2 c) { Corner = c; }
    }

    private readonly struct FlatCellInstance
    {
        public const uint SizeInBytes = 20;
        public readonly Vector2 Cell;
        public readonly uint OwnerId;
        public readonly uint GlyphPacked;
        public readonly uint FgPacked;
        public FlatCellInstance(Vector2 cell, uint ownerId, uint glyphPacked, uint fgPacked)
        {
            Cell = cell;
            OwnerId = ownerId;
            GlyphPacked = glyphPacked;
            FgPacked = fgPacked;
        }
    }

    private readonly struct ThreeDCellInstance
    {
        public const uint SizeInBytes = 20; // float2 + uint + uint + uint
        public readonly Vector2 Cell;
        public readonly uint OwnerId;
        public readonly uint FgPacked;
        public readonly uint ShapeId;

        public ThreeDCellInstance(Vector2 cell, uint ownerId, uint fgPacked, uint shapeId)
        {
            Cell = cell;
            OwnerId = ownerId;
            FgPacked = fgPacked;
            ShapeId = shapeId;
        }
    }

    private long wallClockNow;
    public CellInstancedRenderer(GraphicsDevice gd, IGlyphLaneSelector? laneSelector = null, IFlatGlyphMapper? flatMapper = null)
    {
        wallClockNow = Stopwatch.GetTimestamp();
        this.gd = gd;
        this.laneSelector = laneSelector ?? DefaultGlyphLaneSelector.Instance;
        this.flatMapper = flatMapper ?? Ascii256GlyphMapper.Instance;
        factory = gd.ResourceFactory;

        sampler = factory.CreateSampler(new SamplerDescription(
            SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerAddressMode.Clamp,
            SamplerFilter.MinPoint_MagPoint_MipPoint,
            comparisonKind: null,
            maximumAnisotropy: 0,
            minimumLod: 0,
            maximumLod: 0,
            lodBias: 0,
            borderColor: SamplerBorderColor.TransparentBlack));

        glyphSampler = factory.CreateSampler(new SamplerDescription(
    SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerAddressMode.Clamp,
    SamplerFilter.MinLinear_MagLinear_MipPoint,
    comparisonKind: null,
    maximumAnisotropy: 0,
    minimumLod: 0,
    maximumLod: 0,
    lodBias: 0,
    borderColor: SamplerBorderColor.TransparentBlack));


        layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
          new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
          new ResourceLayoutElementDescription("Samp", ResourceKind.Sampler, ShaderStages.Fragment),
          new ResourceLayoutElementDescription("Camera", ResourceKind.UniformBuffer, ShaderStages.Vertex),
          new ResourceLayoutElementDescription("Offsets", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex),
          new ResourceLayoutElementDescription("GlyphTex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
          new ResourceLayoutElementDescription("GlyphSamp", ResourceKind.Sampler, ShaderStages.Fragment),
          new ResourceLayoutElementDescription("Glyph", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

        glyphUbo = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));



        glyphAtlas = new GlyphAtlasTex(gd, factory, cellW: 16, supersample: 3);
        pipeline = CreatePipeline(layout);

        quadVb = factory.CreateBuffer(new BufferDescription(4 * QuadVertex.SizeInBytes, BufferUsage.VertexBuffer));
        quadIb = factory.CreateBuffer(new BufferDescription(6 * sizeof(ushort), BufferUsage.IndexBuffer));

        gd.UpdateBuffer(quadVb, 0, new[]
        {
            new QuadVertex(new Vector2(0,0)),
            new QuadVertex(new Vector2(1,0)),
            new QuadVertex(new Vector2(1,1)),
            new QuadVertex(new Vector2(0,1)),
        });

        gd.UpdateBuffer(quadIb, 0, new ushort[] { 0, 1, 2, 0, 2, 3 });

        flatInstanceBuffer = factory.CreateBuffer(new BufferDescription(1024 * 1024, BufferUsage.VertexBuffer));

        ownerOffsetBuffer = factory.CreateBuffer(new BufferDescription(1024 * 16, BufferUsage.StructuredBufferReadOnly, 8));
    }

    public void Draw(CommandList cl, LayoutRootPanel root, ConsoleBitmap bitmap, int[] ownerIds, int windowW, int windowH, int fbW, int fbH)
    {
        if (bitmap == null) return;

        var w = Math.Max(1, bitmap.Width);
        var h = Math.Max(1, bitmap.Height);
        var cellCount = checked(w * h);

        // ConsoleBitmap uses ArrayPool so internal Pixels.Length can be >= cellCount.
        // We just need at least cellCount and we must slice to cellCount.
        var pixels = bitmap.GetPixelsSpan();
        if (pixels.Length < cellCount) return;

        if (ownerIds == null || ownerIds.Length < cellCount) return;

        var zoom = MathF.Max(0.01f, VeldridTerminalHost.BoardZoom);
        var effectiveCellWpx = VeldridTerminalHost.CellPxWidth * zoom;
        var effectiveCellHpx = (VeldridTerminalHost.CellPxWidth * 2) * zoom;

        // Keep glyph atlas sizing stable; zoom is applied through viewport/camera sizing.
        var cellWpx = VeldridTerminalHost.CellPxWidth;

        var vpW = Math.Min(fbW, Math.Max(1, (int)MathF.Round(w * effectiveCellWpx)));
        var vpH = Math.Min(fbH, Math.Max(1, (int)MathF.Round(h * effectiveCellHpx)));

        var vpX = (fbW - vpW) / 2;
        var vpY = (fbH - vpH) / 2;

        cl.SetViewport(0, new Veldrid.Viewport((float)vpX, (float)vpY, (float)vpW, (float)vpH, 0f, 1f));
        cl.SetScissorRect(0, (uint)vpX, (uint)vpY, (uint)vpW, (uint)vpH);

        EnsureGlyphAtlas(cellWpx);
        UpdateGlyphUbo();

        EnsureFrameTexture(w, h);

        // CHANGED: Upload from span (no intermediate array)
        frameTex.UploadInk(pixels.Slice(0, cellCount), w, h);

        // Build unified camera (shared by flat and 3D)
        var alignment = BuildBoardAlignment(w, h);
        
        // Ensure camera UBO is created (done in Ensure3dDebugResources)
        Ensure3dDebugResources();
        var t =  (float)(Game.Current?.MainColliderGroup.ScaledNow.TotalSeconds ?? Stopwatch.GetElapsedTime(wallClockNow).TotalSeconds);

        // Tunables
        var shapeScale = 1f;           // padding (fraction of cell-width)
        var zLift = 0.55f;               // lift (cell-width units)
        var rotAmpRadians = 0.375f;       // ~14 degrees; try 0.15..0.35

        var extras = new Vector4(t, shapeScale, zLift, rotAmpRadians);

        gd.UpdateBuffer(cameraUbo, 0, new CameraUniform(alignment.View, alignment.Proj, alignment.CellScale, alignment.BoardCells, extras));

        var maxOwnerId = GetMaxOwnerId(ownerIds, cellCount);
        EnsureOwnerOffsetsCapacity(maxOwnerId + 1);
    BuildOwnerOffsets(root, maxOwnerId);

        EnsureOwnerOffsetBufferSize((uint)(maxOwnerId + 1));
        gd.UpdateBuffer(ownerOffsetBuffer, 0, ownerOffsets);

        DrawBoardPass(cl, alignment);

        EnsureFlatCapacity(cellCount);
        EnsureThreeDCapacity(cellCount);

        // CHANGED: Build instances from spans
        BuildInstances(pixels.Slice(0, cellCount), ownerIds, w, h);

        EnsureFlatInstanceBufferSize((uint)(flatCount * FlatCellInstance.SizeInBytes));
        gd.UpdateBuffer(flatInstanceBuffer, 0, flatInstances);

        // EnsureResourceSet requires cameraUbo to exist (created by Ensure3dDebugResources above)
        EnsureResourceSet();

        cl.SetPipeline(pipeline);
        cl.SetGraphicsResourceSet(0, resourceSet);

        cl.SetVertexBuffer(0, quadVb);
        cl.SetVertexBuffer(1, flatInstanceBuffer);
        cl.SetIndexBuffer(quadIb, IndexFormat.UInt16);

        cl.DrawIndexed(6, (uint)flatCount, 0, 0, 0);

        Draw3dLaneShapes(cl, vpX, vpY, vpW, vpH);
    }



    private void EnsureResourceSet()
    {
        if (resourceSet != null) return;
        resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
            layout,
            frameTex.View,
            sampler,        // bg point sampler
            cameraUbo,      // shared camera UBO
            ownerOffsetBuffer,
            glyphAtlas.View,
            glyphSampler,   // glyph linear sampler
            glyphUbo));

    }
 
    public void Dispose()
    {
        resourceSet?.Dispose();
        resourceSet = null;

        frameTex?.Dispose();
        frameTex = null;

        quadVb?.Dispose();
        quadIb?.Dispose();

        flatInstanceBuffer?.Dispose();
        threeDInstanceBuffer?.Dispose();
        ownerOffsetBuffer?.Dispose();

        pipeline?.Dispose();
        layout?.Dispose();
        sampler?.Dispose();

        glyphAtlas?.Dispose();
        glyphAtlas = null;

        glyphUbo?.Dispose();
        glyphSampler?.Dispose();
        Dispose3dDebug();
    }
}

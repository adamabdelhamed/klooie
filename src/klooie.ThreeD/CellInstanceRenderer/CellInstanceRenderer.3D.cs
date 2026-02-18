using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace klooie;

public sealed partial class CellInstancedRenderer
{
	private const float BoardPlaneZ = 0f;

    private Pipeline pipeline3d;
    private ResourceLayout layout3d;
    private ResourceSet set3d;
    private DeviceBuffer cameraUbo;  // Shared camera UBO for both flat and 3D
 
	private Pipeline boardPipeline3d;
	private ResourceLayout boardLayout3d;
	private ResourceSet boardSet3d;
	private DeviceBuffer boardUbo3d;
	private DeviceBuffer boardVb;
	private DeviceBuffer boardIb;
	private const float ThreeDSsaaScale = 1.5f;
	private Veldrid.Texture threeDSsaaColorTex;
	private TextureView threeDSsaaColorView;
	private Veldrid.Texture threeDSsaaDepthTex;
	private Framebuffer threeDSsaaFramebuffer;
	private Sampler threeDSsaaSampler;
	private ResourceLayout threeDSsaaCompositeLayout;
	private ResourceSet threeDSsaaCompositeSet;
	private Pipeline threeDSsaaCompositePipeline;
	private float threeDSsaaW;
	private float threeDSsaaH;
	private ThreeDCellInstance[] threeDUploadScratch = Array.Empty<ThreeDCellInstance>();

    private const int ThreeDShaderRevision = 5;
	private int threeDShaderRevisionBuilt = -1;

    private DeviceBuffer threeDSsaaCompositeUbo;

    private readonly struct FxaaUniform
    {
        public readonly Vector2 InvTexSize;
        public readonly float Enabled;     // 0 or 1
        public readonly float SpanMax;
        public readonly float ReduceMul;
        public readonly float ReduceMin;
        public readonly float LumaThreshold;
        public readonly float LumaThresholdMin;
        private readonly Vector2 pad;

        public FxaaUniform(Vector2 invTexSize, float enabled, float spanMax, float reduceMul, float reduceMin, float lumaThreshold, float lumaThresholdMin)
        {
            InvTexSize = invTexSize;
            Enabled = enabled;
            SpanMax = spanMax;
            ReduceMul = reduceMul;
            ReduceMin = reduceMin;
            LumaThreshold = lumaThreshold;
            LumaThresholdMin = lumaThresholdMin;
            pad = Vector2.Zero;
        }
    }

    private ThreeDCellInstance[] GetThreeDUploadSlice()
	{
		if (threeDCount <= 0) return Array.Empty<ThreeDCellInstance>();
		if (threeDUploadScratch.Length != threeDCount) threeDUploadScratch = new ThreeDCellInstance[threeDCount];
		Array.Copy(threeDInstances, 0, threeDUploadScratch, 0, threeDCount);
		return threeDUploadScratch;
	}
 

    // Unified camera uniform used by both flat glyph and 3D pipelines
    private readonly struct CameraUniform
    {
        public readonly Matrix4x4 View;
        public readonly Matrix4x4 Proj;
        public readonly Vector2 CellScale;
        public readonly Vector2 ViewCells;

        // Extras.x = timeSeconds
        // Extras.y = cubeScale (0..1 of cell-width)
        // Extras.z = zLift (in cell-width units)
        // Extras.w = wiggleAmp (in cell-width units)
        public readonly Vector4 Extras;

        public CameraUniform(Matrix4x4 view, Matrix4x4 proj, Vector2 cellScale, Vector2 viewCells, Vector4 extras)
        {
            View = view;
            Proj = proj;
            CellScale = cellScale;
            ViewCells = viewCells;
            Extras = extras;
        }
    }


    private readonly struct BoardUniform3D
	{
		public readonly Matrix4x4 Model;
		public readonly Matrix4x4 View;
		public readonly Matrix4x4 Proj;
		public readonly Vector2 BoardCells;
		public readonly float DebugGrid;
		public readonly float Pad0;

		public BoardUniform3D(Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj, Vector2 boardCells, bool debugGrid)
		{
			Model = model;
			View = view;
			Proj = proj;
			BoardCells = boardCells;
			DebugGrid = debugGrid ? 1f : 0f;
			Pad0 = 0f;
		}
	}

	private readonly struct BoardVertex
	{
		public const uint SizeInBytes = 8;
		public readonly Vector2 Corner;

		public BoardVertex(Vector2 corner)
		{
			Corner = corner;
		}
	}

	private readonly struct BoardAlignment3D
	{
		public readonly Vector2 CellScale;
		public readonly Vector2 BoardCells;
		public readonly Vector2 BoardSize;
		public readonly Matrix4x4 View;
		public readonly Matrix4x4 Proj;

		public BoardAlignment3D(Vector2 cellScale, Vector2 boardCells, Vector2 boardSize, Matrix4x4 view, Matrix4x4 proj)
		{
			CellScale = cellScale;
			BoardCells = boardCells;
			BoardSize = boardSize;
			View = view;
			Proj = proj;
		}
	}

    private BoardAlignment3D BuildBoardAlignment(int viewCellsW, int viewCellsH)
    {
        var cellScale = new Vector2(1f, 2f);
        var boardCells = new Vector2(viewCellsW, viewCellsH);
        var boardSize = new Vector2(viewCellsW * cellScale.X, viewCellsH * cellScale.Y);

        var target = new Vector3(boardSize.X * 0.5f, boardSize.Y * 0.5f, BoardPlaneZ);
        var eye = new Vector3(target.X, target.Y, 50f);
        var view = Matrix4x4.CreateLookAt(eye, target, new Vector3(0, -1, 0));

        // Use negative width to flip X-axis (mirrors the view horizontally)
        var proj = Matrix4x4.CreateOrthographic(-boardSize.X, boardSize.Y, 0.1f, 1000f);

        return new BoardAlignment3D(cellScale, boardCells, boardSize, view, proj);
    }

    private static Vector3 ComputeBoardWorldFromCell(float cellX, float cellY, Vector2 ownerOffset, in BoardAlignment3D alignment, float z)
	{
		return new Vector3(
			(cellX + ownerOffset.X + 0.5f) * alignment.CellScale.X,
			(cellY + ownerOffset.Y + 0.5f) * alignment.CellScale.Y,
			z);
	}

	private static Matrix4x4 GetBoardModelMatrix(in BoardAlignment3D alignment)
	{
		var origin = ComputeBoardWorldFromCell(0f, 0f, Vector2.Zero, alignment, BoardPlaneZ);
		return Matrix4x4.CreateScale(alignment.BoardSize.X, alignment.BoardSize.Y, 1f)
			* Matrix4x4.CreateTranslation(origin);
	}



	private void Ensure3dDebugResources()
	{
		if (pipeline3d == null || threeDShaderRevisionBuilt != ThreeDShaderRevision)
		{
			boardSet3d?.Dispose();
			boardSet3d = null;

			boardPipeline3d?.Dispose();
			boardPipeline3d = null;

			boardLayout3d?.Dispose();
			boardLayout3d = null;

			boardUbo3d?.Dispose();
			boardUbo3d = null;

			boardVb?.Dispose();
			boardVb = null;

			boardIb?.Dispose();
			boardIb = null;

			set3d?.Dispose();
			set3d = null;

			pipeline3d?.Dispose();
			pipeline3d = null;

			layout3d?.Dispose();
			layout3d = null;

			cameraUbo?.Dispose();
			cameraUbo = null;

			threeDSsaaCompositeSet?.Dispose();
			threeDSsaaCompositeSet = null;

			threeDSsaaCompositePipeline?.Dispose();
			threeDSsaaCompositePipeline = null;

			threeDSsaaCompositeLayout?.Dispose();
			threeDSsaaCompositeLayout = null;

			threeDSsaaSampler?.Dispose();
			threeDSsaaSampler = null;

			threeDSsaaFramebuffer?.Dispose();
			threeDSsaaFramebuffer = null;

			threeDSsaaDepthTex?.Dispose();
			threeDSsaaDepthTex = null;

			threeDSsaaColorView?.Dispose();
			threeDSsaaColorView = null;

			threeDSsaaColorTex?.Dispose();
			threeDSsaaColorTex = null;

			threeDSsaaW = 0;
			threeDSsaaH = 0;


			layout3d = factory.CreateResourceLayout(new ResourceLayoutDescription(
				new ResourceLayoutElementDescription("Camera", ResourceKind.UniformBuffer, ShaderStages.Vertex),
				new ResourceLayoutElementDescription("Offsets", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)));

			boardLayout3d = factory.CreateResourceLayout(new ResourceLayoutDescription(
				new ResourceLayoutElementDescription("Board", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)));

            threeDSsaaCompositeLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Scene", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SceneSampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Fxaa", ResourceKind.UniformBuffer, ShaderStages.Fragment)));


            cameraUbo = factory.CreateBuffer(new BufferDescription(256, BufferUsage.UniformBuffer));
			boardUbo3d = factory.CreateBuffer(new BufferDescription(256, BufferUsage.UniformBuffer));
            threeDSsaaCompositeUbo = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));


            ShapeRegistry.Instance.EnsureResources(gd, factory);
            CreateBoardBuffers();
			threeDSsaaSampler = factory.CreateSampler(new SamplerDescription(
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerFilter.MinLinear_MagLinear_MipPoint,
				comparisonKind: null,
				maximumAnisotropy: 0,
				minimumLod: 0,
				maximumLod: 0,
				lodBias: 0,
				borderColor: SamplerBorderColor.TransparentBlack));

			pipeline3d = Create3dDebugPipeline(layout3d, gd.MainSwapchain.Framebuffer.OutputDescription);
			boardPipeline3d = CreateBoardPipeline(boardLayout3d);
			threeDSsaaCompositePipeline = Create3dCompositePipeline(threeDSsaaCompositeLayout);

			threeDShaderRevisionBuilt = ThreeDShaderRevision;
		}

		if (set3d == null)
		{
			set3d = factory.CreateResourceSet(new ResourceSetDescription(layout3d, cameraUbo, ownerOffsetBuffer));
		}

		if (boardSet3d == null)
		{
			boardSet3d = factory.CreateResourceSet(new ResourceSetDescription(boardLayout3d, boardUbo3d));
		}
	}
    private static int RoundUp(int v, int multiple) => ((v + multiple - 1) / multiple) * multiple;
    private void EnsureThreeDSsaaTargets(int viewportW, int viewportH)
	{
        var targetW = Math.Max(1, (int)MathF.Ceiling(viewportW * ThreeDSsaaScale));
        var targetH = Math.Max(1, (int)MathF.Ceiling(viewportH * ThreeDSsaaScale));

        const int Bucket = 64;
        targetW = RoundUp(targetW, Bucket);
        targetH = RoundUp(targetH, Bucket);

        // NEW: cap to prevent "zoom into a point" from creating massive SSAA targets.
        // Pick values based on what your GPU can handle comfortably.
        const int MaxW = 2560;
        const int MaxH = 1440;
        targetW = Math.Min(targetW, MaxW);
        targetH = Math.Min(targetH, MaxH);

        if (threeDSsaaFramebuffer != null && targetW == threeDSsaaW && targetH == threeDSsaaH) return;

        threeDSsaaCompositeSet?.Dispose();
		threeDSsaaCompositeSet = null;

		threeDSsaaFramebuffer?.Dispose();
		threeDSsaaFramebuffer = null;

		threeDSsaaDepthTex?.Dispose();
		threeDSsaaDepthTex = null;

		threeDSsaaColorView?.Dispose();
		threeDSsaaColorView = null;

		threeDSsaaColorTex?.Dispose();
		threeDSsaaColorTex = null;

		threeDSsaaColorTex = factory.CreateTexture(TextureDescription.Texture2D((uint)targetW, (uint)targetH, 1, 1, gd.MainSwapchain.Framebuffer.OutputDescription.ColorAttachments[0].Format, TextureUsage.RenderTarget | TextureUsage.Sampled));
		threeDSsaaColorView = factory.CreateTextureView(threeDSsaaColorTex);
		threeDSsaaDepthTex = factory.CreateTexture(TextureDescription.Texture2D((uint)targetW, (uint)targetH, 1, 1, PixelFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil));
		threeDSsaaFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(threeDSsaaDepthTex, threeDSsaaColorTex));
		pipeline3d?.Dispose();
		pipeline3d = Create3dDebugPipeline(layout3d, threeDSsaaFramebuffer.OutputDescription);

        threeDSsaaCompositeSet = factory.CreateResourceSet(new ResourceSetDescription(
        threeDSsaaCompositeLayout,
        threeDSsaaColorView,
        threeDSsaaSampler,
        threeDSsaaCompositeUbo));

        var fxaaEnabled = 1f;
        gd.UpdateBuffer(threeDSsaaCompositeUbo, 0, new FxaaUniform(
            new Vector2(1f / targetW, 1f / targetH),
            fxaaEnabled,
            spanMax: 8f,
            reduceMul: 1f / 8f,
            reduceMin: 1f / 128f,
            lumaThreshold: 0.125f,
            lumaThresholdMin: 0.0312f));

        threeDSsaaW = targetW;
		threeDSsaaH = targetH;
	}



    

	private void CreateBoardBuffers()
	{
		var verts = new[]
		{
			new BoardVertex(new Vector2(0f, 0f)),
			new BoardVertex(new Vector2(1f, 0f)),
			new BoardVertex(new Vector2(1f, 1f)),
			new BoardVertex(new Vector2(0f, 1f)),
		};

		var idx = new ushort[] { 0, 1, 2, 0, 2, 3 };

		boardVb = factory.CreateBuffer(new BufferDescription((uint)(verts.Length * BoardVertex.SizeInBytes), BufferUsage.VertexBuffer));
		boardIb = factory.CreateBuffer(new BufferDescription((uint)(idx.Length * sizeof(ushort)), BufferUsage.IndexBuffer));

		gd.UpdateBuffer(boardVb, 0, verts);
		gd.UpdateBuffer(boardIb, 0, idx);
	}

    private Pipeline Create3dDebugPipeline(ResourceLayout layout, OutputDescription outputs)
    {
		var vertexCode = @"
#version 450

layout(location = 0) in vec3 Pos;
layout(location = 1) in vec3 Nrm;

// Instance data (matches ThreeDCellInstance: vec2 Cell, uint OwnerId, uint FgPacked)
layout(location = 2) in vec2 iCell;
layout(location = 3) in uint iOwnerId;
layout(location = 4) in uint iFgPacked;
layout(location = 5) in uint iShapeId;

layout(set = 0, binding = 0) uniform CameraUBO
{
    mat4 View;
    mat4 Proj;
    vec2 CellScale;
    vec2 ViewCells;
    vec4 Extras; // x=time, y=cubeScale, z=zLift, w=wiggleAmp
} Cam;

layout(std430, set = 0, binding = 1) readonly buffer Offsets { vec2 OwnerOffsets[]; };

layout(location = 0) out vec3 fsin_Nrm;
layout(location = 1) out vec3 fsin_ViewDir;
layout(location = 2) flat out uint fsin_FgPacked;

mat3 RotX(float a)
{
    float c = cos(a), s = sin(a);
    return mat3(
        1, 0, 0,
        0, c,-s,
        0, s, c
    );
}

mat3 RotY(float a)
{
    float c = cos(a), s = sin(a);
    return mat3(
         c, 0, s,
         0, 1, 0,
        -s, 0, c
    );
}

void main()
{
    vec2 off = OwnerOffsets[iOwnerId];

    float wx = (iCell.x + off.x + 0.5) * Cam.CellScale.x;
    float wy = (iCell.y + off.y + 0.5) * Cam.CellScale.y;

    float t = Cam.Extras.x;
    float cubeScale = Cam.Extras.y;  // fraction of cell-width
    float zLift = Cam.Extras.z;      // in cell-width units
    float rotAmp = Cam.Extras.w;     // radians (e.g. 0.25 ~= 14 degrees)

    // Square/padded footprint: size based on cell-width only
    float s = Cam.CellScale.x * cubeScale;

    // Desync rotations per cell (and owner) so it's not a synchronized wobble
    float phase = (iCell.x * 0.73 + iCell.y * 1.11) + float(iOwnerId) * 0.17;

    // Small oscillating tilt around X and Y
    float ax = sin(t * 2.1 + phase) * rotAmp;
    float ay = cos(t * 1.7 + phase) * rotAmp;

    mat3 R = RotY(ay) * RotX(ax);

    vec3 basePos = vec3(wx, wy, Cam.CellScale.x * zLift);

    // Rotate the cube in local space, then scale to world
    vec3 localPos = R * Pos;
    vec3 worldPos = basePos + localPos * s;

    vec4 viewPos = Cam.View * vec4(worldPos, 1.0);
    gl_Position = Cam.Proj * viewPos;

    // IMPORTANT: rotate normals too so lighting changes with tilt
    fsin_Nrm = normalize(R * Nrm);
    fsin_ViewDir = normalize(-viewPos.xyz);
    fsin_FgPacked = iFgPacked;
}";




		var fragmentCode = @"
#version 450

layout(location = 0) in vec3 fsin_Nrm;
layout(location = 1) in vec3 fsin_ViewDir;
layout(location = 2) flat in uint fsin_FgPacked;
layout(location = 0) out vec4 fsout_Color;

vec3 UnpackRgb(uint p)
{
    float r = float(p & 255u);
    float g = float((p >> 8) & 255u);
    float b = float((p >> 16) & 255u);
    return vec3(r, g, b) / 255.0;
}

vec3 SrgbToLinear(vec3 c)
{
    return pow(c, vec3(2.2));
}

vec3 LinearToSrgb(vec3 c)
{
    return pow(max(c, vec3(0.0)), vec3(1.0 / 2.2));
}

void main()
{
    vec3 n = normalize(fsin_Nrm);
    vec3 viewDir = normalize(fsin_ViewDir);

    vec3 keyLight = normalize(vec3(0.45, 0.78, 0.45));
    vec3 fillLight = normalize(vec3(-0.65, 0.25, 0.71));

    float key = max(dot(n, keyLight), 0.0);
    float fill = max(dot(n, fillLight), 0.0);

    vec3 halfDir = normalize(keyLight + viewDir);
    float spec = pow(max(dot(n, halfDir), 0.0), 12.0);

    float rim = pow(clamp(1.0 - max(dot(n, viewDir), 0.0), 0.0, 1.0), 2.0);

    float ambient = 0.34;
    float lit = ambient + key * 0.75 + fill * 0.30;

    vec3 fg = UnpackRgb(fsin_FgPacked);
    vec3 base = SrgbToLinear(fg);
    vec3 linear = base * lit;
    linear += vec3(1.0) * spec * 0.10;
    linear += base * rim * 0.25;

	fsout_Color = vec4(LinearToSrgb(linear), 1.0);
}";





		var shaders = factory.CreateFromSpirv(
            new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(vertexCode), "main"),
            new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(fragmentCode), "main"));

        var vLayout = ShapeRegistry.Instance.VertexLayout;

        var instLayout = new VertexLayoutDescription(
            new VertexElementDescription("iCell", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("iOwnerId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
            new VertexElementDescription("iFgPacked", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
            new VertexElementDescription("iShapeId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1))
        {
            InstanceStepRate = 1
        };


        var pd = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
			DepthStencilState = new DepthStencilStateDescription(true, true, ComparisonKind.LessEqual),

			RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, depthClipEnabled: true, scissorTestEnabled: false),

			PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { layout },
			ShaderSet = new ShaderSetDescription(new[] { vLayout, instLayout }, shaders),
			Outputs = outputs
        };

        var p = factory.CreateGraphicsPipeline(ref pd);
        for (var i = 0; i < shaders.Length; i++) shaders[i].Dispose();
        return p;
    }

	private Pipeline CreateBoardPipeline(ResourceLayout layout)
	{
		var vertexCode = @"
#version 450

layout(location = 0) in vec2 Corner;

layout(set = 0, binding = 0) uniform BoardUBO
{
    mat4 Model;
    mat4 View;
    mat4 Proj;
    vec2 BoardCells;
    float DebugGrid;
    float Pad0;
} Board;

layout(location = 0) out vec2 fsin_Cell;

void main()
{
    fsin_Cell = Corner * Board.BoardCells;
    vec4 worldPos = Board.Model * vec4(Corner.xy, 0.0, 1.0);
    gl_Position = Board.Proj * Board.View * worldPos;
}";

		var fragmentCode = @"
#version 450

layout(set = 0, binding = 0) uniform BoardUBO
{
    mat4 Model;
    mat4 View;
    mat4 Proj;
    vec2 BoardCells;
    float DebugGrid;
    float Pad0;
} Board;

layout(location = 0) in vec2 fsin_Cell;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    float parity = mod(floor(fsin_Cell.x) + floor(fsin_Cell.y), 2.0);
    vec3 checkerA = vec3(0.14, 0.17, 0.20);
    vec3 checkerB = vec3(0.10, 0.12, 0.15);
    vec3 baseColor = mix(checkerA, checkerB, parity);

    vec2 edgeDist = min(fract(fsin_Cell), 1.0 - fract(fsin_Cell));
    float gridLine = 1.0 - smoothstep(0.0, 0.08, min(edgeDist.x, edgeDist.y));
    vec3 color = mix(baseColor, vec3(0.35, 0.40, 0.46), gridLine * Board.DebugGrid * 0.55);

    float border = step(fsin_Cell.x, 0.01) + step(fsin_Cell.y, 0.01)
                 + step(Board.BoardCells.x - 0.01, fsin_Cell.x)
                 + step(Board.BoardCells.y - 0.01, fsin_Cell.y);
    if (border > 0.0)
    {
        color = vec3(0.55, 0.2, 0.2);
    }

    fsout_Color = vec4(color, 0.40);
}";

		var shaders = factory.CreateFromSpirv(
			new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(vertexCode), "main"),
			new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(fragmentCode), "main"));

		var boardLayout = new VertexLayoutDescription(
			new VertexElementDescription("Corner", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
		{
			Stride = BoardVertex.SizeInBytes,
			InstanceStepRate = 0
		};

		var pd = new GraphicsPipelineDescription
		{
			BlendState = BlendStateDescription.SingleAlphaBlend,
			DepthStencilState = DepthStencilStateDescription.Disabled,
			RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, depthClipEnabled: true, scissorTestEnabled: false),
			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = new[] { layout },
			ShaderSet = new ShaderSetDescription(new[] { boardLayout }, shaders),
			Outputs = gd.MainSwapchain.Framebuffer.OutputDescription
		};

		var p = factory.CreateGraphicsPipeline(ref pd);
		for (var i = 0; i < shaders.Length; i++) shaders[i].Dispose();
		return p;
	}

	private Pipeline Create3dCompositePipeline(ResourceLayout layout)
	{
		var vertexCode = @"
#version 450

layout(location = 0) in vec2 Corner;
layout(location = 0) out vec2 fsin_Uv;

void main()
{
    fsin_Uv = vec2(Corner.x, 1.0 - Corner.y);
    vec2 ndc = Corner * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
}";

        var fragmentCode = @"
#version 450

layout(set = 0, binding = 0) uniform texture2D Scene;
layout(set = 0, binding = 1) uniform sampler SceneSampler;

layout(set = 0, binding = 2) uniform FxaaUBO
{
    vec2 InvTexSize;
    float Enabled;
    float SpanMax;
    float ReduceMul;
    float ReduceMin;
    float LumaThreshold;
    float LumaThresholdMin;
} Fxaa;

layout(location = 0) in vec2 fsin_Uv;
layout(location = 0) out vec4 fsout_Color;

vec3 SrgbToLinear(vec3 c)
{
    return pow(c, vec3(2.2));
}

float Luma(vec3 rgb)
{
    vec3 linear = SrgbToLinear(rgb);
    return dot(linear, vec3(0.2126, 0.7152, 0.0722));
}

vec4 SampleScene(vec2 uv)
{
    return texture(sampler2D(Scene, SceneSampler), uv);
}

void main()
{
	if (Fxaa.Enabled < 0.5)
	{
		fsout_Color = SampleScene(fsin_Uv);
		return;
	}
    vec2 rcp = Fxaa.InvTexSize;
    vec2 uv = fsin_Uv;

    vec4 cM  = SampleScene(uv);
    vec3 rgbM = cM.rgb;
    float aM = cM.a;

if (aM < 0.001)
{
    fsout_Color = cM;
    return;
}
    float lumaM = Luma(rgbM);

    float lumaNW = Luma(SampleScene(uv + vec2(-1.0, -1.0) * rcp).rgb);
    float lumaNE = Luma(SampleScene(uv + vec2( 1.0, -1.0) * rcp).rgb);
    float lumaSW = Luma(SampleScene(uv + vec2(-1.0,  1.0) * rcp).rgb);
    float lumaSE = Luma(SampleScene(uv + vec2( 1.0,  1.0) * rcp).rgb);

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    float lumaRange = lumaMax - lumaMin;

    if (lumaRange < max(Fxaa.LumaThresholdMin, lumaMax * Fxaa.LumaThreshold))
    {
        fsout_Color = cM;
        return;
    }

    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * Fxaa.ReduceMul), Fxaa.ReduceMin);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);

    dir = clamp(dir * rcpDirMin, vec2(-Fxaa.SpanMax), vec2(Fxaa.SpanMax)) * rcp;

    vec4 c1 = SampleScene(uv + dir * (1.0/3.0 - 0.5));
    vec4 c2 = SampleScene(uv + dir * (2.0/3.0 - 0.5));
    vec4 c3 = SampleScene(uv + dir * (-0.5));
    vec4 c4 = SampleScene(uv + dir * (0.5));

    vec3 rgbA = 0.5 * (c1.rgb + c2.rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (c3.rgb + c4.rgb);

    float aA = 0.5 * (c1.a + c2.a);
    float aB = aA * 0.5 + 0.25 * (c3.a + c4.a);

    float lumaB = Luma(rgbB);

    bool useA = (lumaB < lumaMin || lumaB > lumaMax);
    vec3 outRgb = useA ? rgbA : rgbB;
    float outA = useA ? aA : aB;

    // Optional: keep center alpha as a floor so “thin” coverage doesn’t get smeared away
    outA = max(outA, aM);

    fsout_Color = vec4(outRgb, outA);
}";


        var shaders = factory.CreateFromSpirv(
			new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(vertexCode), "main"),
			new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(fragmentCode), "main"));

		var quadLayout = new VertexLayoutDescription(
			new VertexElementDescription("Corner", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
		{
			Stride = QuadVertex.SizeInBytes,
			InstanceStepRate = 0
		};

		var pd = new GraphicsPipelineDescription
		{
			BlendState = BlendStateDescription.SingleAlphaBlend,
			DepthStencilState = DepthStencilStateDescription.Disabled,
			RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, depthClipEnabled: true, scissorTestEnabled: false),
			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = new[] { layout },
			ShaderSet = new ShaderSetDescription(new[] { quadLayout }, shaders),
			Outputs = gd.MainSwapchain.Framebuffer.OutputDescription
		};

		var p = factory.CreateGraphicsPipeline(ref pd);
		for (var i = 0; i < shaders.Length; i++) shaders[i].Dispose();
		return p;
	}

	private void DrawAlignedBoard(CommandList cl, in BoardAlignment3D alignment)
	{
		var model = GetBoardModelMatrix(alignment);
		gd.UpdateBuffer(boardUbo3d, 0, new BoardUniform3D(model, alignment.View, alignment.Proj, alignment.BoardCells, true));

		cl.SetPipeline(boardPipeline3d);
		cl.SetGraphicsResourceSet(0, boardSet3d);
		cl.SetVertexBuffer(0, boardVb);
		cl.SetIndexBuffer(boardIb, IndexFormat.UInt16);
		cl.DrawIndexed(6, 1, 0, 0, 0);
	}

	private void DrawBoardPass(CommandList cl, in BoardAlignment3D alignment)
	{
		Ensure3dDebugResources();
		DrawAlignedBoard(cl, alignment);
	}

    private void Draw3dLaneShapes(CommandList cl, int vpX, int vpY, int vpW, int vpH)
    {
        Ensure3dDebugResources();
		EnsureThreeDSsaaTargets(vpW, vpH);

		cl.SetFramebuffer(threeDSsaaFramebuffer);
		cl.ClearColorTarget(0, RgbaFloat.Clear);
		cl.ClearDepthStencil(1f);
		cl.SetViewport(0, new Veldrid.Viewport(0, 0, threeDSsaaW, threeDSsaaH, 0f, 1f));
		cl.SetScissorRect(0, 0, 0, (uint)threeDSsaaW, (uint)threeDSsaaH);

        // Allocate instance buffer for at least 1 instance so the GPU path stays valid.
        var uploadCount = Math.Max(1, threeDCount);
        var bytes = (uint)uploadCount * ThreeDCellInstance.SizeInBytes;
        EnsureThreeDInstanceBufferSize(bytes);

        // Keep a single dummy instance for the "no instances" case.
        // IMPORTANT: if you've added ShapeId to ThreeDCellInstance, include it here too.
        var one = new[] { new ThreeDCellInstance(new Vector2(0, 0), 0, 0x00FFFFFFu, shapeId: 0u) };

        if (threeDCount == 0)
        {
            gd.UpdateBuffer(threeDInstanceBuffer, 0, one);

            cl.SetPipeline(pipeline3d);
            cl.SetGraphicsResourceSet(0, set3d);

            var shape = ShapeRegistry.Instance.GetShape(0); // fallback shape
            cl.SetVertexBuffer(0, shape.VertexBuffer);
            cl.SetVertexBuffer(1, threeDInstanceBuffer);
            cl.SetIndexBuffer(shape.IndexBuffer, shape.IndexFormat);
            cl.DrawIndexed(indexCount: shape.IndexCount, instanceCount: 1, indexStart: 0, vertexOffset: 0, instanceStart: 0);

			Composite3dSsaa(cl, vpX, vpY, vpW, vpH);
			return;
        }

        // Get the active instances. This MUST be the first 'threeDCount' items.
        // If GetThreeDUploadSlice() returns a larger backing array, fix it to return exactly threeDCount,
        // or copy into a scratch array here before uploading (to avoid uploading junk past threeDCount).
        var slice = GetThreeDUploadSlice();

        // Batch by ShapeId by sorting in-place by ShapeId so equal IDs become contiguous.
        // This keeps the renderer generic: no per-shape branches, one draw per run.
        Array.Sort(slice, 0, threeDCount, ThreeDCellInstanceShapeIdComparer.Instance);

        gd.UpdateBuffer(threeDInstanceBuffer, 0, slice);
        cl.SetPipeline(pipeline3d);
        cl.SetGraphicsResourceSet(0, set3d);
        cl.SetVertexBuffer(1, threeDInstanceBuffer); // <-- REQUIRED for instancing

        var i = 0;
        while (i < threeDCount)
        {
            var shapeId = (ushort)slice[i].ShapeId;
            var start = i;

            i++;
            while (i < threeDCount && slice[i].ShapeId == shapeId) i++;

            var runCount = (uint)(i - start);

            var shape = ShapeRegistry.Instance.GetShape(shapeId);
            cl.SetVertexBuffer(0, shape.VertexBuffer);
            cl.SetIndexBuffer(shape.IndexBuffer, shape.IndexFormat);

            cl.DrawIndexed(
                indexCount: shape.IndexCount,
                instanceCount: runCount,
                indexStart: 0,
                vertexOffset: 0,
                instanceStart: (uint)start);
        }

		Composite3dSsaa(cl, vpX, vpY, vpW, vpH);
    }

	private void Composite3dSsaa(CommandList cl, int vpX, int vpY, int vpW, int vpH)
	{
		cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
		cl.SetViewport(0, new Veldrid.Viewport(vpX, vpY, vpW, vpH, 0f, 1f));
		cl.SetScissorRect(0, (uint)vpX, (uint)vpY, (uint)vpW, (uint)vpH);
		cl.SetPipeline(threeDSsaaCompositePipeline);
		cl.SetGraphicsResourceSet(0, threeDSsaaCompositeSet);
		cl.SetVertexBuffer(0, quadVb);
		cl.SetIndexBuffer(quadIb, IndexFormat.UInt16);
		cl.DrawIndexed(6, 1, 0, 0, 0);
	}

    private sealed class ThreeDCellInstanceShapeIdComparer : IComparer<ThreeDCellInstance>
    {
        public static readonly ThreeDCellInstanceShapeIdComparer Instance = new ThreeDCellInstanceShapeIdComparer();
        private ThreeDCellInstanceShapeIdComparer() { }
        public int Compare(ThreeDCellInstance a, ThreeDCellInstance b) => a.ShapeId.CompareTo(b.ShapeId);
    }



    private void Dispose3dDebug()
    {
		set3d?.Dispose();
		set3d = null;

		boardSet3d?.Dispose();
		boardSet3d = null;

		pipeline3d?.Dispose();
		pipeline3d = null;

		boardPipeline3d?.Dispose();
		boardPipeline3d = null;

		layout3d?.Dispose();
		layout3d = null;

		boardLayout3d?.Dispose();
		boardLayout3d = null;

		cameraUbo?.Dispose();
		cameraUbo = null;

		boardUbo3d?.Dispose();
		boardUbo3d = null;

 
        boardVb?.Dispose();
		boardVb = null;

		boardIb?.Dispose();
		boardIb = null;

		threeDSsaaCompositeSet?.Dispose();
		threeDSsaaCompositeSet = null;

		threeDSsaaCompositePipeline?.Dispose();
		threeDSsaaCompositePipeline = null;

		threeDSsaaCompositeLayout?.Dispose();
		threeDSsaaCompositeLayout = null;

		threeDSsaaSampler?.Dispose();
		threeDSsaaSampler = null;

		threeDSsaaFramebuffer?.Dispose();
		threeDSsaaFramebuffer = null;

		threeDSsaaDepthTex?.Dispose();
		threeDSsaaDepthTex = null;

		threeDSsaaColorView?.Dispose();
		threeDSsaaColorView = null;

		threeDSsaaColorTex?.Dispose();
		threeDSsaaColorTex = null;

        threeDSsaaCompositeUbo?.Dispose();
        threeDSsaaCompositeUbo = null;
    }
}

using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace klooie;

public sealed partial class CellInstancedRenderer
{
    private Pipeline CreateBackgroundPipeline(ResourceLayout layout, OutputDescription outputs)
    {
        var vertexCode = @"
#version 450

layout(location = 0) in vec2 Corner;
layout(location = 1) in vec2 Cell;
layout(location = 2) in uint OwnerId;
layout(location = 3) in uint GlyphPacked;
layout(location = 4) in uint FgPacked;
layout(location = 5) in uint BgPacked;

layout(set = 0, binding = 2) uniform CameraUBO
{
    mat4 View;
    mat4 Proj;
    vec2 CellScale;
    vec2 ViewCells;
} Cam;

layout(std430, set = 0, binding = 3) readonly buffer Offsets { vec2 OwnerOffsets[]; };

layout(location = 0) flat out uint fsin_BgPacked;

void main()
{
    vec2 off = OwnerOffsets[OwnerId];
    vec2 expandedCorner = mix(vec2(-0.02), vec2(1.02), Corner);
    vec2 worldXY = (Cell + off + expandedCorner) * Cam.CellScale;
    gl_Position = Cam.Proj * Cam.View * vec4(worldXY, 0.0, 1.0);
    fsin_BgPacked = BgPacked;
}";

        var fragmentCode = @"
#version 450

layout(location = 0) flat in uint fsin_BgPacked;
layout(location = 0) out vec4 fsout_Color;

vec3 UnpackRgb(uint p)
{
    float r = float(p & 255u);
    float g = float((p >> 8) & 255u);
    float b = float((p >> 16) & 255u);
    return vec3(r, g, b) / 255.0;
}

void main()
{
    fsout_Color = vec4(UnpackRgb(fsin_BgPacked), 1.0);
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

        var instLayout = new VertexLayoutDescription(
            new VertexElementDescription("Cell", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("OwnerId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
            new VertexElementDescription("Glyph", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
            new VertexElementDescription("FgPacked", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
            new VertexElementDescription("BgPacked", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1))
        {
            Stride = FlatCellInstance.SizeInBytes,
            InstanceStepRate = 1
        };

        var pd = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, depthClipEnabled: true, scissorTestEnabled: false),
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { layout },
            ShaderSet = new ShaderSetDescription(new[] { quadLayout, instLayout }, shaders),
            Outputs = outputs
        };

        var p = factory.CreateGraphicsPipeline(ref pd);
        for (var i = 0; i < shaders.Length; i++) shaders[i].Dispose();
        return p;
    }

    private Pipeline CreatePipeline(ResourceLayout layout, OutputDescription outputs)
    {
        var vertexCode = @"
#version 450

layout(location = 0) in vec2 Corner;     // per-vertex: (0..1)
layout(location = 1) in vec2 Cell;       // per-instance: cell x/y
layout(location = 2) in uint OwnerId;    // per-instance
layout(location = 3) in uint GlyphPacked; // per-instance (index | (page<<16))
layout(location = 4) in uint FgPacked;   // per-instance
layout(location = 5) in uint BgPacked;   // per-instance

layout(set = 0, binding = 2) uniform CameraUBO
{
    mat4 View;
    mat4 Proj;
    vec2 CellScale;
    vec2 ViewCells;
} Cam;

layout(std430, set = 0, binding = 3) readonly buffer Offsets { vec2 OwnerOffsets[]; };

layout(location = 0) out vec2 fsin_Corner;
layout(location = 1) flat out uint fsin_GlyphPacked;
layout(location = 2) flat out uint fsin_FgPacked;
void main()
{
    vec2 off = OwnerOffsets[OwnerId];

    // World-space positioning: compute position in world space then apply View/Proj
    vec2 worldXY = (Cell + off + Corner) * Cam.CellScale;
    gl_Position = Cam.Proj * Cam.View * vec4(worldXY, 0.0, 1.0);

    fsin_Corner = Corner;
    fsin_GlyphPacked = GlyphPacked;
    fsin_FgPacked = FgPacked;
}";

        var fragmentCode = @"
#version 450

layout(set = 0, binding = 4) uniform texture2D GlyphTex;
layout(set = 0, binding = 5) uniform sampler GlyphSamp;   // Glyph sampler (linear)

layout(set = 0, binding = 6) uniform GlyphUBO
{
    vec2 AtlasCellPix;   // atlas cell size in px
    vec2 AtlasSize;      // atlas texture size in px
    vec2 PadPix;         // (padX, padTop)
    vec2 InnerPix;       // (innerW, innerH) -- after subtracting padX and padTop/bottom
    vec4 Shadow;         // (offsetX_px, offsetY_px, opacity, radius_px)
} Glyph;

layout(location = 0) in vec2 fsin_Corner;
layout(location = 1) flat in uint fsin_GlyphPacked;
layout(location = 2) flat in uint fsin_FgPacked;
layout(location = 0) out vec4 fsout_Color;

vec3 UnpackRgb(uint p)
{
    float r = float(p & 255u);
    float g = float((p >> 8) & 255u);
    float b = float((p >> 16) & 255u);
    return vec3(r, g, b) / 255.0;
}

float SampleGlyphA(vec2 pix, vec2 atlasSize)
{
    return texture(sampler2D(GlyphTex, GlyphSamp), pix / atlasSize).a;
}

void main()
{
    // Packed: low 16 bits = glyph index, high 16 bits = page (unused for now)
    uint glyphIndex = fsin_GlyphPacked & 65535u;

    float gx = float(glyphIndex & 15u);
    float gy = float(glyphIndex >> 4);

    vec2 cellPix = Glyph.AtlasCellPix;
    vec2 atlasSize = Glyph.AtlasSize;

    // Clamp corner just in case
    vec2 c = clamp(fsin_Corner, vec2(0.0), vec2(1.0));

    // Map into inner rect (avoids sampling padding/borders)
    vec2 inner = c * (Glyph.InnerPix - vec2(1.0));
    vec2 glyphPix = vec2(gx, gy) * cellPix + Glyph.PadPix + inner + vec2(0.5);

    float a = SampleGlyphA(glyphPix, atlasSize);

    // Softer top - down shadow/outline:
    // - radius expands on all sides (like an ambient occlusion outline)
    // - offset biases the whole thing (like a directional light)
    float radius = max(0.0, Glyph.Shadow.w);
        vec2 base = glyphPix + Glyph.Shadow.xy;

        // 8 directions
        vec2 d0 = vec2(1, 0);
        vec2 d1 = vec2(-1, 0);
        vec2 d2 = vec2(0, 1);
        vec2 d3 = vec2(0, -1);
        vec2 d4 = vec2(1, 1);
        vec2 d5 = vec2(-1, 1);
        vec2 d6 = vec2(1, -1);
        vec2 d7 = vec2(-1, -1);

        // Two rings => nicer falloff without too many samples
        float r1 = radius;
        float r2 = radius * 1.75;

        // Dilated alpha at inner ring (max of neighbors)
        float m1 = 0.0;
        m1 = max(m1, SampleGlyphA(base + d0 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d1 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d2 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d3 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d4 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d5 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d6 * r1, atlasSize));
        m1 = max(m1, SampleGlyphA(base + d7 * r1, atlasSize));

        // Dilated alpha at outer ring
        float m2 = 0.0;
        m2 = max(m2, SampleGlyphA(base + d0 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d1 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d2 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d3 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d4 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d5 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d6 * r2, atlasSize));
        m2 = max(m2, SampleGlyphA(base + d7 * r2, atlasSize));

        // Combine rings: inner stronger, outer weaker => soft edge
        float aShadow = clamp(m1 * 0.70 + m2 * 0.30, 0.0, 1.0);

        // Keep shadow outside glyph so interior stays crisp
        float shadowMask = clamp(aShadow - a, 0.0, 1.0);

vec3 fg = UnpackRgb(fsin_FgPacked);

// If the glyph color is very dark, use a light ""glow"" outline instead of a black shadow.
// Otherwise keep the old behavior (black shadow).
float fgLum = dot(fg, vec3(0.2126, 0.7152, 0.0722));
vec3 shadowColor = (fgLum < 0.12) ? vec3(1.0) : vec3(0.0);

// Optional: when it's a glow, dial opacity down a bit so it doesn't look chunky.
float shadowOpacity = Glyph.Shadow.z * ((fgLum < 0.12) ? 0.65 : 1.0);

float shadowAlpha = shadowMask * shadowOpacity;
float glyphAlpha = a;
float outAlpha = clamp(shadowAlpha + glyphAlpha, 0.0, 1.0);
vec3 outRgb = outAlpha > 0.0
    ? ((shadowColor * shadowAlpha) + (fg * glyphAlpha)) / outAlpha
    : vec3(0.0);

fsout_Color = vec4(outRgb, outAlpha);
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

    var instLayout = new VertexLayoutDescription(
        new VertexElementDescription("Cell", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
        new VertexElementDescription("OwnerId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
        new VertexElementDescription("Glyph", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
        new VertexElementDescription("FgPacked", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
        new VertexElementDescription("BgPacked", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1))
    {
        Stride = FlatCellInstance.SizeInBytes,
        InstanceStepRate = 1
    };

    var pd = new GraphicsPipelineDescription
    {
        BlendState = BlendStateDescription.SingleAlphaBlend,
        DepthStencilState = DepthStencilStateDescription.Disabled,
        RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, depthClipEnabled: true, scissorTestEnabled: false),
        PrimitiveTopology = PrimitiveTopology.TriangleList,
        ResourceLayouts = new[] { layout },
        ShaderSet = new ShaderSetDescription(new[] { quadLayout, instLayout }, shaders),
        Outputs = outputs
    };

    var p = factory.CreateGraphicsPipeline(ref pd);
        for (var i = 0; i<shaders.Length; i++) shaders[i].Dispose();
        return p;
    }
}

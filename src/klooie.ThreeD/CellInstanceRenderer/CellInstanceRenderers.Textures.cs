using PowerArgs;
using Veldrid;
using VdTexture = Veldrid.Texture;
using VdTextureView = Veldrid.TextureView;

namespace klooie;

public sealed partial class CellInstancedRenderer
{
    private void EnsureFrameTexture(int w, int h)
    {
        if (frameTex != null && frameTex.W == w && frameTex.H == h) return;

        frameTex?.Dispose();
        frameTex = new ControlTex(gd, factory, w, h);

        resourceSet?.Dispose();
        resourceSet = null;
    }

    private sealed class ControlTex : IDisposable
    {
        private readonly GraphicsDevice gd;
        public readonly int W;
        public readonly int H;

        public readonly VdTexture Tex;
        public readonly VdTextureView View;

        private byte[] rgba;

        public ControlTex(GraphicsDevice gd, ResourceFactory factory, int w, int h)
        {
            this.gd = gd;
            W = w;
            H = h;

            Tex = factory.CreateTexture(TextureDescription.Texture2D((uint)w, (uint)h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
            View = factory.CreateTextureView(Tex);

            rgba = new byte[w * h * 4];
        }
        public void UploadInk(ReadOnlySpan<ConsoleCharacter> pixels, int w, int h)
        {
            var cellCount = checked(w * h);
            if (pixels.Length < cellCount) throw new ArgumentException($"pixels.Length {pixels.Length} < {cellCount}", nameof(pixels));

            var dst = rgba;
            var di = 0;

            for (var i = 0; i < cellCount; i++)
            {
                var rgb = pixels[i].BackgroundColor;
                dst[di++] = rgb.R;
                dst[di++] = rgb.G;
                dst[di++] = rgb.B;
                dst[di++] = 255;
            }

            gd.UpdateTexture(Tex, dst, 0, 0, 0, (uint)w, (uint)h, 1, 0, 0);
        }


        public void Dispose()
        {
            View.Dispose();
            Tex.Dispose();
        }
    }
}

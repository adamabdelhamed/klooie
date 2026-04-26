using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace klooie;
public static class SystemDrawingBitmapConverter
{
    public static ConsoleBitmap Convert(Bitmap bitmap, int scale = 3, RGB? chromaKey = null)
    {
        var sourceCellW = scale;
        var sourceCellH = sourceCellW * 2;

        var w = bitmap.Width / sourceCellW;
        var h = bitmap.Height / sourceCellH;

        var consoleBitmap = ConsoleBitmap.Create(w, h);

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                var basePtr = (byte*)data.Scan0;

                for (var cy = 0; cy < h; cy++)
                {
                    for (var cx = 0; cx < w; cx++)
                    {
                        long r = 0;
                        long g = 0;
                        long b = 0;
                        long a = 0;
                        long count = 0;

                        var startX = cx * sourceCellW;
                        var startY = cy * sourceCellH;

                        for (var y = 0; y < sourceCellH; y++)
                        {
                            var row = basePtr + ((startY + y) * data.Stride);

                            for (var x = 0; x < sourceCellW; x++)
                            {
                                var pixel = row + ((startX + x) * 4);

                                b += pixel[0];
                                g += pixel[1];
                                r += pixel[2];
                                a += pixel[3];
                                count++;
                            }
                        }

                        var avg = new RGB((byte)(r / count), (byte)(g / count), (byte)(b / count));
                        var avgA = (byte)(a / count);
                        if(avgA == 0)
                        {
                            avg = chromaKey ?? ConsoleString.DefaultBackgroundColor;
                        }
                        consoleBitmap.SetPixel(cx, cy, new ConsoleCharacter(' ', RGB.White, avg));
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
            bitmap.Dispose();
        }
        return consoleBitmap;
    }
}

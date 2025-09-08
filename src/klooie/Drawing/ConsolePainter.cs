using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class ConsolePainter
{ 
    public static int AvgWriteLength => FastConsoleWriter.AvgWriteLength;
    internal static readonly FrameRateMeter SkipRateMeter = new FrameRateMeter();
    private static readonly FastConsoleWriter fastConsoleWriter = new FastConsoleWriter();
    private static readonly PaintBuffer paintBuilder = new PaintBuffer();
    private static readonly List<Run> runsOnLine = new List<Run>(512);

    private static int lastBufferWidth;
    private static AnsiState _ansi;
    public static void Initialize()
    {
        fastConsoleWriter.Write("\x1b[?25l".ToCharArray(), "\x1b[?25l".Length);
        fastConsoleWriter.Write("\x1b[?1049h".ToCharArray(), "\x1b[?1049h".Length);
    }

    public static void Reset()
    {
        fastConsoleWriter.Write("\x1b[?25h".ToCharArray(), "\x1b[?25h".Length);
        fastConsoleWriter.Write("\x1b[?1049l".ToCharArray(), "\x1b[?1049l".Length);
    }

    public static bool Paint(ConsoleBitmap bitmap)
    {
        if (Console.WindowHeight == 0) return false;

        if (PaintQoS.CanPaint()  == false)
        {
            SkipRateMeter.Increment();
            ConsoleApp.Current?.WriteLine(SkipRateMeter.CurrentFPS+" Frames Dropped/S");
            return false;
        }
        _ansi = default;
        int colorThresholdSq = PaintCompressor.ComputeColorThresholdSq();
        PaintCompressor.BuildPerFrameThresholds(colorThresholdSq);
        if (lastBufferWidth != Console.BufferWidth)
        {
            lastBufferWidth = Console.BufferWidth;
            Console.Clear();
        }

        int attempts = 0;
        while (true)
        {
            try
            {
                if (bitmap.Width == 0 || bitmap.Height == 0)
                {
                    paintBuilder.Clear();
                    PaintQoS.BeginWrite();
                    fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                    PaintQoS.EndWrite(0);
                    return false;
                }

                paintBuilder.Clear();
                int charBudget = PaintQoS.BudgetedCharsThisFrame();
                for (int y = 0; y < bitmap.Height; y++)
                {
                    if (paintBuilder.Length >= charBudget) break; // <-- hard cap per frame
                    runsOnLine.Clear();
                    int x = 0;
                    while (x < bitmap.Width)
                    {
                        ref var p = ref bitmap.Pixels[bitmap.IndexOf(x, y)];
                        var fg = p.ForegroundColor;
                        var bg = p.BackgroundColor;
                        bool under = p.IsUnderlined;
                        int start = x;
                        x++;
                        while (x < bitmap.Width)
                        {
                            ref var q = ref bitmap.Pixels[bitmap.IndexOf(x, y)];

                            if (q.IsUnderlined != under) break;

                            bool fgOk, bgOk;

                            if (colorThresholdSq == 0)
                            {
                                fgOk = (q.ForegroundColor == fg);
                                bgOk = (q.BackgroundColor == bg);
                            }
                            else
                            {
                                int offset = x - start;
                                if (offset >= PaintCompressor.MaxSoftenedRun)
                                {
                                    // tolerance fully decayed -> strict equality
                                    fgOk = (q.ForegroundColor == fg);
                                    bgOk = (q.BackgroundColor == bg);
                                }
                                else
                                {
                                    int thrSq = PaintCompressor.s_ThrSqByOffset[offset];
                                    byte cap = PaintCompressor.s_ChannelCapByOffset[offset];

                                    // --- Space-aware: ignore FG when glyph is space (only BG is visible) ---
                                    if (q.Value == ' ')
                                    {
                                        // fast channel guard on BG before d² compare
                                        int dBr = q.BackgroundColor.R - bg.R; if ((dBr ^ (dBr >> 31)) > cap) break;
                                        int dBg = q.BackgroundColor.G - bg.G; if ((dBg ^ (dBg >> 31)) > cap) break;
                                        int dBb = q.BackgroundColor.B - bg.B; if ((dBb ^ (dBb >> 31)) > cap) break;

                                        bgOk = PaintCompressor.ColorsCloseEnough(q.BackgroundColor, bg, thrSq);
                                        fgOk = true; // ignored for spaces
                                    }
                                    else
                                    {
                                        // fast channel guards (FG + BG)
                                        int dFr = q.ForegroundColor.R - fg.R; if ((dFr ^ (dFr >> 31)) > cap) break;
                                        int dFg = q.ForegroundColor.G - fg.G; if ((dFg ^ (dFg >> 31)) > cap) break;
                                        int dFb = q.ForegroundColor.B - fg.B; if ((dFb ^ (dFb >> 31)) > cap) break;

                                        int dBr = q.BackgroundColor.R - bg.R; if ((dBr ^ (dBr >> 31)) > cap) break;
                                        int dBg = q.BackgroundColor.G - bg.G; if ((dBg ^ (dBg >> 31)) > cap) break;
                                        int dBb = q.BackgroundColor.B - bg.B; if ((dBb ^ (dBb >> 31)) > cap) break;

                                        fgOk = PaintCompressor.ColorsCloseEnough(q.ForegroundColor, fg, thrSq);
                                        bgOk = PaintCompressor.ColorsCloseEnough(q.BackgroundColor, bg, thrSq);
                                    }
                                }
                            }

                            if (!fgOk || !bgOk) break;
                            x++;
                        }

                        runsOnLine.Add(new Run(start, x - start, fg, bg, under));
                    }

                    for (int i = 0; i < runsOnLine.Count; i++)
                    {
                        if (paintBuilder.Length >= charBudget) break;
                        var run = runsOnLine[i];
                        EmitRunOptimized(bitmap, y, run, paintBuilder);
                    }
                }

                PaintQoS.BeginWrite();
                fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                PaintQoS.EndWrite(paintBuilder.Length);
                break;
            }
            catch (IOException) when (attempts++ < 2) { continue; }
            catch (ArgumentOutOfRangeException) when (attempts++ < 2) { continue; }
        }
        return true;
    }





    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitRunOptimized(ConsoleBitmap bm, int y, Run run, PaintBuffer pb)
    {
        ref var st = ref _ansi;

        // Cursor
        if (st.CursorX != run.Start + 1 || st.CursorY != y + 1)
        {
            Ansi.Cursor.Move.ToLocation(run.Start + 1, y + 1, pb);
            st.CursorX = run.Start + 1;
            st.CursorY = y + 1;
        }

        // Colors – force both on first use
        if (!st.HasColor)
        {
            Ansi.Color.Foreground.Rgb(run.FG, pb);
            Ansi.Color.Background.Rgb(run.BG, pb);
            st.Fg = run.FG;
            st.Bg = run.BG;
            st.HasColor = true;
        }
        else
        {
            if (run.FG != st.Fg)
            {
                Ansi.Color.Foreground.Rgb(run.FG, pb);
                st.Fg = run.FG;
            }
            if (run.BG != st.Bg)
            {
                Ansi.Color.Background.Rgb(run.BG, pb);
                st.Bg = run.BG;
            }
        }

        // Underline toggle
        if (run.Underlined != st.Under)
        {
            pb.Append(run.Underlined ? Ansi.Text.UnderlinedOn : Ansi.Text.UnderlinedOff);
            st.Under = run.Underlined;
        }

        pb.AppendRunFromPixels(bm.Pixels, bm.Width, run.Start, y, run.Length);
        st.CursorX += run.Length;
    }
}

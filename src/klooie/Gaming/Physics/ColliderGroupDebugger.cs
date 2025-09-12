
#if DEBUG
using System.Drawing;
using System.Drawing.Imaging;

namespace klooie.Gaming;

public static class ColliderGroupDebugger
{
    public static Event<VelocityEvent> VelocityEventOccurred { get; private set; } 
    private static string OutputDir { get; set; }

    private static ColliderGroup group;

    public static bool TryInit(ColliderGroup group, string outputDir, ILifetime lt)
    {
        ColliderGroupDebugger.group = group;
        if (VelocityEventOccurred != null) return false;
        lt.OnDisposed(() => VelocityEventOccurred = null);
        VelocityEventOccurred = Event<VelocityEvent>.Create();
        if (outputDir != null)
        {
            OutputDir = outputDir;
            if (Directory.Exists(OutputDir) == false) Directory.CreateDirectory(OutputDir);
            VelocityEventOccurred.Subscribe(HandleEvent, lt);
        }
        return true;
    }

    private static void HandleEvent(VelocityEvent ev)
    {
        var cycleDir = Path.Combine(OutputDir, group.WallClockNow.TotalSeconds.ToString().Replace(".","_"), ev.MovingObject.GetHashCode().ToString());
        if (Directory.Exists(cycleDir) == false)
        {
            Directory.CreateDirectory(cycleDir);
        }

        var thisFileNumber = Directory.GetFiles(cycleDir)
            .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out int _))
            .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f))).MaxOrDefault() + 1;

        var thisFilePath = Path.Combine(cycleDir, thisFileNumber.ToString() + ".png");

        var viz = ev.GetVisualizations();
        Visualize(viz, thisFilePath);
    }

    private static void Visualize(List<RectangleVizualization> viz, string path, float scale = 100)
    {
        var allRects = new List<RectF>();
        allRects.AddRange(viz.Select(v => v.Rectangle));

        var left = allRects.Min(r => r.Left);
        var right = allRects.Max(r => r.Right);
        var top = allRects.Min(r => r.Top);
        var bottom = allRects.Max(r => r.Bottom);


        var w = (int)(Math.Ceiling((right - left) * scale));
        var h = (int)(Math.Ceiling((bottom - top) * scale * 2));

        using (var bitmap = new Bitmap(w, h))
        using (Graphics g = Graphics.FromImage(bitmap)!)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            foreach (var rectangleViz in viz)
            {   
                var adjustedX = (rectangleViz.Rectangle.Left - left) * scale;
                var adjustedY = (rectangleViz.Rectangle.Top - top) * scale * 2;
                var adjustedW = rectangleViz.Rectangle.Width * scale;
                var adjustedH = rectangleViz.Rectangle.Height * scale * 2;
                g.FillRectangle(new SolidBrush(Color.FromArgb(rectangleViz.Alpha, rectangleViz.BackgroundColor.R, rectangleViz.BackgroundColor.G, rectangleViz.BackgroundColor.B)), adjustedX, adjustedY, adjustedW, adjustedH);

                if(rectangleViz.Angle.HasValue)
                {
                    // draw a line from the center of the rectangle to the edge
                    var adjustedRect = new RectF(adjustedX, adjustedY, adjustedW, adjustedH);
                    var center = adjustedRect.Center;
                    var endPoint = center.RadialOffset(rectangleViz.Angle.Value, adjustedRect.Width / 2);
                    g.DrawLine(new Pen(Color.FromArgb(rectangleViz.Alpha,0, 0, 0), 5), center.Left, center.Top, endPoint.Left, endPoint.Top);
                }
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}

public class RectangleVizualization
{
    public required RectF Rectangle { get; init; }
    public required RGB BackgroundColor { get; init; }
    public required byte Alpha { get; init; } = 255;

    public Angle? Angle { get; init; }
}

public abstract class VelocityEvent
{
    public required float NowSeconds { get; init; }
    public required GameCollider MovingObject { get; init; }
    public abstract List<RectangleVizualization> GetVisualizations();
}

public class AngleChange : VelocityEvent
{
    public required Angle From { get; init; }
    public required Angle To { get; init; }

    public override List<RectangleVizualization> GetVisualizations() => new List<RectangleVizualization>()
    {
        new RectangleVizualization()
        {
                Rectangle = MovingObject.Bounds,
                Alpha = 128,
                BackgroundColor = RGB.White,
                Angle = From
        },
        new RectangleVizualization()
        {
                Rectangle = MovingObject.Bounds,
                Alpha = 128,
                BackgroundColor = RGB.White,
                Angle = To
        }
    };
}

public class SuccessfulMove : VelocityEvent
{
    public required Angle Angle { get; init; }
    public required RectF From { get; init; }
    public required RectF To { get; init; }

    public override List<RectangleVizualization> GetVisualizations() => new List<RectangleVizualization>()
    {
        new RectangleVizualization()
        {
                Rectangle = From,
                Alpha = 128,
                BackgroundColor = RGB.Green,
                Angle = Angle
        },
        new RectangleVizualization()
        {
                Rectangle = To,
                Alpha = 128,
                BackgroundColor = RGB.Green,
                Angle = Angle
        }
    };
}

public class FailedMove : VelocityEvent
{
    public required Angle Angle { get; init; }
    public required RectF From { get; init; }
    public required RectF To { get; init; }
    public GameCollider Obstacle { get; init; }

    public override List<RectangleVizualization> GetVisualizations() => new List<RectangleVizualization>()
    {
        new RectangleVizualization()
        {
                Rectangle = From,
                Alpha = 128,
                BackgroundColor = RGB.Green,
                Angle = Angle
        },
        new RectangleVizualization()
        {
                Rectangle = To,
                Alpha = 128,
                BackgroundColor = RGB.Orange,
                Angle = Angle
        },
        new RectangleVizualization()
        {
            Rectangle = Obstacle.Bounds,
            Alpha = 128,
            BackgroundColor = RGB.Red,
            Angle = Obstacle.Velocity.angle,
        }
    };
}
#endif
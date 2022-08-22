namespace klooie;
/*
public readonly struct Circle
{
    public static int FindLineCircleIntersections(float cx, float cy, float radius, float x1, float y1, float x2, float y2, out float ox1, out float oy1, out float ox2, out float oy2)
    {
        float dx, dy, A, B, C, det, t;

        dx = x2 - x1;
        dy = y2 - y1;

        A = dx * dx + dy * dy;
        B = 2 * (dx * (x1 - cx) + dy * (y1 - cy));
        C = (x1 - cx) * (x1 - cx) +
            (y1 - cy) * (y1 - cy) -
            radius * radius;

        det = B * B - 4 * A * C;
        if ((A <= 0.0000001) || (det < 0))
        {
            // No real solutions.
            ox1 = float.NaN;
            ox2 = float.NaN;
            oy1 = float.NaN;
            oy2 = float.NaN;
            return 0;
        }
        else if (det == 0)
        {
            // One solution.
            t = -B / (2 * A);

            ox1 = x1 + t * dx;
            oy1 = y1 + t * dy;

            ox2 = float.NaN;
            oy2 = float.NaN;
            return 1;
        }
        else
        {
            // Two solutions.
            t = (float)((-B + Math.Sqrt(det)) / (2 * A));

            ox1 = x1 + t * dx;
            oy1 = y1 + t * dy;

            t = (float)((-B - Math.Sqrt(det)) / (2 * A));

            ox2 = x1 + t * dx;
            oy2 = y1 + t * dy;
            return 2;
        }
    }
}
*/
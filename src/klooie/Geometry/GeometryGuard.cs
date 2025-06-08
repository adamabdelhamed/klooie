namespace klooie;
internal static class GeometryGuard
{

    public static void ValidateFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || float.IsSubnormal(value)) throw new ArgumentException($"Invalid float value: {value}");
    }

    public static void ValidateFloats(float a, float b)
    {
        ValidateFloat(a);
        ValidateFloat(b);
    }

    public static void ValidateFloats(float a, float b, float c)
    {
        ValidateFloat(a);
        ValidateFloat(b);
        ValidateFloat(c);
    }

    public static void ValidateFloats(float a, float b, float c, float d)
    {
        ValidateFloat(a);
        ValidateFloat(b);
        ValidateFloat(c);
        ValidateFloat(d);
    }
}

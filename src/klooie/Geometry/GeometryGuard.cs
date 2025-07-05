namespace klooie;
internal static class GeometryGuard
{
    /*
    public static void ValidateFloat(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);

        // Extract exponent and mantissa
        int exponent = (bits >> 23) & 0xFF;
        int mantissa = bits & 0x7FFFFF;
        bool isNaN = (exponent == 0xFF) && (mantissa != 0);

        // Check for signaling NaN (exponent all ones, MSB of mantissa 0, but mantissa nonzero)
        bool isSignalingNaN = isNaN && ((mantissa & 0x400000) == 0);

        // Check for all bits set (0xFFFFFFFF) or all bits zero (0x00000000) (optional: zero is fine)
        if (bits == unchecked((int)0xFFFFFFFF) || isSignalingNaN)
        {
            throw new ArgumentException($"Unsafe or illegal float bit pattern: 0x{bits:X8}");
        }
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
    */
}

namespace klooie;
public static class PseudoRandom
{
    private static Random r = new Random();

    public static double NextDouble() => r.NextDouble();

    public static bool NextBool() => NextDouble() < .5f;

    public static int Next(int min, int exclusiveMax) => r.Next(min, exclusiveMax);

}

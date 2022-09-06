namespace klooie.Gaming.Code;
public class RandomDoubleDirective : Directive
{
    [ArgRequired]
    public string Id { get; set; }

    [ArgRequired]
    public List<double> Values { get; set; }

    private int index;

    private double _NextDouble()
    {
        var ret = Values[index];
        index = index == Values.Count - 1 ? 0 : index + 1;
        return ret;
    }

    private int _Next(int min, int max)
    {
        var range = (max - 1) - min;
        var d = _NextDouble();
        var increment = range * d;
        var ret = ConsoleMath.Round(min + increment);
        return ret;
    }

    public static double NextDouble(string id) => Game.Current
        .Rules.WhereAs<RandomDoubleDirective>()
        .Where(d => d.Id == id)
        .Single()
        ._NextDouble();

    public static int Next(string id, int min, int max) => Game.Current
        .Rules.WhereAs<RandomDoubleDirective>()
        .Where(d => d.Id == id)
        .Single()
        ._Next(min, max);
}

namespace klooie.Gaming;

public class ScoreComponent
{
    public string Id { get; set; }
    public float Value { get; set; }
    public float Weight { get; set; } = -1;
    public float WeightedScore => Value * (Weight * WeightBoostMultiplier);
    public bool NeedsToBeNormalized { get; set; } = true;
    public float WeightBoostMultiplier { get; set; } = 1f;
    public ScoreComponent WeighIfNotSet(float weight)
    {
        if (Weight == -1)
        {
            this.Weight = weight;
        }
        return this;
    }

    public override string ToString() => $"{Id} - {Value} X {Weight} = {WeightedScore}";
}

public class WanderScore
{
    public Angle Angle { get; set; }
    public List<ScoreComponent> Components { get; set; }

    public float FinalScore
    {
        get
        {
            var sum = 0f;
            var weights = 0f;
            for (int i = 0; i < Components.Count; i++)
            {
                sum += Components[i].WeightedScore;
                weights += Components[i].Weight;
            }

            return sum / weights;
        }
    }

    public override string ToString() => $"Angle: {Angle}, Score: {FinalScore}";

    private static IEnumerable<T> IterateThrough<T>(IEnumerable<IEnumerable<T>> items)
    {
        foreach (var enumerable in items)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }


    private static Random rand = new Random();
    public static Dictionary<Type, float> Mutate(Dictionary<Type, float> original)
    {
        var ret = new Dictionary<Type, float>();
        foreach (var val in original) ret.Add(val.Key, val.Value);

        var configs = ret.ToArray();
        var toMutate = configs[rand.Next(0, configs.Length)];
        var amount = rand.Next(1, 10) * .01f;
        var direction = rand.NextDouble() <= .5 ? -1 : 1;
        var newVal = toMutate.Value + (toMutate.Value * amount * direction);
        ret[toMutate.Key] = newVal;
        return ret;
    }


    private class WeightedLabelEqualityComparer : IEqualityComparer<WeightedLabel>
    {
        public static readonly WeightedLabelEqualityComparer Default = new WeightedLabelEqualityComparer();
        public bool Equals(WeightedLabel x, WeightedLabel y) => x.Hash == y.Hash;
        public int GetHashCode(WeightedLabel obj) => obj.Hash.GetHashCode();
    }

    private class WeightedLabel
    {
        public ConsoleString Label { get; set; }
        public float Weight { get; set; }


        public string Hash => (Label.StringValue + "-" + Weight);

    }

    private static ConsoleString Compress(float score)
    {
        score = (float)ConsoleMath.Round(score * 100);
        if (score == 100) return "$".ToGreen();

        var color = score > 70 ? RGB.Green :
                    score > 40 ? RGB.Yellow
                    : RGB.Red;

        return ("" + score).Replace("0.", "0").ToConsoleString(color);
    }

    public static void NormalizeScores(List<WanderScore> scores)
    {
        var allComponents = new HashSet<string>();
        foreach (var s in scores)
        {
            foreach (var c in s.Components)
            {
                if (c.NeedsToBeNormalized)
                {
                    allComponents.Add(c.Id);
                }
            }
        }

        foreach (var c in allComponents)
        {
            NormalizeScores(scores, c);
        }
    }

    public static void NormalizeScores(List<WanderScore> scores, string component)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (int i = 0; i < scores.Count; i++)
        {
            WanderScore? score = scores[i];
            ScoreComponent? cs = null;
            for (int j = 0; j < score.Components.Count; j++)
            {
                ScoreComponent? c = score.Components[j];
                if (c.Id == component)
                {
                    cs = c;
                    break;
                }
            }

            if (cs == null) continue;

            min = Math.Min(cs.Value, min);
            max = Math.Max(cs.Value, max);
        }

        var range = max - min;
        if (range == 0) return;

        for (int i = 0; i < scores.Count; i++)
        {
            WanderScore? score = scores[i];
            ScoreComponent? cs = null;
            for (int j = 0; j < score.Components.Count; j++)
            {
                ScoreComponent? c = score.Components[j];
                if (c.Id == component)
                {
                    cs = c;
                    break;
                }
            }
            if (cs == null) continue;

            var deltaFromMin = cs.Value - min;
            var percentage = deltaFromMin / range;
            cs.Value = percentage;
        }
    }

    public static ConsoleString MakeTable(List<WanderScore> scores)
    {
        //   Filter(scores);
        ConsoleTableBuilder builder = new ConsoleTableBuilder();
        var props = IterateThrough(scores
                .Select(s => s.Components))
                .OrderByDescending(c => c.Weight)
                .Select(c => new WeightedLabel() { Weight = c.Weight, Label = c.Id.ToYellow() })
                .Distinct(WeightedLabelEqualityComparer.Default)
                .ToList();
        var cols = new ConsoleString[]
        {
                    "Ang".ToYellow(),
                    "F".ToYellow(),
        }
            .Union(
                props
                .Take(4)
                .Select(l => l.Label.Length <= 3 ? l.Label : l.Label.Substring(0, 3)))
            .ToList();

        var rows = scores.Select(s =>
        {
            var ret = new List<ConsoleString>();
            ret.Add((ConsoleMath.Round(s.Angle.Value) + "").ToWhite());
            ret.Add((ConsoleMath.Round(100 * s.FinalScore) + "").ToWhite());
            foreach (var prop in props)
            {
                var cVal = s.Components.Where(c => c.Id == prop.Label.StringValue).FirstOrDefault();
                ret.Add(Compress(cVal.Value) + $",{(cVal.Weight * cVal.WeightBoostMultiplier + "").Replace("0.", ".")}".ToWhite());
            }
            return ret;
        }).ToList();

        return builder.FormatAsTable(cols, rows);
    }
}


namespace klooie.Gaming;

public class ScoreComponent : Recyclable
{
    public string Id { get; set; }
    public float Value { get; set; }
    public float Weight { get; set; } = -1;
    public float WeightedScore => Value * (Weight * WeightBoostMultiplier);
    public bool NeedsToBeNormalized { get; set; } = true;
    public float WeightBoostMultiplier { get; set; } = 1f;
    public ScoreComponent() { }
    public ScoreComponent WeighIfNotSet(float weight)
    {
        if (Weight == -1)
        {
            this.Weight = weight;
        }
        return this;
    }

    public static ScoreComponent Create() => ScoreComponentPool.Instance.Rent();

    protected override void OnReturn()
    {
        base.OnReturn();
        Id = null;
        Value = default;
        Weight = -1;
        NeedsToBeNormalized = true;
        WeightBoostMultiplier = 1f;
    }

    public override string ToString() => $"{Id} - {Value} X {Weight} = {WeightedScore}";
}

public static class DescendingScoreComparer
{
    private static Comparison<WanderScore> comparison = new Comparison<WanderScore>(Compare);
    private static int Compare(WanderScore x, WanderScore y) => y.FinalScore.CompareTo(x.FinalScore);
    public static void SortScores(RecyclableList<WanderScore> scores) => scores.Items.Sort(comparison);
}

public class WanderScore : Recyclable
{
    public Angle Angle { get; set; }
    public RecyclableList<ScoreComponent> Components { get; set; }

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

    public WanderScore() { }

    public static WanderScore Create() => WanderScorePool.Instance.Rent();

    protected override void OnInit()
    {
        Components = RecyclableListPool<ScoreComponent>.Instance.Rent();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Angle = default;
        for (var j = 0; j < Components.Count; j++)
        {
           Components[j].Dispose();
        }
        Components.Dispose();
    }

    public override string ToString() => $"Angle: {Angle}, Score: {FinalScore}";

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

    private static HashSet<string> sharedHashSet = new HashSet<string>();
    public static void NormalizeScores(List<WanderScore> scores)
    {
        sharedHashSet.Clear();
        foreach (var s in scores)
        {
            for(var i = 0; i < s.Components.Count; i++)
            {
                var c = s.Components[i];
                if (c.NeedsToBeNormalized)
                {
                    sharedHashSet.Add(c.Id);
                }
            }
        }

        foreach (var c in sharedHashSet)
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
}


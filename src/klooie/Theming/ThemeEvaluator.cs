using System.Reflection;

namespace klooie.Theming;
internal static class ThemeEvaluator
{
    public static ThemeApplicationTracker Apply(Style[] styles, ConsolePanel root = null, ILifetimeManager lt = null)
    {
        styles.For((s, i) => s.Index = i);
        var tracker = new ThemeApplicationTracker(styles);
        root = root ?? ConsoleApp.Current.LayoutRoot;
        lt = lt ?? root;

        // Evaluates the root, all descendents, and any future descendents

        EvaulateAllControls(root, lt, styles, tracker);
        ConsoleApp.Current.LayoutRoot.DescendentAdded.Subscribe(c =>
        {
            if (ShouldEvaluate(c, root) == false) return;
            EvaluateControl(c, lt, styles, tracker);
        }, lt);
        return tracker;
    }

    private static void EvaulateAllControls(ConsolePanel root, ILifetimeManager applyLifetime, Style[] styles, ThemeApplicationTracker tracker)
    {
        foreach (var control in GetAllControls())
        {
            if (ShouldEvaluate(control,root) == false) continue;
          
            EvaluateControl(control, applyLifetime, styles, tracker);
        }
    }

    private static void EvaluateControl(ConsoleControl c, ILifetimeManager lt, Style[] styles, ThemeApplicationTracker tracker) => c.GetType()
        .GetProperties()
        .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null)
        .ForEach(p => EvaluateProperty(c, p, styles, lt, tracker));

    private static void EvaluateProperty(ConsoleControl c, PropertyInfo property, Style[] styles, ILifetimeManager lt, ThemeApplicationTracker tracker)
    {
        Style mostSpecificStyle = null;
        int? highestScore = null;
        bool tagsNeedToBeMonitored = false;

        for (int i = 0; i < styles.Length; i++)
        {
            Style style = styles[i];
            int? score = ScoreForSpecificity(style, c, property.Name);

            if (score.HasValue)
            {
                if (!highestScore.HasValue || score.Value > highestScore.Value)
                {
                    highestScore = score;
                    mostSpecificStyle = style;
                }

                if (RequiresMonitoring(style, c))
                {
                    tagsNeedToBeMonitored = true;
                }
            }
        }

        if (mostSpecificStyle != null)
        {
            if (tagsNeedToBeMonitored)
            {
                tracker.MonitoredApplicationCounts[mostSpecificStyle.Index]++;
                var evalLifetime = Lifetime.EarliestOf(lt);
                MonitorTags(c, property, styles, lt, evalLifetime, tracker);
                mostSpecificStyle.ApplyPropertyValue(c, evalLifetime);
            }
            else if (highestScore > 0)
            {
                tracker.RawApplicationCounts[mostSpecificStyle.Index]++;
                mostSpecificStyle.ApplyPropertyValue(c, lt);
            }
        }
    }

    private static bool ShouldEvaluate(ConsoleControl c, ConsolePanel root)
    {
        var isInScope = c == root || IsInsideRoot(c, root);
        var shouldBeIgnored = ShouldBeIgnored(c, root);
         return isInScope == true && shouldBeIgnored == false;
    }

    private static int? ScoreForSpecificity(Style s, ConsoleControl c, string propertyName)
    {
        if (s.PropertyName != propertyName) return null;

        var typeChainDelta = TypeChainDelta(c, s.Type);
        if (typeChainDelta.HasValue == false) return null;

        var insideDelta = s.Within == null ? null : InsideOfDelta(c, s.Within);
        if (s.Within != null && insideDelta.HasValue == false) return null;

        var tagExpression = s.Tag == null ? null : BooleanExpressionParser.Parse(s.Tag);
        var tagsDictionary = c.Tags.ToDictionary(t => t, t => true);

        var insideTagDelta = s.WithinTag == null ? null : InsideOfDelta(c, s.WithinTag);

        // 0-100 points possible for type chain - higher score the closer you are to the concrete type
        float score = 100f * (1 - (typeChainDelta.Value / (float)TypeChainLength(c, s.Type)));

        // 0-100 additinal points if you have a matching IfInsideOf clause - higher score if you are closer to the derived type
        score += insideDelta.HasValue == false ? 0 : 100f * (1 - (insideDelta.Value / (float)ParentChainLength(c)));

        // 0-50 additinal points if you have a matching IfInsideOfTag clause - higher score if you are closer to the child
        score += insideTagDelta.HasValue == false ? 0 : 100f * (1 - (insideTagDelta.Value / (float)ParentChainLength(c)));

        // tie breaker for tags - they must be added even if their tag conditions don't match so that they can 
        // subscribe for tag changes that may enable them later
        if(ShouldBeDeferred(s,c))
        {
            score = -1;
        }

        return ConsoleMath.Round(score);
    }

    private static void MonitorTags(ConsoleControl c, PropertyInfo prop, Style[] styles, ILifetimeManager themeLt, Lifetime evalLifetime, ThemeApplicationTracker tracker)
    {
        // invalidate if any of my parents tags change
        foreach (var parent in ParentChain(c))
        {
            parent.TagsChanged.Subscribe(()=>
            {
                evalLifetime.Dispose();
                EvaluateProperty(c, prop, styles, themeLt, tracker);
            }, evalLifetime);
        }

        // invalidate if any of my tags change
        c.TagsChanged.Subscribe(()=>
        {
            evalLifetime.Dispose();
            EvaluateProperty(c, prop, styles, themeLt, tracker);
        }, evalLifetime);
    }

    public static bool ShouldBeDeferred(Style s, ConsoleControl c)
    {
        var tagExpression = s.Tag == null ? null : BooleanExpressionParser.Parse(s.Tag);
        var tagsDictionary = c.Tags.ToDictionary(t => t, t => true);
        if (tagExpression != null && tagExpression.Evaluate(tagsDictionary) == false) return true;

        var insideTagDelta = s.WithinTag == null ? null : InsideOfDelta(c, s.WithinTag);
        if (s.WithinTag != null && insideTagDelta.HasValue == false) return true;

        return false;
    }

    public static bool RequiresMonitoring(Style s, ConsoleControl c) => s.Tag != null || s.WithinTag != null;
    

    private static bool IsInsideRoot(ConsoleControl c, Container root)
    {
        var parent = c.Parent;
        while (parent != null)
        {
            if (parent == root) return true;
            parent = parent.Parent;
        }
        return false;
    }

    private static IEnumerable<ConsoleControl> GetAllControls()
    {
        yield return ConsoleApp.Current.LayoutRoot;
        foreach (var d in ConsoleApp.Current.LayoutRoot.Descendents)
        {
            yield return d;
        }
    }

    private static int? TypeChainDelta(ConsoleControl c, Type styleType)
    {
        var currentType = c.GetType();
        int delta = 0;
        while (currentType != styleType && currentType != null)
        {
            currentType = currentType.BaseType;
            delta++;
        }

        return currentType == null ? null : new int?(delta);
    }

    private static int TypeChainLength(ConsoleControl c, Type styleType)
    {
        var currentType = c.GetType();
        int count = 0;
        while (currentType != typeof(ConsoleControl) && currentType != null)
        {
            currentType = currentType.BaseType;
            count++;
        }

        return currentType != null ? count : throw new NotSupportedException("No type chain");
    }

    private static int ParentChainLength(ConsoleControl c)
    {
        var currentParent = c.Parent;
        int count = 0;
        while (currentParent != null)
        {
            count++;
            currentParent = currentParent.Parent;
        }
        return count;
    }

    public static IEnumerable<Container> ParentChain(ConsoleControl c)
    {
        var currentParent = c.Parent;
        while (currentParent != null)
        {
            yield return currentParent;
            currentParent = currentParent.Parent;
        }
    }

    private static bool ShouldBeIgnored(ConsoleControl c, ConsolePanel root)
    {
        var currentParent = c.Parent;
        while (currentParent != null && currentParent != root.Parent)
        {
            if(currentParent.GetType().Attrs<ThemeIgnoreAttribute>().Where(attr => Is(c,attr.ToIgnore)).Any())
            {
                return true;
            }
            currentParent = currentParent.Parent;
        }
        return false;
    }

    private static int? InsideOfDelta(ConsoleControl c, string ifInsideOfTag)
    {
        var parent = c.Parent;
        var delta = 0;
        while (parent != null)
        {
            delta++;
            if (parent.HasSimpleTag(ifInsideOfTag)) return delta;
            parent = parent.Parent;
        }
        return null;
    }

    private static int? InsideOfDelta(ConsoleControl c, Type ifInsideOf)
    {
        var parent = c.Parent;
        var delta = 0;
        while (parent != null)
        {
            if (Is(parent, ifInsideOf)) return delta;
            parent = parent.Parent;
            delta++;
        }
        return null;
    }

    private static bool Is(ConsoleControl c, Type t) => c.GetType() == t || c.GetType().IsSubclassOf(t);
}

internal class ThemeApplicationTracker
{
    public int[] MonitoredApplicationCounts { get; private set; }
    public int[] RawApplicationCounts { get; private set; }

    private Style[] styles;
    public ThemeApplicationTracker(Style[] styles)
    {
        this.styles = styles;
        MonitoredApplicationCounts = new int[styles.Length];
        RawApplicationCounts = new int[styles.Length];
        for (var i = 0; i < styles.Length; i++)
        {
            MonitoredApplicationCounts[i] =0;
            RawApplicationCounts[i] = 0;
        }
    }

    public IEnumerable<(Style Style, int MonitoredCount, int RawCount, int TotalCount)> GetUsage()
    {
        for (var i = 0; i < styles.Length; i++)
        {
            var mon = MonitoredApplicationCounts[i];
            var raw=RawApplicationCounts[i];
            var total = mon + raw;
            yield return (styles[i], mon, raw, total);
        }
    }

    public IEnumerable<Style> WhereNeverApplied() => GetUsage()
        .Where(u => u.TotalCount == 0)
        .Select(u => u.Style);
}
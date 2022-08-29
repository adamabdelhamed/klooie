using PowerArgs;
using System.Reflection;

namespace klooie.Theming;
internal static class ThemeEvaluator
{
    public static void Apply(Style[] styles, ConsolePanel root = null, ILifetimeManager lt = null)
    {
        root = root ?? ConsoleApp.Current.LayoutRoot;
        lt = lt ?? root.Manager;

        // Evaluates the root, all descendents, and any future descendents

        EvaulateAllControls(root, lt, styles);
        ConsoleApp.Current.ControlAdded.SubscribeForLifetime(c =>
        {
            if (IsInsideRoot(c, root) == false) return;
            EvaluateControl(c, lt, styles);
        }, lt);
    }

    private static void EvaulateAllControls(ConsolePanel root, ILifetimeManager applyLifetime, Style[] styles)
    {
        foreach (var control in GetAllControls())
        {
            if (control != root && IsInsideRoot(control, root) == false) continue;
            EvaluateControl(control, applyLifetime, styles);
        }
    }

    private static void EvaluateControl(ConsoleControl c, ILifetimeManager lt, Style[] styles) => c.GetType()
        .GetProperties()
        .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null)
        .ForEach(p => EvaluateProperty(c, p, styles, lt));

    private static void EvaluateProperty(ConsoleControl c, PropertyInfo property, Style[] styles, ILifetimeManager lt)
    {
        var applicableStyles = styles
            .Select(style => new { Style = style, Score = ScoreForSpecificity(style,c, property.Name) })
            .Where(t => t.Score.HasValue)
            .OrderBy(t => t.Score.Value);

        var mostSpecificStyle = applicableStyles.LastOrDefault();

        if (mostSpecificStyle != null)
        {
            var tagsNeedToBeMonitored = applicableStyles.Where(s => RequiresMonitoring(s.Style, c)).Any();

            if (tagsNeedToBeMonitored)
            {
                var evalLifetime = Lifetime.EarliestOf(lt);
                MonitorTags(c, property, styles, lt, evalLifetime);
                mostSpecificStyle.Style.ApplyPropertyValue(c, evalLifetime);
            }
            else if(mostSpecificStyle.Score > 0)
            {
                mostSpecificStyle.Style.ApplyPropertyValue(c, lt);
            }
            else
            {

            }
        }
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

    private static void MonitorTags(ConsoleControl c, PropertyInfo prop, Style[] styles, ILifetimeManager themeLt, Lifetime evalLifetime)
    {
        // invalidate if any of my parents tags change
        foreach (var parent in ParentChain(c))
        {
            parent.TagsChanged.SubscribeForLifetime(()=>
            {
                evalLifetime.Dispose();
                EvaluateProperty(c, prop, styles, themeLt);
            }, evalLifetime);
        }

        // invalidate if any of my tags change
        c.TagsChanged.SubscribeForLifetime(()=>
        {
            evalLifetime.Dispose();
            EvaluateProperty(c, prop, styles, themeLt);
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

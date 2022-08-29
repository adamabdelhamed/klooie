using PowerArgs;
namespace klooie.Theming;

/// <summary>
/// A fluent API for defining styles
/// </summary>
public class StyleBuilder
{
    private enum FrameType
    {
        Target,
        Tag,
        Within,
        WithinTag,
        Style,
        Pop,
    }

    private Stack<FrameType> stack = new Stack<FrameType>();
    private Type currentType;
    private string currentTag;
    private Type currentWithin;
    private string currentWithinTag;

    private List<Style> styles = new List<Style>();

    private StyleBuilder() { }

    /// <summary>
    /// Creates a new StyleBuilder
    /// </summary>
    /// <returns>a new StyleBuilder</returns>
    public static StyleBuilder Create() => new StyleBuilder();

    /// <summary>
    /// Instructs the builder to target the given control type
    /// </summary>
    /// <typeparam name="T">the type of control to build styles for</typeparam>
    /// <returns>this builder</returns>
    public StyleBuilder For<T>() where T : ConsoleControl
    {
        if (ArePendingStyles())
        {
            throw new InvalidOperationException($"You can't call {nameof(For)}() while there are pending styles. Did you forget to assign a style for the previous control type?");
        }
        ClearContext();
        stack.Push(FrameType.Target);
        currentType = typeof(T);
        return this;
    }

    /// <summary>
    /// Instructs the builder to target the given tag
    /// </summary>
    /// <param name="tag">the tag to target</param>
    /// <returns>this builder</returns>
    /// <exception cref="InvalidOperationException">If you forgot to call For() or if a tag is already set</exception>
    public StyleBuilder Tag(string tag)
    {
        if(currentType == null)
        {
            throw new InvalidOperationException($"Did you forget to call {nameof(For)}()");
        }

        if(currentTag != null)
        {
            throw new InvalidOperationException($"Tag already set. Did you forget to call {nameof(Clear)}()");
        }

        stack.Push(FrameType.Tag);
        currentTag = tag;
        return this;
    }

    /// <summary>
    /// Instructs the builder to target controls that have an anscestor of the given type
    /// </summary>
    /// <typeparam name="T">the anscestor type</typeparam>
    /// <returns>this builder</returns>
    /// <exception cref="InvalidOperationException">If a target is not set or if a previous Within is still pending</exception>
    public StyleBuilder Within<T>() where T : Container
    {
        if (currentType == null)
        {
            throw new InvalidOperationException($"Did you forget to call {nameof(For)}()");
        }

        if (currentWithin != null)
        {
            throw new InvalidOperationException($"Within already set. Did you forget to call {nameof(Clear)}()");
        }

        stack.Push(FrameType.Within);
        currentWithin = typeof(T);
        return this;
    }

    /// <summary>
    /// Instructs the builder to target controls that have an anscestor with the given tag
    /// </summary>
    /// <param name="tag">the anscestor tag</param>
    /// <returns>this builder</returns>
    /// <exception cref="InvalidOperationException">If a target is not set or if a previous WithinTag is still pending</exception>
    public StyleBuilder WithinTag(string tag)
    {
        if (currentType == null)
        {
            throw new InvalidOperationException($"Did you forget to call {nameof(For)}()");
        }

        if (currentWithinTag != null)
        {
            throw new InvalidOperationException($"WithinTag already set. Did you forget to call {nameof(Clear)}()");
        }

        stack.Push(FrameType.WithinTag);
        currentWithinTag = tag;
        return this;
    }

    /// <summary>
    /// Adds a style for the Foreground property with the given value
    /// </summary>
    /// <param name="color">the foreground color</param>
    /// <returns>this builder</returns>
    public StyleBuilder FG(RGB color) => Property(nameof(ConsoleControl.Foreground), color);

    /// <summary>
    /// Adds a style for the Background property with the given value
    /// </summary>
    /// <param name="color">the background color</param>
    /// <returns>this builder</returns>
    public StyleBuilder BG(RGB color) => Property(nameof(ConsoleControl.Background), color);

    /// <summary>
    /// Adds a style for the BorderColor property with the given value
    /// </summary>
    /// <param name="color">the border color</param>
    /// <returns>this builder</returns>
    public StyleBuilder Border(RGB color) => Property(nameof(BorderPanel.BorderColor), color);

    /// <summary>
    /// Adds a style for the FocusColor property with the given value
    /// </summary>
    /// <param name="color">the focus color</param>
    /// <returns>this builder</returns>
    public StyleBuilder Focus(RGB color) => Property(nameof(ConsoleControl.FocusColor), color);

    /// <summary>
    /// Adds a style for the given property with the given value
    /// </summary>
    /// <param name="name">the property name to style</param>
    /// <param name="value">the style value</param>
    /// <returns>this builder</returns>
    /// <exception cref="InvalidOperationException">if no target is set</exception>
    public StyleBuilder Property(string name, object value)
    {
        if (currentType == null)
        {
            throw new InvalidOperationException($"Did you forget to call {nameof(For)}()");
        }

        styles.Add(new Style(currentType,name, value, currentTag,currentWithin, currentWithinTag));
        stack.Push(FrameType.Style);
        return this;
    }

    /// <summary>
    /// Instructs the builder to clear its context so you can add rules
    /// for another control type
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public StyleBuilder Clear()
    {
        if (currentType == null)
        {
            throw new InvalidOperationException($"You can only call {nameof(Clear)} when there is a target on the stack");
        }

        if (ArePendingStyles())
        {
            throw new InvalidOperationException($"You can't call {nameof(Clear)}() while there are pending styles. Did you forget to assign a style for the previous control type?");
        }

        int popCount = 0;
        while (stack.Skip(popCount).First() != FrameType.Target)
        {
            popCount++;
        }

        var advancedCount = 0;
        for(var i = 0; i < popCount; i++)
        {
            Pop(false, out int advanced, (2 * i) - advancedCount);
            advancedCount += advanced;
        }
        return this;
    }

    /// <summary>
    /// Instructs the builder to forget the previous scoping instruction
    /// so that you can return to the previous scope
    /// </summary>
    /// <returns>this builder</returns>
    public StyleBuilder Pop() => Pop(true, out int ignored, skip: 1);

    private StyleBuilder Pop(bool assertNoPendingStyles, out int advanced, int skip = 0)
    {
        advanced = 0;
        if (currentType == null)
        {
            throw new InvalidOperationException($"You can only call {nameof(Pop)} when there is a target on the stack");
        }

        if (assertNoPendingStyles && ArePendingStyles())
        {
            throw new InvalidOperationException($"You can't call {nameof(Pop)}() while there are pending styles. Did you forget to assign a style for the previous control type?");
        }

        if (stack.None()) throw new InvalidOperationException("Nothing to pop");
        var popped = stack.Skip(skip).First();

        if (popped == FrameType.Tag)
        {
            currentTag = null;
        }
        else if (popped == FrameType.Target)
        {
            currentType = null;
        }
        else if (popped == FrameType.Within)
        {
            currentWithin = null;
        }
        else if (popped == FrameType.WithinTag)
        {
            currentWithinTag = null;
        }
        else if(popped == FrameType.Style)
        {
            stack.Push(FrameType.Pop);
            return this;
        }
        else if(popped == FrameType.Pop)
        {
            advanced = 1;
            Pop(assertNoPendingStyles, out advanced, skip + 1);
            return this;
        }
        else
        {
            throw new Exception("Unknown pop type: " + popped);
        }

        stack.Push(FrameType.Pop);
        return this;
    }

    public Style[] ToArray() => styles.ToArray();


    private bool ArePendingStyles()
    {
        foreach(var entry in stack)
        {
          //  if (entry == FrameType.Pop) return true;
            if (entry == FrameType.Style) return false;
            if (entry == FrameType.Target) return true;
        }
        return false;
    }

    private void ClearContext()
    {
        currentType = null;
        currentTag = null;
        currentWithin = null;
        currentWithinTag = null;
    }
}
using klooie;

public abstract class BaseGridEditor<TGrid, TItem> where TGrid : BeatGrid<TItem> where TItem : class
{
    protected abstract TGrid Grid { get; }
    protected List<TItem> Clipboard = new();
    protected CommandStack CommandStack;

    public BaseGridEditor(CommandStack commandStack)
    {
        CommandStack = commandStack;
    }

    public virtual bool HandleKeyInput(ConsoleKeyInfo k)
    {
        if (Matches(k, ConsoleKey.A, ctrl: true)) return SelectAll();
        if (Matches(k, ConsoleKey.D, ctrl: true)) return DeselectAll();
        if (Matches(k, ConsoleKey.LeftArrow, ctrl: true) || Matches(k, ConsoleKey.RightArrow, ctrl: true)) return SelectAllLeftOrRight(k);
        if (Matches(k, ConsoleKey.C, shift: true)) return Copy();
        if (Matches(k, ConsoleKey.V, shift: true)) return Paste();
        if (Matches(k, ConsoleKey.Delete)) return DeleteSelected();
        if (Matches(k, ConsoleKey.LeftArrow, alt: true) || Matches(k, ConsoleKey.RightArrow, alt: true) || Matches(k, ConsoleKey.UpArrow, alt: true) || Matches(k, ConsoleKey.DownArrow, alt: true))  return MoveSelection(k);
        if (Matches(k, ConsoleKey.Z, ctrl: true)) return Undo();
        if (Matches(k, ConsoleKey.Y, ctrl: true)) return Redo();

        return false;
    }

    protected bool Matches(ConsoleKeyInfo k, ConsoleKey key, bool ctrl = false, bool shift = false, bool alt = false)
         => k.Key == key && (!ctrl || k.Modifiers.HasFlag(ConsoleModifiers.Control))
                         && (!shift || k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                         && (!alt || k.Modifiers.HasFlag(ConsoleModifiers.Alt))
                         && (ctrl ? k.Modifiers.HasFlag(ConsoleModifiers.Control) : !k.Modifiers.HasFlag(ConsoleModifiers.Control))
                         && (shift ? k.Modifiers.HasFlag(ConsoleModifiers.Shift) : !k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                         && (alt ? k.Modifiers.HasFlag(ConsoleModifiers.Alt) : !k.Modifiers.HasFlag(ConsoleModifiers.Alt));
    

    // --- ABSTRACT/OVERRIDABLES ---
    protected abstract List<TItem> GetSelectedValues();
    protected abstract List<TItem> GetAllValues();
    protected abstract void RefreshVisibleCells();
    protected abstract void FireStatusChanged(ConsoleString message);

    // --- SELECTION ---
    private bool SelectAll()
    {
        var sel = GetSelectedValues();
        sel.Clear();
        sel.AddRange(GetAllValues());
        RefreshVisibleCells();
        FireStatusChanged("All items selected".ToWhite());
        return true;
    }
    private bool DeselectAll()
    {
        GetSelectedValues().Clear();
        RefreshVisibleCells();
        FireStatusChanged("Deselected all items".ToWhite());
        return true;
    }
    protected virtual bool SelectAllLeftOrRight(ConsoleKeyInfo k) => false;

    // --- CLIPBOARD ---
    private bool Copy()
    {
        Clipboard.Clear();
        var sel = GetSelectedValues();
        Clipboard.AddRange(DeepCopyClipboard(sel));
        FireStatusChanged($"Copied {sel.Count} items to clipboard".ToWhite());
        return true;
    }
    private bool Paste()
    {
        if (Clipboard.Count == 0) return true;
        return PasteClipboard();
    }
    protected abstract IEnumerable<TItem> DeepCopyClipboard(IEnumerable<TItem> src);
    protected abstract bool PasteClipboard();

    // --- DELETE ---
    protected abstract bool DeleteSelected();

    // --- MOVE ---
    protected abstract bool MoveSelection(ConsoleKeyInfo k);

    // --- UNDO/REDO ---
    protected virtual bool Undo()
    {
        CommandStack.Undo();
        return true;
    }
    protected virtual bool Redo()
    {
        CommandStack.Redo();
        return true;
    }
}

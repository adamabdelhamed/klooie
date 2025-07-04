﻿namespace klooie;

/// <summary>
/// A control that lets the user view and edit the current value among a set of options.
/// </summary>
public partial class Dropdown : ProtectedConsolePanel
{
    private Label valueLabel;
    private bool isOpen;

    public List<DialogChoice> Options { get; private set; } = new List<DialogChoice>();

    /// <summary>
    /// The currently selected option
    /// </summary>
    public partial DialogChoice Value { get; set; }

    /// <summary>
    /// If true then W will be treated as Up and S will be treated as down.
    /// </summary>
    public bool EnableWAndSKeysForUpDown { get; set; }

    public List<ConsoleKeyInfo> StandardKeysToOpen { get; set; } = new List<ConsoleKeyInfo> { ConsoleKey.Enter.KeyInfo(), ConsoleKey.DownArrow.KeyInfo() };

    /// <summary>
    /// Creates a new Dropdown
    /// <param name="options">the options to display</param>
    /// </summary>
    public Dropdown(IEnumerable<DialogChoice> options)
    {
        this.Options.AddRange(options);
        Value = this.Options.FirstOrDefault();
        CanFocus = true;
        Height = 1;
        Width = 15;
        valueLabel = ProtectedPanel.Add(new Label());

        SubscribeToAnyPropertyChange(this, SyncValueLabel, this);
        SyncValueLabel();
        Focused.Subscribe(SyncValueLabel, this);
        Unfocused.Subscribe(SyncValueLabel, this);

        this.KeyInputReceived.Subscribe(async (k) =>
        {
            if (isOpen) return;

            foreach(var key in StandardKeysToOpen)
            {
                if (k.Key == key.Key && k.Modifiers == key.Modifiers)
                {
                    await Open();
                    return;
                }
            }
            if (EnableWAndSKeysForUpDown && (k.Key == ConsoleKey.W || k.Key == ConsoleKey.S))
            {
                await Open();
            }
        }, this);
    }

    private void SyncValueLabel(object me)
    {
        var _this =  me as Dropdown;
        _this.SyncValueLabel();
    }
    private void SyncValueLabel()
    {
        var text = Value.DisplayText.StringValue;
        if (text.Length > Width - 3 && Width > 0)
        {
            text = text.Substring(0, Math.Max(0, Width - 3));
        }

        while (text.Length < Width - 2 && Width > 0)
        {
            text += " ";
        }

        if (HasFocus || isOpen)
        {
            text += isOpen ? "^ " : "v ";
        }
        else
        {
            text += "  ";
        }

        valueLabel.Text = HasFocus ? text.ToBlack(RGB.Cyan) : isOpen ? text.ToCyan(RGB.DarkGray) : text.ToWhite();
    }

    private async Task Open()
    {
        isOpen = true;
        SyncValueLabel();
        Unfocus();
        try
        {
            var appropriatePopupWidth = 2 + 1 + Options.Select(o => o.DisplayText.Length).Max() + 1 + 1 + 2;
            var scrollPanel = new ScrollablePanel();
            scrollPanel.Width = appropriatePopupWidth - 4;
            scrollPanel.Height = Math.Min(8, Options.Count);

            var optionsStack = scrollPanel.ScrollableContent.Add(new StackPanel());
            optionsStack.Height = Options.Count;
            optionsStack.Width = scrollPanel.Width - 3;
            optionsStack.X = 1;
            optionsStack.AddRange(Options.Select(option => new Label() { CanFocus = true, Text = option.DisplayText, Tag = option }));
            scrollPanel.ScrollableContent.Width = optionsStack.Width + 2;
            scrollPanel.ScrollableContent.Height = optionsStack.Height;

            var popup = new BorderPanel(scrollPanel) { FocusStackDepth = this.Parent.FocusStackDepth + 1, BorderColor = RGB.DarkCyan };
            popup.AddTag(nameof(Dropdown));
            popup.Width = scrollPanel.Width + 4;
            popup.Height = scrollPanel.Height + 2;
            popup.X = this.AbsoluteX;
            popup.Y = this.AbsoluteY + 1;
            ConsoleApp.Current.LayoutRoot.Add(popup);

            var index = Options.IndexOf(Value);

            Action syncSelectedIndex = () =>
            {
                var labels = optionsStack.Children.WhereAs<Label>().ToArray();
                for (var i = 0; i < Options.Count; i++)
                {
                    labels[i].Text = Options[i].DisplayText;

                        // This value won't show so we need to invert its colors
                        if (labels[i].Text.Where(c => c.BackgroundColor == popup.Background && c.ForegroundColor == popup.Background).Count() == labels[i].Text.Length)
                    {
                        labels[i].Text = new ConsoleString(labels[i].Text.Select(c => new ConsoleCharacter(c.Value, c.ForegroundColor.GetCompliment(), popup.Background)));
                    }

                }

                var label = optionsStack.Children.ToArray()[index] as Label;
                label.Focus();
                label.Text = label.Text.ToBlack(bg: RGB.Cyan);
            };
            syncSelectedIndex();

            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, () =>
            {
                Value = Options[index];
                popup.Dispose();
            }, popup);

             ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, ()=> popup.TryDispose("Escape"), popup);

            Action up = () =>
            {
                if (index > 0)
                {
                    index--;
                    syncSelectedIndex();
                }
                else
                {
                    index = Options.Count - 1;
                    syncSelectedIndex();
                }
            };

            Action down = () =>
            {
                if (index < Options.Count - 1)
                {
                    index++;
                    syncSelectedIndex();
                }
                else
                {
                    index = 0;
                    syncSelectedIndex();
                }
            };

            if (EnableWAndSKeysForUpDown)
            {
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.W, up, popup);
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.S, down, popup);
            }

            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, up, popup);
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, down, popup);
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Tab, ConsoleModifiers.Shift, up, popup);
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Tab, down, popup);

            await popup.AsTask();
        }
        finally
        {
            isOpen = false;
            SyncValueLabel();
            Focus();
        }
    }
}

public abstract partial class Dropdown<T> : ProtectedConsolePanel
{
    public partial T Value { get; set; }

    public Dropdown()
    {
        CanFocus = false;
        Width = 15;
        var dropdown = ProtectedPanel.Add(new Dropdown(Choices())).Fill();
        dropdown.ValueChanged.Subscribe(() => this.Value = (T)dropdown.Value.Value, this);
        this.ValueChanged.Subscribe(() => dropdown.Value = dropdown.Options.Where(o => o.Value.Equals(Value)).Single(), this);
    }

    protected abstract IEnumerable<DialogChoice> Choices();
}

public class EnumDropdown<T> : Dropdown<T> where T : Enum
{
    protected override IEnumerable<DialogChoice> Choices() => Enums
    .GetEnumValues<T>()
    .Select(e => new DialogChoice()
    {
        DisplayText = e.ToConsoleString(),
        Id = e.ToString(),
        Value = e,
    });
}
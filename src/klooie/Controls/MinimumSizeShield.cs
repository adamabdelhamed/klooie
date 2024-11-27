namespace klooie;
public class MinimumSizeShieldOptions
{
    public int MinWidth { get; set; }
    public int MinHeight { get; set; }
    public Action OnMinimumSizeMet { get; set; }
    public Action OnMinimumSizeNotMet { get; set; }
}

public class MinimumSizeShield : ConsolePanel
{
    private MinimumSizeShieldOptions options;
    private Label messageLabel;
    private Lifetime tooSmallLifetime;
    private ConsoleControl focusThief;
    public MinimumSizeShield(MinimumSizeShieldOptions options)
    {
        this.options = options;
        IsVisible = false;
        messageLabel = this.Add(new Label()).CenterBoth();
        BoundsChanged.Subscribe(CheckSize, this);
        ZIndex = int.MaxValue;
    }

    private void CheckSize()
    {
        if (Application == null) return;
        if (Width < options.MinWidth || Height < options.MinHeight)
        {
            if (tooSmallLifetime == null)
            {
                tooSmallLifetime = new Lifetime();
                IsVisible = true;

                focusThief = Add(new ConsoleControl() { IsVisible = false, CanFocus = false, FocusStackDepth = this.FocusStackDepth + 1 });
                Application.ClearFocus();
                Application.PushKeyForLifetime(ConsoleKey.Escape, () => { }, this);
                options.OnMinimumSizeNotMet?.Invoke();
                ConsoleApp.Current.Invoke(OnTooSmall);
            }
        }
        else
        {
            IsVisible = false;
            if (tooSmallLifetime != null)
            {
                tooSmallLifetime.Dispose();
                tooSmallLifetime = null;
                focusThief?.Dispose();
                options.OnMinimumSizeMet?.Invoke();
            }
            else
            {
                options.OnMinimumSizeMet?.Invoke();
            }
        }
    }

    private async Task OnTooSmall()
    {
        while (tooSmallLifetime != null && tooSmallLifetime.IsExpired == false)
        {
            ConsoleString msg = ConsoleString.Empty;
            if (Width >= 75)
            {
                var widthNeeded = options.MinWidth - Width;
                var heightNeeded = options.MinHeight - Height;
                if (widthNeeded > 0 && heightNeeded > 0)
                {
                    var colStr = widthNeeded == 1 ? "column" : "columns";
                    var rowStr = heightNeeded == 1 ? "row" : "rows";
                    msg = $"Please zoom out or make the screen {widthNeeded} {colStr} wider and {heightNeeded} {rowStr} taller".ToYellow(Background);
                }
                else if (widthNeeded > 0)
                {
                    var colStr = widthNeeded == 1 ? "column" : "columns";
                    msg = $"Please zoom out or make the screen {widthNeeded} {colStr} wider".ToYellow(Background);
                }
                else if (heightNeeded > 0)
                {
                    var rowStr = heightNeeded == 1 ? "row" : "rows";
                    msg = $"Please zoom out or make the screen {heightNeeded} {rowStr} taller".ToYellow(Background);
                }
                else
                {
                    msg = "Error evaluating minimun screen size".ToRed(Background);
                }
            }
            else if (Width >= 9)
            {
                msg = "Too small".ToYellow(Background);
            }
            else
            {
                msg = "".ToYellow(Background);
            }

            messageLabel.Text = msg;
            await Task.Yield();
        }
    }
}
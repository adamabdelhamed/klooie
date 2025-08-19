using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class SingleNoteEditor : ProtectedConsolePanel
{
    private static LazyPool<SingleNoteEditor> _pool = new(() => new SingleNoteEditor());
    private SingleNoteEditor() { }
    public static SingleNoteEditor Create() => _pool.Value.Rent().Construct();

    private Event _noteChanged;
    public Event NoteChanged => _noteChanged ??= Event.Create();

    private int _midiNote = 36;
    private int _velocity = 127;
    private double _durationSeconds = 1.0;

    public int MidiNote { get => _midiNote; set => SetInt(ref _midiNote, value, 0, 127, UpdateNoteLabel);  }
    public int Velocity { get => _velocity; set => SetInt(ref _velocity, value, 0, 127, UpdateVelocityLabel); }
    public double DurationSeconds { get => _durationSeconds; set => SetDouble(ref _durationSeconds, value, 0.05, 30.0, UpdateDurationLabel); }
    public NoteExpression NoteExpression => NoteExpression.Create(MidiNote, DurationSeconds, Velocity);

    private ConsoleStringRenderer noteLabel;
    private ConsoleStringRenderer velocityLabel;
    private ConsoleStringRenderer durationLabel;
    private ConsoleStringRenderer titleLabel;
    private Recyclable? focusLifetime;

    private SingleNoteEditor Construct()
    {
        CanFocus = true;
        SetDefaults();
        Focused.Subscribe(OnFocused, this);
        Unfocused.Subscribe(OnUnfocused, this);

        var stack = ProtectedPanel.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both }).DockToTop(padding: 1).DockToLeft(padding: 2);

        titleLabel = stack.Add(RentLabel("Single Note Editor".ToWhite()));
        stack.Add(RentLabel());

        stack.Add(RentLabel(nameof(MidiNote).ToYellow()));
        noteLabel = stack.Add(RentLabel("".ToWhite()));
        stack.Add(RentLabel());
        UpdateNoteLabel();

        stack.Add(RentLabel(nameof(NoteExpression.Velocity).ToYellow()));
        velocityLabel = stack.Add(RentLabel("".ToWhite()));
        stack.Add(RentLabel());
        UpdateVelocityLabel();

        stack.Add(RentLabel(nameof(NoteExpression.DurationTime).ToYellow()));
        durationLabel = stack.Add(RentLabel("".ToWhite()));
        UpdateDurationLabel();

        return this;
    }

    private ConsoleStringRenderer RentLabel(ConsoleString? initialValue = null)
    {
        initialValue = initialValue ?? ConsoleString.Empty;
        // visual tree will dispose when the panel is disposed
        var ret = ConsoleStringRendererPool.Instance.Rent();
        ret.Content = initialValue;
        return ret;
    }

    private void OnUnfocused()
    {
        focusLifetime?.Dispose();
        focusLifetime = null;
        titleLabel.Content = titleLabel.Content.ToWhite(RGB.Black);

        UpdateNoteLabel();
        UpdateVelocityLabel();
        UpdateDurationLabel();
    }

    private void OnFocused()
    {
        focusLifetime = DefaultRecyclablePool.Instance.Rent();
        titleLabel.Content = titleLabel.Content.ToBlack(RGB.Cyan);
        RegisterKeys();
        UpdateNoteLabel();
        UpdateVelocityLabel();
        UpdateDurationLabel();
    }

    private void RegisterKeys()
    {
        Bind(ConsoleKey.UpArrow, IncrementNoteNumber);
        Bind(ConsoleKey.W, IncrementNoteNumber);
        Bind(ConsoleKey.DownArrow, DecrementNoteNumber);
        Bind(ConsoleKey.S, DecrementNoteNumber);

        Bind(ConsoleKey.UpArrow, IncrementVelocity, ConsoleModifiers.Alt);
        Bind(ConsoleKey.W, IncrementVelocity, ConsoleModifiers.Alt);
        Bind(ConsoleKey.DownArrow, DecrementVelocity, ConsoleModifiers.Alt);
        Bind(ConsoleKey.S, DecrementVelocity, ConsoleModifiers.Alt);

        Bind(ConsoleKey.RightArrow, IncrementDuration);
        Bind(ConsoleKey.D, IncrementDuration);
        Bind(ConsoleKey.LeftArrow, DecrementDuration);
        Bind(ConsoleKey.A, DecrementDuration);
    }

    private void Bind(ConsoleKey key, Action action, ConsoleModifiers modifiers = ConsoleModifiers.None) => ConsoleApp.Current.PushKeyForLifetime(key, modifiers, action, focusLifetime);
    private void IncrementNoteNumber() => MidiNote = Math.Min(MidiNote + 1, 127);
    private void DecrementNoteNumber() => MidiNote = Math.Max(MidiNote - 1, 0);
    private void IncrementVelocity() => Velocity = Math.Min(Velocity + 1, 127);
    private void DecrementVelocity() => Velocity = Math.Max(Velocity - 1, 0);
    private void IncrementDuration() => DurationSeconds += 0.05;
    private void DecrementDuration() => DurationSeconds -= 0.05;
    private void UpdateNoteLabel() => noteLabel.Content = $"{MidiNoteHelper.NoteName(MidiNote).DisplayString}".ToWhite() + GetKeyHintLabel("WS");
    private void UpdateVelocityLabel() => velocityLabel.Content = $"{Velocity}".ToWhite() + GetKeyHintLabel("ALT + WS");
    private void UpdateDurationLabel()
    {
        if (durationLabel == null) return;
        durationLabel.Content = $"{DurationSeconds:0.00}".ToWhite() + GetKeyHintLabel("AD");
    }

    private ConsoleString GetKeyHintLabel(string hint) => HasFocus ?"  ".ToWhite() + $" {hint} ".ToBlack(RGB.Cyan) : ConsoleString.Empty;

    protected override void OnReturn()
    {
        _noteChanged?.Dispose();
        _noteChanged = null;
        SetDefaults();
        base.OnReturn();
    }

    private void SetDefaults()
    {
        MidiNote = 36; // Reset to default
        Velocity = 127; // Reset to default
        DurationSeconds = 1.0; // Reset to default
    }

    private void SetInt(ref int field, int newValue, int min, int max, Action onChanged)
    {
        var clamped = Math.Clamp(newValue, min, max);
        if (field != clamped)
        {
            field = clamped;
            onChanged();
            NoteChanged.Fire();
        }
    }

    private void SetDouble(ref double field, double newValue, double min, double max, Action onChanged)
    {
        var clamped = Math.Clamp(newValue, min, max);
        clamped = Math.Round(clamped, 2); // Snap to 2 decimal places
        if (Math.Abs(field - clamped) > 0.0001)
        {
            field = clamped;
            onChanged();
            NoteChanged.Fire();
        }
    }
}

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

    public Event NoteChanged { get; } = Event.Create();

    private int _midiNote = 36; // Default to C2
    private int _velocity = 127; // Default velocity
    public int MidiNote
    {
        get => _midiNote;
        set
        {
            if (_midiNote != value)
            {
                _midiNote = Math.Clamp(value, 0, 127);
                UpdateNoteLabel();
                NoteChanged.Fire();
            }
        }
    }
    public int Velocity
    {
        get => _velocity;
        set
        {
            if (_velocity != value)
            {
                _velocity = Math.Clamp(value, 0, 127);
                UpdateVelocityLabel();
                NoteChanged.Fire();
            }
        }
    }


    private ConsoleStringRenderer noteLabel;
    private ConsoleStringRenderer velocityLabel;
    private ConsoleStringRenderer titleLabel;
    private Recyclable? focusLifetime;
    public NoteExpression NoteExpression => NoteExpression.Create(MidiNote, 1, Velocity);
    private SingleNoteEditor Construct()
    {
        CanFocus = true;
        Focused.Subscribe(OnFocused, this);
        Unfocused.Subscribe(OnUnfocused, this);
        var stack = ProtectedPanel.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both }).DockToTop(padding:1).DockToLeft(padding: 2);

        titleLabel = stack.Add(new ConsoleStringRenderer("Single Note Editor".ToWhite()));
        stack.Add(new ConsoleStringRenderer("".ToWhite()));

        stack.Add(new ConsoleStringRenderer("Note".ToYellow()));
        noteLabel = stack.Add(new ConsoleStringRenderer("".ToWhite()));
        stack.Add(new ConsoleStringRenderer("".ToWhite()));
        UpdateNoteLabel();

        stack.Add(new ConsoleStringRenderer("Velocity".ToYellow()));
        velocityLabel = stack.Add(new ConsoleStringRenderer("".ToWhite()));
        UpdateVelocityLabel();
        return this;
    }

    private void OnUnfocused()
    {
        focusLifetime?.Dispose();
        focusLifetime = null;
        titleLabel.Content = titleLabel.Content.ToWhite(RGB.Black);
    }

    private void OnFocused()
    {
        focusLifetime = DefaultRecyclablePool.Instance.Rent();
        titleLabel.Content = titleLabel.Content.ToBlack(RGB.Cyan);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, IncrementNoteNumber, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.W, IncrementNoteNumber, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, DecrementNoteNumber, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.S, DecrementNoteNumber, focusLifetime);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, ConsoleModifiers.Alt, IncrementVelocity, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.W, ConsoleModifiers.Alt, IncrementVelocity, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, ConsoleModifiers.Alt, DecrementVelocity, focusLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.S, ConsoleModifiers.Alt, DecrementVelocity, focusLifetime);
    }

    private void IncrementNoteNumber()
    {
        MidiNote = Math.Min(MidiNote + 1, 127);
        UpdateNoteLabel();
        NoteChanged.Fire();
    }

    private void DecrementNoteNumber()
    {
        MidiNote = Math.Max(MidiNote - 1, 0);
        UpdateNoteLabel();
        NoteChanged.Fire();
    }

    private void IncrementVelocity()
    {
        Velocity = Math.Min(Velocity + 1, 127);
        UpdateVelocityLabel();
        NoteChanged.Fire();
    }

    private void DecrementVelocity()
    {
        Velocity = Math.Max(Velocity - 1, 0);
        UpdateVelocityLabel();
        NoteChanged.Fire();
    }

    private void UpdateNoteLabel()
    {
        var displayInfo = PianoPanel.NoteName(MidiNote);
        noteLabel.Content = $"{displayInfo.DisplayString}".ToWhite();
    }

    private void UpdateVelocityLabel()
    {
        velocityLabel.Content = $"{Velocity}".ToWhite();
    }
}

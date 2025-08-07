using System;
using System.Collections;
using System.Collections.Generic;

namespace klooie;

public class SongComposer : ProtectedConsolePanel
{
    private GridLayout layout;
    public TrackHeadersPanel TrackHeaders { get; private init; }
    public TrackGrid Grid { get; private init; }
    public StatusBar StatusBar { get; private init; }

    // Expose player if desired
    public BeatGridPlayer<MelodyClip> Player => Grid.Player;
    public IMidiProvider MidiProvider { get; private set; }

    public TrackGridEditor Editor { get; }

    public SongComposer(WorkspaceSession session, ConsoleControl commandBar, IMidiProvider midiProvider)
    {
        this.MidiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        var rowSpecPrefix = commandBar == null ? "1r" : "1p;1r";
        var rowOffset = commandBar == null ? 0 : 1;
        // 16p for track headers, rest for grid
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", "16p;1r")).Fill();

        // Add the track headers (left, all rows except command/status)
        TrackHeaders = layout.Add(new TrackHeadersPanel(this, session), 0, rowOffset);
        Editor = new TrackGridEditor(this, session.Commands);
        Grid = layout.Add(new TrackGrid(session, Editor, midiProvider), 1, rowOffset);

        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset + 1, columnSpan: 2);

        if (commandBar != null)
        {
            layout.Add(commandBar, 0, 0, columnSpan: 2);
        }

        Grid.StatusChanged.Subscribe(message => StatusBar.Message = message, this);
        SetupKeyForwarding();
    }

    private void SetupKeyForwarding()
    {
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(OnGlobalKeyPressed, this);
    }

    private void OnGlobalKeyPressed(ConsoleKeyInfo info)
    {
        if (ConsoleApp.Current.FocusedControl != Grid && TryForwardToGrid(info)) return;
        // Add more forwarding logic here if needed
    }

    private bool TryForwardToGrid(ConsoleKeyInfo info)
    {
        if(IsHorizontalArrow(info) || info.Key == ConsoleKey.Spacebar || info.Key == ConsoleKey.M)
        {
            Grid.KeyInputReceived.Fire(info);
            Grid.Focus();
            return true;
        }
        return false;
    }

    private bool IsHorizontalArrow(ConsoleKeyInfo info) => info.Key == ConsoleKey.LeftArrow || info.Key == ConsoleKey.RightArrow;
}



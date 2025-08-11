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
    private StackPanel commandBar;
    public SongComposer(WorkspaceSession session, IMidiProvider midiProvider)
    {
        this.MidiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };
        var rowSpecPrefix =  "1p;1r";
        var rowOffset =  1;
        // 16p for track headers, rest for grid
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", "16p;1r")).Fill();

        // Add the track headers (left, all rows except command/status)
        TrackHeaders = layout.Add(new TrackHeadersPanel(this, session), 0, rowOffset);
        Editor = new TrackGridEditor(this, session.Commands);
        Grid = layout.Add(new TrackGrid(session, Editor, midiProvider), 1, rowOffset);

        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset + 1, columnSpan: 2);

        layout.Add(commandBar, 0, 0, columnSpan: 2);
        ExportSongUXHelper.SetupExport(Grid.Compose, commandBar);
        AddBPMDropdown();
        AddSynthTweakerCommand();
        Grid.StatusChanged.Subscribe(message => StatusBar.Message = message, this);
        SetupKeyForwarding();
    }

    private void AddSynthTweakerCommand()
    {
        var uiHint = commandBar.Add(new ConsoleStringRenderer(ConsoleString.Parse("[B=Cyan][Black] F10 [D][White] Instruments")));
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Key != ConsoleKey.F10) return;
            if (MidiProvider.TryConnect(MidiProvider.GetProductNames()[0], out IMidiInput input) == false) return;
            var maxDepth = Math.Max(ConsoleApp.Current.LayoutRoot.FocusStackDepth, ConsoleApp.Current.LayoutRoot.Descendents.Max(d => d.FocusStackDepth));
            var tweaker = ConsoleApp.Current.LayoutRoot.Add(new SynthTweakerPanel(input, WorkspaceSession.Current.CurrentSong.BeatsPerMinute) { FocusStackDepth = maxDepth+1 }).Fill();
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => tweaker.Dispose(), tweaker);
        }, uiHint);
    }

    private void AddBPMDropdown()
    {
        double[] options = [30, 60, 75, 90, 120, 180, 240];
        var dd = commandBar.Add(new Dropdown(options.Select(o => new DialogChoice() { DisplayText = $"{o} BPM".ToWhite(), Id = o.ToString(), Value = o  })));
        var selected = dd.Options.Where(o => (double)o.Value == WorkspaceSession.Current.CurrentSong.BeatsPerMinute).SingleOrDefault();
        if(selected != null)
        {
            dd.Value = selected;
        }

        dd.ValueChanged.Subscribe(() =>
        {
            if (UpdateBPMCommand.IsExecuting) return;
            WorkspaceSession.Current.Commands.Execute(new UpdateBPMCommand(this, dd, (double)dd.Value.Value));
        }, dd);
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
        if (ConsoleApp.Current.FocusStackDepth != Grid.FocusStackDepth) return false;
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



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class WorkspaceSession
{
    public static WorkspaceSession Current { get; set; } = null!;
    public required Workspace Workspace { get; init; }
    public CommandStack Commands { get; } = new();

    public SongInfo CurrentSong { get; set; }

    public async Task Initialize()
    {
        if (Workspace.Settings.LastOpenedSong != null)
        {
            CurrentSong = Workspace.Songs.FirstOrDefault(s => s.Title == Workspace.Settings.LastOpenedSong);
        }

        if(CurrentSong != null)
        {
            return;
        }

        await NewSong();

    }

    public async Task NewSong()
    {
        await Task.Yield();
        var songName = (await TextInputDialog.Show(new ShowTextInputOptions("Enter song name".ToYellow()) { AllowEscapeToClose = false }))?.ToString().Trim();
        if (string.IsNullOrWhiteSpace(songName))
        {
            await Initialize();
            return;
        }
        CurrentSong = new SongInfo()
        {
            Title = songName,
            BeatsPerMinute = 60,
            Tracks = new List<ComposerTrack>()
                {
                    new ComposerTrack("Track 1",  InstrumentPicker.GetAllKnownInstruments().First())
                }
        };
        Workspace.UpdateSettings(s => s.LastOpenedSong = CurrentSong.Title);
        Workspace.AddSong(CurrentSong);
    }

    public async Task<bool> OpenSong()
    {
        await Task.Yield();
        var songName = (await TextInputDialog.Show(new ShowTextInputOptions("Enter song name".ToYellow()) { AllowEscapeToClose = false }))?.ToString().Trim();
        if(string.IsNullOrWhiteSpace(songName)) return false;
        var choice = Workspace.Songs.FirstOrDefault(s => s.Title.Equals(songName, StringComparison.OrdinalIgnoreCase));
        if (choice == null) return false;

        CurrentSong = choice;
        Workspace.UpdateSettings(s => s.LastOpenedSong = CurrentSong.Title);
        return true;
    }
}
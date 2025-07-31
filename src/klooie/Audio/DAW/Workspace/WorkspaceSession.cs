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
        else
        {
            var songName = (await TextInputDialog.Show(new ShowTextInputOptions("Enter song name".ToYellow()) { AllowEscapeToClose = false }))?.ToString().Trim();
            if (string.IsNullOrWhiteSpace(songName))
            {
                await Initialize();
                return;
            }
            CurrentSong = new SongInfo() { Title = songName, BeatsPerMinute = 60, Notes = new ListNoteSource() { BeatsPerMinute = 60 } };
            Workspace.Settings.LastOpenedSong = songName;
            Workspace.AddSong(CurrentSong);
        }
    }
}
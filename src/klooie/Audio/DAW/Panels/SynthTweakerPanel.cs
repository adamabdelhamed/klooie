using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using klooie;

public class SynthTweakerPanel : ProtectedConsolePanel
{
    private GridLayout layout;
    private Label codeLabel;
    private Menu<SynthTweaker.PatchFactoryInfo> patchMenu;
    private SingleNoteEditor noteEditor;
    private SynthTweaker? tweaker;
    private string? currentPath;
    private SynthTweaker.PatchFactoryInfo? currentPatch => patchMenu?.SelectedItem;
    private SynthTweakerSettings settings;


    private static string SettingsFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SynthTweakerSettings.json");

    public SynthTweakerPanel(IMidiInput midiInput, double bpm = 60)
    {
        // Layout: 1 row, 2 columns: code on left, melody on right
        layout = ProtectedPanel.Add(new GridLayout("1r", "30p;1r;25p")).Fill();
        var codeContainer = layout.Add(new ConsolePanel(), 1, 0);
        codeLabel = codeContainer.Add(new Label()).Fill(new Thickness(2,2,1,1));
        codeLabel.CompositionMode = CompositionMode.BlendBackground;
        codeLabel.Background = new RGB(20,20,20);
        codeContainer.Background = codeLabel.Background;
        noteEditor = layout.Add(SingleNoteEditor.Create(), 2, 0);
        noteEditor.NoteChanged.Subscribe(PlayCurrentNote, noteEditor);
        this.Ready.SubscribeOnce(ListenForFileOpenShortcut);
        LoadSettings();
        noteEditor.MidiNote = settings.LatestMidiNote;
        noteEditor.Velocity = settings.LatestVelocity;
        if (settings.LatestSourcePath != null && File.Exists(settings.LatestSourcePath))
        {
            this.Ready.SubscribeOnce(()=> LoadFile(settings.LatestSourcePath));
        }



        ProtectedPanel.Add(new ConsoleStringRenderer(ConsoleString.Parse("[White]Press [B=Cyan][Black] ALT + O [D][White] to open a file.")) { CompositionMode = CompositionMode.BlendBackground })
            .CenterHorizontally()
            .DockToBottom(padding: 1);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Spacebar, PlayCurrentNote, this);
    }

    private void ListenForFileOpenShortcut() => ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.O, ConsoleModifiers.Alt, () => ConsoleApp.Current.Invoke(async()=> await OpenFileDialog()), this);
    

    private async Task OpenFileDialog()
    {
        var result = (await TextInputDialog.Show("Enter a path to a synth source file")).StringValue;
        if (string.IsNullOrWhiteSpace(result) || !File.Exists(result))
        {
            codeLabel.Text = "Invalid file path".ToRed();
            return;
        }
        LoadFile(result);
    }

    private void LoadFile(string path)
    {
        this.currentPath = path;
        tweaker?.Dispose();
        tweaker = SynthTweaker.Create();
        tweaker.CodeChanged.Subscribe(code => codeLabel.Text = code.ToDifferentBackground(codeLabel.Background), tweaker);
        tweaker.PatchesCompiled.Subscribe(patches =>
        {
            patchMenu = layout.Add(new Menu<SynthTweaker.PatchFactoryInfo>(patches), 0, 0);
            var found = false;
            for (var i = 0; i < patches.Count; i++)
            {
                if (patches[i].Name == currentPatch?.Name)
                {
                    patchMenu.SelectedIndex = i;
                    found = true;
                    break;
                }
            }

            if (found == false && patches.Count > 0) patchMenu.SelectedIndex = 0;

            if(patches.Count == 1)
            {
                noteEditor.Focus();
            }
            else
            {
                patchMenu.Focus();
            }    

            PlayCurrentNote();
        }, tweaker);
        tweaker.Initialize(path);
        settings.LatestSourcePath = path;
        SaveSettings();
    }

    private void PlayCurrentNote()
    {
        if (currentPatch == null || noteEditor.NoteExpression == null) return;
        settings.LatestMidiNote = noteEditor.MidiNote;
        settings.LatestVelocity = noteEditor.Velocity;
        SaveSettings();
        var noteExpression = noteEditor.NoteExpression
            .WithInstrument(InstrumentExpression.Create("Current Patch", currentPatch.Factory))
            .WithDuration(1);
        var noteList = NoteCollection.Create(noteExpression);
        var song = new Song(noteList, 60);
        SoundProvider.Current.Play(song);
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(settings);
        if(!Directory.Exists(Path.GetDirectoryName(SettingsFilePath))) Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, json);
    }

    private void LoadSettings()
    {
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                if(string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Settings file is empty.");
                if (!json.Trim().StartsWith("{") || !json.Trim().EndsWith("}")) throw new InvalidOperationException("Settings file is not in valid JSON format.");
                settings = JsonSerializer.Deserialize<SynthTweakerSettings>(json);
                return;
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }
        settings = new SynthTweakerSettings() { LatestMidiNote = 36, LatestVelocity = 127 };
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        tweaker?.Dispose();
    }
}

public class SynthTweakerSettings
{
    public string? LatestSourcePath { get; set; }
    public int LatestMidiNote { get; set; }
    public int LatestVelocity { get; set; }
}
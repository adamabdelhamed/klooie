using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using klooie;

public class SynthTweakerPanel : ProtectedConsolePanel
{
    private GridLayout layout;
    private Label codeLabel;
    private MelodyMaker melodyMaker;
    private SynthTweaker? tweaker;
    private string? currentPath;

    public MelodyMaker MelodyMaker => melodyMaker;

    private static readonly SynthTweakerSettings settings = LoadSettings();

    private static SynthTweakerSettings LoadSettings()
    {
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<SynthTweakerSettings>(json) ?? new SynthTweakerSettings();
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine($"Failed to load settings: {ex.Message}");
                return new SynthTweakerSettings();
            }
        }
        return new SynthTweakerSettings();
    }

    private static string SettingsFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SynthTweakerSettings.json");

    public SynthTweakerPanel(IMidiInput midiInput, double bpm = 60)
    {
        // Layout: 1 row, 2 columns: code on left, melody on right
        layout = ProtectedPanel.Add(new GridLayout("1r", "90p;1r")).Fill();
        codeLabel = layout.Add(new Label("Press ALT + O to open a file.".ToWhite()) , 0, 0);
        melodyMaker = layout.Add(new MelodyMaker(midiInput, bpm), 1, 0);
        this.Ready.SubscribeOnce(ListenForFileOpenShortcut);

        if(settings.LatestSourcePath != null && File.Exists(settings.LatestSourcePath))
        {
            LoadFile(settings.LatestSourcePath);
        }
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
        tweaker.CodeChanged.Subscribe(code => codeLabel.Text = code, tweaker);
        tweaker.Initialize(path, melodyMaker.Notes, melodyMaker.BeatsPerMinute);
        tweaker.PatchCompiled.Subscribe(_ =>
        {
            if(melodyMaker.Player.CurrentBeat > melodyMaker.Timeline.NoteSource.Select(n => n.StartBeat).DefaultIfEmpty(0).Max())
            {
                melodyMaker.Player.Seek(0);
            }
            melodyMaker.Timeline.Player.StopAtEnd = true;
            melodyMaker.StartPlayback();
        }, melodyMaker);
        var newNotes = melodyMaker.Notes.Select(n => n.WithInstrument(InstrumentExpression.Create("Synth", tweaker.CurrentFactory.Factory)));
        (melodyMaker.Notes as ListNoteSource).Clear();
        (melodyMaker.Notes as ListNoteSource).AddRange(newNotes);
        melodyMaker.Timeline.InstrumentFactory = () => tweaker.CurrentFactory?.Factory();
        settings.LatestSourcePath = path;
        SaveSettings();
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(settings);
        if(!Directory.Exists(Path.GetDirectoryName(SettingsFilePath))) Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, json);
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
}
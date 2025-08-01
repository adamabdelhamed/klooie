using klooie;
using PowerArgs;

var app = new ConsoleApp();


app.Invoke(() => app.FocusChanged.Sync(() => app.WriteLine($"Focused control is of type: {app.FocusedControl?.GetType().Name ?? "null"}".ToBlack()),app));

app.Invoke(async () =>
{
    app.Sound = new AudioPlaybackEngine();
    var workspace = await Workspace.Bootstrap();
    WorkspaceSession.Current = new WorkspaceSession() { Workspace = workspace };
    var midi = MIDIInput.Create();
    var dawPanel = ConsoleApp.Current.LayoutRoot.Add(new DAWPanel(WorkspaceSession.Current, midi)).Fill();
});

app.Run();

 
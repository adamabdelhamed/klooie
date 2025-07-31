using klooie;
 
var app = new ConsoleApp();

app.Invoke(async () =>
{
    app.Sound = new AudioPlaybackEngine();
    var workspace = await Workspace.Bootstrap();
    var session = new WorkspaceSession() { Workspace = workspace };
    var midi = MIDIInput.Create();
    var dawPanel = ConsoleApp.Current.LayoutRoot.Add(new DAWPanel(session, midi)).Fill();
});

app.Run();

 
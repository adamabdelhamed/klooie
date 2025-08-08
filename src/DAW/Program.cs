using klooie;
using PowerArgs;
File.WriteAllText(@"C:\Users\adama\OneDrive\Desktop\doc.html", SynthDocGenerator.GenerateHtml());
File.WriteAllText(@"C:\Users\adama\OneDrive\Desktop\doc.md", SynthDocGenerator.GenerateMarkdown());
var app = new ConsoleApp();
app.Invoke(async () =>
{
    app.Sound = new AudioPlaybackEngine();
    var workspace = await Workspace.OpenMRUOrNew();
    WorkspaceSession.Current = new WorkspaceSession() { Workspace = workspace };
    var midi = MIDIInput.Create();
    var dawPanel = ConsoleApp.Current.LayoutRoot.Add(new DAWPanel(WorkspaceSession.Current, midi)).Fill();
});

app.Run();

 
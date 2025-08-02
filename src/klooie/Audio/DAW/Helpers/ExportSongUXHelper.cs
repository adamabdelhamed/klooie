using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class ExportSongUXHelper
{
    private static bool IsExporting = false;
    public static void SetupExport(Func<Song> songGetter, StackPanel hintPanel)
    {
        var uiHint = hintPanel.Add(new ConsoleStringRenderer(ConsoleString.Parse("[B=Cyan][Black] ALT + X [D][White] Export")));
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Modifiers != ConsoleModifiers.Alt || k.Key != ConsoleKey.X) return;
            if (IsExporting) return; // Prevent multiple setups
            IsExporting = true;
            try
            {
                await PerformExport(songGetter);
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine(ex.Message.ToRed());
            }
            finally
            {
                IsExporting = false;
            }
        }, uiHint);
    }

    private static async Task PerformExport(Func<Song> songGetter)
    {
        await Task.Yield();
        var filePath = (await TextInputDialog.Show(new ShowTextInputOptions("Enter output file path (.wav)".ToYellow()) { DialogWidth = 60, DialogHeight = 10, SpeedPercentage = 0, TextBoxFactory = () =>
        {
            var tb = new TextBox() { Background = RGB.White, Foreground = RGB.Black };
            tb.Value = WorkspaceSession.Current.Workspace.Settings.LastExportedWavFilePath?.ToBlack() ?? "output.wav".ToBlack();
            return tb;
        }}))?.ToString();
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (filePath.StartsWith("\"") && filePath.EndsWith("\"")) filePath = filePath[1..^1]; // Remove quotes
        if (filePath.EndsWith(".wav") == false) filePath += ".wav";
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(dir)) filePath = Path.Combine(Environment.CurrentDirectory, filePath);
        else if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var song = songGetter();
        var maxDescendentDepth = ConsoleApp.Current.LayoutRoot.Descendents.Select(d => d.FocusStackDepth).Max();
        var depth = 1 + Math.Max(maxDescendentDepth, ConsoleApp.Current.LayoutRoot.FocusStackDepth);
        var filter = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = depth }).Fill();
        filter.Add(new ConsoleStringRenderer("Exporting... Please wait...".ToWhite()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();

        try
        {
            var analysis = await ScheduledSignalSourceMixer.ToWav(song, filePath);
            WorkspaceSession.Current.Workspace.UpdateSettings(s => s.LastExportedWavFilePath = filePath);
            var realTimeConfidence = analysis.RealtimeConfidence();
            ConsoleApp.Current.WriteLine($"Exported file is {(realTimeConfidence * 100).ToString("N0")}% realtime safe".ToConsoleString(realTimeConfidence > .98f ? RGB.DarkGreen : realTimeConfidence > .95f ? RGB.Orange : RGB.Red));
        }
        finally
        {
            filter.Dispose();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class ExportSongUXHelper
{
    public static void SetupExport(Func<Song> songGetter, StackPanel hintPanel)
    {
        var uiHint = hintPanel.Add(new ConsoleStringRenderer(ConsoleString.Parse("[B=Cyan][Black] ALT + X [D][White] Export")));
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            try
            {
                if (k.Modifiers != ConsoleModifiers.Alt || k.Key != ConsoleKey.X) return;
                await Task.Yield();
                var filePath = (await TextInputDialog.Show("".ToYellow()))?.ToString();
                if (string.IsNullOrWhiteSpace(filePath)) return;
                if (filePath.StartsWith("\"") && filePath.EndsWith("\"")) filePath = filePath[1..^1]; // Remove quotes
                if (Directory.Exists(Path.GetDirectoryName(filePath)) == false) Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                var song = songGetter();
                var maxDescendentDepth = ConsoleApp.Current.LayoutRoot.Descendents.Select(d => d.FocusStackDepth).Max();
                var depth = 1 + Math.Max(maxDescendentDepth, ConsoleApp.Current.LayoutRoot.FocusStackDepth);
                var filter = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = depth }).Fill();
                filter.Add(new ConsoleStringRenderer("Exporting... Please wait...".ToWhite()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
                var analysis = await ScheduledSignalSourceMixer.ToWav(song, filePath);
                filter.Dispose();
                var realTimeConfidence = analysis.RealtimeConfidence();
                ConsoleApp.Current.WriteLine($"Exported file is {(realTimeConfidence * 100).ToString("N0")}% realtime safe".ToConsoleString(realTimeConfidence > .98f ? RGB.DarkGreen : realTimeConfidence > .95f ? RGB.Orange : RGB.Red));
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine(ex.Message.ToRed());
            }
        }, uiHint);
    }
}
using klooie;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Focus;

internal class FocusSamples
{
    public void GlobalKeyHandlerSample()
    {
//#Sample -id PushKeyForLifetime
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Spacebar, () =>
         {
             // code in here runs whenever spacebar is pressed.
         }, ConsoleApp.Current);
//#EndSample
    }

    public void FocusEventSample()
    {
//#Sample -id FocusEvents
        var textBox = new TextBox();
        textBox.Focused.Subscribe(() =>
        {
            // runs whenever the text box gets focus
        }, textBox);
        textBox.Unfocused.Subscribe(() =>
        {
            // runs whenever the text box loses focus
        }, textBox);
//#EndSample
    }
}

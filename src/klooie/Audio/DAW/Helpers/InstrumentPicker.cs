using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class InstrumentPicker
{
    public static Dropdown CreatePickerDropdown() => new Dropdown(GetAllKnownInstruments().Select(i => new DialogChoice() { DisplayText = i.Name.ToWhite(), Id = i.Name, Value = i }));

    public static IEnumerable<InstrumentExpression> GetAllKnownInstruments()
    {
        yield return new InstrumentExpression() { Name = "Synth Lead", PatchFunc = SynthLead.Create };
        yield return new InstrumentExpression() { Name = "Kick", PatchFunc = DrumKit.Kick };
        yield return new InstrumentExpression() { Name = "Snare", PatchFunc = DrumKit.Snare };
        yield return new InstrumentExpression() { Name = "Clap", PatchFunc = DrumKit.Clap };
    }
}

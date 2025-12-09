using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class InstrumentPicker
{
    private static Dictionary<string,InstrumentExpression> GetInstrumentMap()
    {
        return GetAllKnownInstruments().ToDictionary(i => i.Name, i => new InstrumentExpression()
        {
            Name = i.Name,
            PatchFunc = i.PatchFunc
        });
    }

    public static InstrumentExpression ResolveInstrument(string name)
    {
        var instrumentMap = GetInstrumentMap();
        if (instrumentMap.TryGetValue(name, out var instrument))
        {
            return instrument;
        }
        return new InstrumentExpression() { Name = name, PatchFunc = SynthLead.Create };
    }

    public static Dropdown CreatePickerDropdown() => new Dropdown(GetAllKnownInstruments().Select(i => new DialogChoice() { DisplayText = i.Name.ToWhite(), Id = i.Name, Value = i }));

    public static IEnumerable<InstrumentExpression> GetAllKnownInstruments()
    {
        yield return new InstrumentExpression() { Name = "Synth Lead", PatchFunc = SynthLead.Create };
        yield return new InstrumentExpression() { Name = "Bass", PatchFunc = Bass.Basic};
        yield return new InstrumentExpression() { Name = "Synth Lead (Slim)", PatchFunc = SynthLead.CreateSlim };
        yield return new InstrumentExpression() { Name = "Kick", PatchFunc = DrumKit.Kick };
        yield return new InstrumentExpression() { Name = "Snare", PatchFunc = DrumKit.Snare };
        yield return new InstrumentExpression() { Name = "Clap", PatchFunc = DrumKit.Clap };
        yield return new InstrumentExpression() { Name = "Pad", PatchFunc = SoftPad.Create};
        yield return new InstrumentExpression() { Name = "Toy Box", PatchFunc = ToyBox.Create };
    }
 
}

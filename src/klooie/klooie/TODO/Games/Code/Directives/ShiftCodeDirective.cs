using PowerArgs;

namespace klooie.Gaming.Code;
public class ShiftCodeDirective : Directive
{

    [ArgRequired]
    public float Amount { get; set; }
}

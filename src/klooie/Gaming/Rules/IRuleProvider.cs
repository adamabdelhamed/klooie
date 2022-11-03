namespace klooie.Gaming;

public interface IRuleProvider
{
    IRule[] Rules { get; }
}

public class ArrayRulesProvider : IRuleProvider
{
    public static readonly IRuleProvider Empty = new ArrayRulesProvider(new IRule[0]);
    public IRule[] Rules { get; private set; }
    public ArrayRulesProvider(IRule[] rules) => this.Rules = rules;
}


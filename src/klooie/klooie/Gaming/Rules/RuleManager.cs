namespace klooie.Gaming;

internal class RuleManager
{
    private IRuleProvider ruleProvider;
    private IRule[] rules;
    private List<IRule> dynamicRules;

    public IEnumerable<IRule> Rules
    {
        get
        {
            foreach (var rule in rules) yield return rule;
            foreach (var rule in dynamicRules) yield return rule;
        }
    }

    public IRuleProvider Provider => ruleProvider;

    public RuleManager(IRuleProvider ruleProvider)
    {
        this.ruleProvider = ruleProvider;
        this.dynamicRules = new List<IRule>();
    }

    public async Task AddDynamicDirective(IRule r)
    {
        await r.ExecuteAsync();
        dynamicRules.Add(r);
    }

    public async Task Startup()
    {
        rules = ruleProvider.Rules;
        foreach (var rule in Rules)
        {
            await rule.ExecuteAsync();
        }
    }
}
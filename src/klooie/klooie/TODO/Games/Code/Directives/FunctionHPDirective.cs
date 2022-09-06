namespace klooie.Gaming.Code;
public class FunctionHPDirective : FunctionDirective
{
    [ArgDefaultValue(10)]
    public float HP { get; set; } = 10;
    [ArgDefaultValue(0)]
    public float RunnableThreshold { get; set; } = 0;
    public string OnDisabled { get; set; }
    public bool DisposeOnDisabled { get; set; }
    public List<string> Tags { get; set; }

    protected override Task OnFunctionIdentified(Function myFunction)
    {

        var myCode = Game.Current.GamePanel.Controls
            .WhereAs<CodeControl>()
            .Where(c => c.Token != null && c.Token.IsWithin(myFunction))
            .OrderBy(c => c.Token.Line)
            .ThenBy(c => c.Token.Column)
            .ToList();


        foreach (var code in myCode)
        {
            if (Tags != null && Tags.Count > 0)
            {
                code.AddTags(Tags);
            }

            code.Power.HP = HP;
            if (RunnableThreshold < 1)
            {
                var codeEl = code;
                codeEl.Power.Subscribe(nameof(PowerInfo.HP), () =>
                {
                    var completeness = myCode.Where(c => c.Power.HP > 0).Count() / (float)myCode.Count;
                    var couldRun = myFunction.CanExecute;
                    myFunction.CanExecute = completeness >= RunnableThreshold;

                    var healthPercentage = completeness - RunnableThreshold;
                    if (healthPercentage > 0)
                    {
                        Game.Current.GamePanel.Add(new HPUpdate(healthPercentage * 100, 100, myCode.First(), 0));
                    }
                    if (myFunction.CanExecute && couldRun == false)
                    {
                        foreach (var el in myCode)
                        {
                            el.IsDimmed = false;
                        }
                    }
                    else if (myFunction.CanExecute == false && couldRun)
                    {
                        Game.Current.Publish(OnDisabled, myFunction);
                        foreach (var el in myCode)
                        {
                            if (DisposeOnDisabled)
                            {
                                el.Dispose();
                            }
                            else
                            {
                                el.IsDimmed = true;
                            }
                        }
                    }

                }, codeEl);
            }
        }
        return Task.CompletedTask;
    }
}

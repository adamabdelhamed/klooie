using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class InlineWatchDirective : Directive
{

    [ArgRequired]
    [ArgPosition(0)]
    public DynamicArg Variable { get; set; }

    [ArgDefaultValue("null")]
    public string DefaultValue { get; set; } = "null";

    public DynamicArg DisplayName { get; set; }

    [ArgCantBeCombinedWith(nameof(DisplayName))]
    public DynamicArg Eval { get; set; }

    [ArgRequired(If = nameof(Eval))]
    public string EvalVariable { get; set; }

    public string Until { get; set; }

    public bool HideIfNull { get; set; }

    [ArgIgnore]
    public string EffectiveDisplay => DisplayName != null ? DisplayName.StringValue : Variable.StringValue;

    public bool AutoHide { get; set; }

    public override Task ExecuteAsync()
    {
       
        var element = Game.Current.GamePanel.Add(new InlineWatchLabel(this));

        if (Until != null)
        {
            Game.Current.Subscribe(Until, (ev2) =>
            {
                if (element.IsExpired == false)
                {
                    element.Dispose();
                }
            }, Game.Current);
        }

        return Task.CompletedTask;
    }

    public class InlineWatchLabel : NoFrillsLabel
    {
        private InlineWatchDirective directive;
        private TimeSpan lastUpdatedTime;
        public InlineWatchLabel(InlineWatchDirective directive) : base(FormatWatch(directive, directive.DefaultValue))
        {
            this.directive = directive;
            this.IsVisible = false;
            // Find the rightmost code just below the directive
            var anchor = Game.Current.GamePanel.Controls
                .WhereAs<CodeControl>()
                .Where(c => c.Token.Line > directive.Tokens.First().Line)
                .OrderBy(c => c.Token.Line)
                .ThenByDescending(c => c.Token.Column)
                .First();

            // Move the watch to the right of the anchor code
            this.MoveTo(anchor.Right() + 2, anchor.Top);

            // Update the value now and make sure we update it whenever it changes
            Heap.Current.Sync(directive.Variable.StringValue, UpdateValue, Game.Current);
            UpdateValue();
        }

        private void UpdateValue()
        {
            if (Heap.Current.TryGetValue(directive.Variable.StringValue, out object val))
            {
                val = val != null ? val.ToString() : directive.DefaultValue;

                if ("" + val == "True" || "" + val == "False")
                {
                    val = val.ToString().ToLower();
                }

            }
            else
            {
                val = directive.DefaultValue;
            }

            Text = FormatWatch(directive, val + "");
            lastUpdatedTime = Game.Current.MainColliderGroup.Now;
            IsVisible = true;

            if (directive.HideIfNull && "null".Equals(val + "", StringComparison.OrdinalIgnoreCase))
            {
                IsVisible = false;
            }

            if (directive.AutoHide)
            {
                Game.Current.Delay(2100).Then(() =>
                {
                    var elapsed = Game.Current.MainColliderGroup.Now - lastUpdatedTime;
                    if (elapsed >= TimeSpan.FromSeconds(2))
                    {
                        IsVisible = false;
                    }
                });
            }
        }

        private static ConsoleString FormatWatch(InlineWatchDirective d, string value)
        {
            if (d.Eval != null)
            {
                var evalResult = TimeThread.ResolveStatic("{{" + d.EvalVariable + "}}");
                var isTrue = ("" + (true.Equals(evalResult) || ("" + evalResult).Equals("true", StringComparison.OrdinalIgnoreCase))).ToLower();
                return ($" {d.Eval.StringValue} == {isTrue}").ToConsoleString(ConsoleColor.Black, isTrue == "true" ? ConsoleColor.Green : ConsoleColor.Gray);
            }
            else
            {
                return ($" {d.EffectiveDisplay} = {value} ").ToConsoleString(ConsoleColor.Black, ConsoleColor.Gray);
            }
        }
    }
}

using PowerArgs;

namespace klooie.Gaming;

public abstract class LooseItem : GameCollider
{
    public ConsoleString DisplayString { get; set; }

    public static Event<LooseItem> OnIncorporated { get; private set; } = new Event<LooseItem>();

    public Func<Character, bool> Filter { get; set; } = e => true;

    public Event Incorporated { get; private set; } = new Event();

    public IInventoryItem Item { get; private set; }

    public string Sound { get; set; } = "marketingpickup";

    public LooseItem(IInventoryItem item)
    {
        this.Item = item;
        Game.Current.Invoke(async () =>
        {
            await Game.Current.Delay(500);
            while (this.IsExpired == false)
            {
                Evaluate();
                await Task.Yield();
            }
        });

        var displayKey = item.GetType().Name + "DisplayName";
        var displayNameOverride = Game.Current.RuleVariables.ContainsKey(displayKey) ? Game.Current.RuleVariables.Get<object>(displayKey).ToString().Trim().ToGreen() : item.DisplayName;
        this.DisplayString = displayNameOverride.StringValue.ToBlack(RGB.Green);
        this.Background = DisplayString[0].BackgroundColor;
        this.ResizeTo(DisplayString.Length + 4, 3);
        this.Filter = e => e is MainCharacter;
    }

    private void Evaluate()
    {
        var touching = MainCharacter.Current != null && (MainCharacter.Current.Touches(this) || Game.Current.GamePanel.Controls
            .WhereAs<GameCollider>()
            .Where(el => el.Touches(this))
            .Any());

        if (touching)
        {
            SoundProvider.Current.Play(Sound);
            Incorporate(MainCharacter.Current);
            OnIncorporated.Fire(this);
            this.Dispose();
            Incorporated?.Fire();
        }

    }

    public abstract void Incorporate(Character c);

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(DisplayString, 2, 1);
}


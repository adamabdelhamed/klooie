using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.Gaming;

    public enum AimMode
    {
        Auto,
        Manual
    }

    public interface IInteractable
    {
        float MaxInteractDistance { get; }
        RectF InteractionPoint { get; }
        Task Interact(Character character);
    }

    public class Interactable : GameCollider, IInteractable
    {
        public float MaxInteractDistance { get; set; }

        public RectF InteractionPoint { get; set; }

        public Func<Character, Task> InteractFunc { get; set; }

        public Task Interact(Character character) => InteractFunc(character);
    }

public class MainCharacter : Character
{
    public static Event<Weapon> OnEquipWeapon { get; private set; } = new Event<Weapon>();


    public ConsoleColor Color { get; set; } = ConsoleColor.Magenta;

    [ThreadStatic]
    private static MainCharacter _current;
    public static MainCharacter Current
    {
        get
        {
            return _current;
        }
        private set
        {
            if (_current != null) throw new Exception("Already a Main character");
            _current = value;
        }
    }

    private static int NextId = 100;

    public MainCharacter()
    {
        this.Id = nameof(MainCharacter) + ": " + NextId++;
        this.AddTag(nameof(MainCharacter));
        this.MoveTo(0, 0);
        Current = this;
        this.OnDisposed(() =>
        {
            if (_current == this)
            {
                _current = null;
            }
        });


        this.Inventory.Subscribe(nameof(Inventory.PrimaryWeapon), () =>
        {
            if (Inventory.PrimaryWeapon != null) OnEquipWeapon.Fire(Inventory.PrimaryWeapon);
        }, this);

        this.Inventory.Subscribe(nameof(Inventory.ExplosiveWeapon), () =>
        {
            if (Inventory.PrimaryWeapon != null) OnEquipWeapon.Fire(Inventory.ExplosiveWeapon);
        }, this);
        InitializeTargeting(new AutoTargetingFunction(new AutoTargetingOptions()
        {
            Source = this,
            TargetTag = "enemy",
        }));
    }


    public void RegisterItemForPickup(GameCollider item, Action afterPickup)
    {
        this.Velocity.ImpactOccurred.Subscribe((i) =>
        {
            if (i.ColliderHit == item)
            {
                afterPickup();
                item.Dispose();
            }
        }, item);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        if (IsVisible == false)
        {
            context.Fill(new ConsoleCharacter(' ', RGB.Black));
            return;
        }
        char c;

        var angle = Velocity.Angle;

        c = angle.Arrow;
        context.DrawPoint(new ConsoleCharacter(c, Color), 0, 0);
    }
}

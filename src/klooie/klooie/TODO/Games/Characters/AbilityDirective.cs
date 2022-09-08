using klooie.Gaming.Code;
using System.Reflection;

namespace klooie.Gaming;
public interface IAbility : IInventoryItem
{
    void Initialize();
    void Enable(bool isNew);
    void Disable();
    bool AutoEarned { get; }
}

    public class AbilitiesDirective : Directive
    {
        public override Task ExecuteAsync()
        {
            Game.Current.Subscribe(Game.ReadyEventId, (ev) =>
            {
                var allAbilities = new List<IAbility>();
                foreach (var assembly in new Assembly[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() })
                {
                    foreach (var type in assembly.GetTypes().Where(t => t.IsAbstract == false && t.IsInterface == false && t.GetInterfaces().Contains(typeof(IAbility))))
                    {
                        var ability = (Activator.CreateInstance(type) as IAbility);
                        ability.Initialize();
                        allAbilities.Add(ability);
                    }
                }
                MainCharacterHelpers.ApplyWhenMainCharacterEnters(() =>
                {
                    foreach (var ability in allAbilities.Where(a => a.AutoEarned))
                    {
                        if (MainCharacter.Current.Inventory.Items.Where(i => i.GetType() == ability.GetType()).None())
                        {
                            MainCharacter.Current.Inventory.Items.Add(ability);
                        }
                    }

                    foreach (var ability in MainCharacter.Current.Inventory.Items.WhereAs<IAbility>().ToArray())
                    {
                        ability.Enable(false);
                    }

                    MainCharacter.Current.Inventory.Items.Added.Subscribe((item) =>
                    {
                        (item as IAbility)?.Enable(true);
                    }, Game.Current);
                    MainCharacter.Current.Inventory.Items.Removed.Subscribe((item) => (item as IAbility)?.Disable(), Game.Current);
                });


            }, Game.Current);
            return Task.CompletedTask;
        }
    }

    public class AbilityDirective : PlacementDirective
    {
        public string ElementId { get; set; }
        [ArgRequired]
        [AbilityTypeValidator]
        public string Type { get; set; }

        public string AfterPickup { get; set; }

        public bool AutoPickup { get; set; }

        public override Task OnEventFired(object args)
        {
            if (On.StringValue == Game.ReadyEventId || On.StringValue == "Visited" || On.StringValue == "Unvisited")
            {
                throw new ArgException("On should be InventoryHydrated");
            }

            var toPlace = CreateItem();
            toPlace.Id = ElementId;
            toPlace.Incorporated.SubscribeOnce(() => Game.Current.Publish(AfterPickup));

            if (AutoPickup)
            {
                MainCharacterHelpers.ApplyWhenMainCharacterEnters(() =>
                {
                    toPlace.Incorporate(MainCharacter.Current);
                    toPlace.Dispose();
                });
            }
            else
            {
                Game.Current.GamePanel.Add(toPlace);
                Place(toPlace);
            }

            return Task.CompletedTask;
        }

        public LooseAbility CreateItem()
        {
            var t = AbilityTypeValidator.GetAbilityType(Type);
            if (t != null)
            {
                var toPlace = new LooseAbility(Activator.CreateInstance(t) as IAbility);
                return toPlace;
            }
            else
            {
                throw new ArgException("Unknown ability type: " + Type);
            }
        }
    }

    public class AbilityTypeValidator : ArgValidator
    {
        public override void Validate(string name, ref string arg)
        {
            if (GetAbilityType(arg) == null)
            {
                throw new ValidationArgException($"Invalid ability type: {arg}");
            }
        }

        public static Type GetAbilityType(string typeName)
        {
            var comparison = StringComparison.OrdinalIgnoreCase;
            foreach (var assembly in new Assembly[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() })
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.Equals(typeName, comparison))
                    {
                        return type;
                    }
                    if (type.Name.Equals(typeName + "Ability", comparison))
                    {
                        return type;
                    }
                }
            }
            return null;
        }
    }

public class LooseAbility : LooseItem
{
    public LooseAbility(IAbility item) : base(item) { }

    public override void Incorporate(Character target)
    {
        var existingCount = target.Inventory.Items.Where(i => i.GetType() == Item.GetType()).Count();
        if (existingCount == 0 || Item.AllowMultiple)
        {
            target.Inventory.Items.Add(Item);
        }
    }
}

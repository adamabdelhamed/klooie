using PowerArgs;
namespace klooie.Gaming;
public abstract class UsableItem : IInventoryItem
{
    public abstract ConsoleString DisplayName { get; }
    public virtual Character Holder { get; set; }
    public abstract bool AllowMultiple { get; }
    protected abstract Task UseInternal();

    public async Task<bool> TryUse()
    {
        var i = Holder.Inventory.Items.IndexOf(this);
        if (i < 0) return false;

        Holder.Inventory.Items.RemoveAt(i);
        await UseInternal();
        return true;
    }
}

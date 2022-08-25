using PowerArgs;

namespace klooie;

public class DialogChoice
{
    /// <summary>
    /// The display text for the option
    /// </summary>
    public ConsoleString DisplayText { get; set; }

    /// <summary>
    /// The id of this option's value
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// An object that this option represents
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// Compares the ids of each option
    /// </summary>
    /// <param name="obj">the other option</param>
    /// <returns>true if the ids match</returns>
    public override bool Equals(object obj)
    {
        var b = obj as DialogChoice;
        if (b == null) return false;
        return b.Id == this.Id;
    }

    /// <summary>
    /// gets the hashcode of the id
    /// </summary>
    /// <returns>the hashcode of the id</returns>
    public override int GetHashCode() => Id == null ? base.GetHashCode() : Id.GetHashCode();

    public static IEnumerable<DialogChoice> OKCancel => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "OK".ToConsoleString(), Id = "OK", Value = "OK" },
        new DialogChoice(){ DisplayText = "Cancel".ToConsoleString(), Id = "Cancel", Value = "Cancel" },
    };

    public static IEnumerable<DialogChoice> YesNo => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "Yes".ToConsoleString(), Id = "Yes", Value = "Yes" },
        new DialogChoice(){ DisplayText = "No".ToConsoleString(), Id = "No", Value = "No" },
    };

    public static IEnumerable<DialogChoice> Close => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "Close".ToConsoleString(), Id = "Close", Value = "Close" },
    };
}
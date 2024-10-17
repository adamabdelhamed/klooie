namespace ScrollSucker;

public abstract class CutScene
{
    public float StartLocation { get; set; }
    public abstract Task Execute();
}


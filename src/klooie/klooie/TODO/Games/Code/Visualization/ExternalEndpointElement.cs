using PowerArgs;

namespace klooie.Gaming.Code;
public class ExternalEndpointElementOptions
{
    public string Id { get; set; }
    public ConsoleColor BorderColor { get; set; }
    public ConsoleString Label { get; set; }
    public EntryPointDirective EntryPoint { get; set; }
    public EntryPointDirective DirectiveOptions { get; set; }
}

public class ExternalEndpointElement : GameCollider
{
    public ExternalEndpointElementOptions Options { get; private set; }
    public RectF SpoutLocation => new RectF(this.Left - 1, this.Top + 1, 1, 1);

    public ExternalEndpointElement(ExternalEndpointElementOptions options)
    {
        this.Id = options.Id;
        this.MoveTo(Left, Top);
        this.Options = options;
        this.ResizeTo(3, 1);
    }

    public void StartThread(string localGroupId = null)
    {
        if (Options.EntryPoint.TargetFunction.CanRun == false)
        {
            return;
        }

        Process.Current.Threads.Add(new ThreadRuntime(new ThreadFunctionOptions()
        {
            EntryPoint = Options.EntryPoint.TargetFunction,
            InitialDestination = Options.EntryPoint.InitialDestination,
            Source = this,
            LocalGroupId = localGroupId
        }));
    }


    public static ExternalEndpointElement GetDataSourceCreateIfNoExists(string name, EntryPointDirective entryPointInfo = null)
    {
        var existing = Game.Current.GamePanel.Controls.WhereAs<ExternalEndpointElement>().Where(e => e.Options.Id == name).SingleOrDefault();

        if (existing == null)
        {
            var newDataSource = Game.Current.GamePanel.Add(new ExternalEndpointElement(new ExternalEndpointElementOptions()
            {
                BorderColor = ConsoleColor.White,
                Id = name,
                Label = name.ToString().ToWhite(),
                EntryPoint = entryPointInfo
            }));
            newDataSource.MoveTo(-100, 100);
            Game.Current.Invoke(async () =>
            {
                newDataSource.MoveTo(Game.Current.GamePanel.Width - newDataSource.Width, 10);
            });

            return newDataSource;
        }
        else
        {
            var ret = Game.Current.GamePanel.Controls.WhereAs<ExternalEndpointElement>().Where(c => c.Options.Id == name).Single();
            ret.Options.EntryPoint = ret.Options.EntryPoint ?? entryPointInfo;
            return ret;
        }
    }

    public static ExternalEndpointElement GetEndpointCreateIfNoExists(EntryPointDirective args)
    {
        var ret = GetDataSourceCreateIfNoExists(args.Source, args);
        ret.Options.DirectiveOptions = args;
        return ret;
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString("O--".ToMagenta(), 0, 0);
}


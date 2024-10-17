using Newtonsoft.Json;

namespace ScrollSucker;

public class LevelEditor : World
{
    private GameCollider cursor;
    private SpawnDirective currentTool;
    private TextViewer watch;
    private bool top;
    private CameraOperator cameraOperator;
    private UndoRedoStack undoRedoStack;

    public UndoRedoStack UndoRedoStack => undoRedoStack;

    public LevelEditor(LevelSpec[] levels, string levelsDir) : base(LoadOrNew(levels, levelsDir)) { }

    private static LevelSpec LoadOrNew(LevelSpec[] levels, string levelsDir)
    {
        LevelSpec ret = null;
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var choice = await ChoiceDialog.Show(new ShowMessageOptions("Create a new level or edit an existing one.")
            {
                AllowEnterToClose = false,
                AllowEscapeToClose = false,
                UserChoices = new List<DialogChoice>()
                {
                    new DialogChoice(){ DisplayText = "New".ToConsoleString(), Id = "New", Shortcut = new KeyboardShortcut(ConsoleKey.N) },
                    new DialogChoice(){ DisplayText = "Load".ToConsoleString(), Id = "Load", Shortcut = new KeyboardShortcut(ConsoleKey.L) },
                }
            });

            if (choice.Id != "Load")
            {
                var levelName = (await TextInputDialog.Show("Name your new level")).ToString();
                ret = new LevelSpec() { Path = Path.Combine(levelsDir, levelName+".json") };
                app.Stop();
            }

            await Dialog.Show(() =>
            {
                var panel = new ConsolePanel() { Width = 80, Height = 4 }; 
                var dd = panel.Add(new Dropdown(levels.Select(l => new DialogChoice() { DisplayText = Path.GetFileNameWithoutExtension(l.Path).ToConsoleString(), Id = l.Path }))).CenterBoth();
                dd.Sync(nameof(dd.Value), () => ret = levels.Where(l => l.Path == dd.Value.Id).Single(), dd);
                return panel;
            },new DialogOptions()
            {
                AllowEnterToClose = true,
                AllowEscapeToClose = false,
            });
            app.Stop();
        });
        app.Run();
        return ret;
    }

    public GameCollider Cursor => cursor;

   protected override async Task Startup()
   {
        await base.Startup();
        undoRedoStack = new UndoRedoStack();
        undoRedoStack.OnUndoRedoAction.Subscribe(() => { Save();RefreshPreviews(); }, this);
        currentTool = new EnemyDirective() { Display = "[Red]enemy", HP = 10 };
        AddCursor();
        AddKeyboardShortcuts();
        AddWatch();
        AddExistingItems();
   }

    private void AddExistingItems()
    {
        foreach(var d in spec.Directives())
        {
            d.Preview(this);
        }
    }

    public void RefreshPreviews()
    {
        // remove all previews
        GamePanel.Children
             .WhereAs<GameCollider>()
             .Where(c => c != Cursor)
             .ToArray()
             .ForEach(c => c.Dispose());

        // re-render the previews in their shifted position
        Spec.Directives().ForEach(d => d.Preview(this));
    }

    private void AddKeyboardShortcuts()
    {
        var permanentActions = new List<(KeyboardShortcut Shortcut, Action Action)>()
        {
            (new KeyboardShortcut(ConsoleKey.E, ConsoleModifiers.Alt), () => SetTool<EnemyWaveDirective>()),
            (new KeyboardShortcut(ConsoleKey.W, ConsoleModifiers.Alt), () => SetTool<WeaponDirective>()),
            (new KeyboardShortcut(ConsoleKey.A, ConsoleModifiers.Alt),() => SetTool <AmmoDirective>()),
            (new KeyboardShortcut(ConsoleKey.O, ConsoleModifiers.Alt),() => SetTool <ObstacleDirective>()),
            (new KeyboardShortcut(ConsoleKey.C, ConsoleModifiers.Alt),() => ShowConsole()),
        };

        var undoableActions = new List<(KeyboardShortcut Shortcut, Action<Dictionary<string, object>> Do, Action<Dictionary<string, object>> Undo)>()
        {
            (new KeyboardShortcut(ConsoleKey.Delete), DeleteDirective, UndoDelete),
            (new KeyboardShortcut(ConsoleKey.Enter), StampTool, UndoStampTool),
        };

        foreach(var action in permanentActions)
        {
            var mine = action;
            ConsoleApp.Current.PushKeyForLifetime(action.Shortcut.Key, action.Shortcut.Modifier, mine.Action, this);
        }

        foreach (var action in undoableActions)
        {
            var mine = action;
            ConsoleApp.Current.PushKeyForLifetime(action.Shortcut.Key, action.Shortcut.Modifier, ()=> undoRedoStack.Do(new LevelEditorAction(action.Do, action.Undo)), this);
        }
    }

    private void ShowConsole()
    {
        var panel = LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = LayoutRoot.FocusStackDepth + 1, Height = 20 }).DockToBottom().FillHorizontally();
        var console = panel.Add(new LevelEditorConsole()).Fill();
        PushKeyForLifetime(ConsoleKey.C, ConsoleModifiers.Alt, panel.Dispose, panel);
    }

    private void UndoStampTool(Dictionary<string, object> state)
    {
        (state[nameof(SpawnDirective)] as SpawnDirective)!.RemoveFrom(Spec);
        RefreshPreviews();
    }

    private void StampTool(Dictionary<string, object> state)
    {
        if (spec.Directives().Where(d => d.X == cursor.Left && d.Top == top).Any()) return;

        var preview = currentTool.Preview(this);
        preview.MoveTo(cursor.Left, cursor.Top);
        currentTool.X = cursor.Left;
        currentTool.Top = top;
        var added = currentTool.Clone();
        state.Add(nameof(SpawnDirective), added);
        added.AddTo(spec);
        UpdateWatch();
        Save();
    }

    private void SetTool<T>() where T : SpawnDirective
    {
        Invoke(async () =>
        { 
            currentTool = currentTool is T ? currentTool : Activator.CreateInstance<T>();
            if (await EditCurrentTool() == false) return;
            currentTool.ValidateUserInput();
            UpdateCursor();
        });
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(spec, Formatting.Indented);
        File.WriteAllText(spec.Path, json);

        Invoke(async () =>
        {
            var savedLabel = LayoutRoot.Add(new Label() { ZIndex = int.MaxValue, Text = "saved".ToGreen() }).DockToTop().CenterHorizontally();
            await Task.Delay(1000);
            savedLabel.Dispose();
        });
    }

    private void UndoDelete(Dictionary<string, object> state)
    {
        (state[nameof(SpawnDirective)] as SpawnDirective)!.AddTo(Spec);
        RefreshPreviews();
    }

    private void DeleteDirective(Dictionary<string,object> state)
    {
        var touching = GamePanel.Children.WhereAs<GameCollider>().Where(c => c != cursor && c.Touches(cursor)).FirstOrDefault();
        if (touching == null) return;

        foreach(var d in spec.Directives().ToArray())
        {
            if (d.X != touching.Left) continue;
            if (d.Top != IsTop(touching)) continue;

            touching.Dispose();
            d.RemoveFrom(spec);
            state.Add(nameof(SpawnDirective), d);
            UpdateWatch();
            Save();
        }
    }

    public async Task<bool> EditCurrentTool()
    {
        var currentToolClone = currentTool.Clone();
        var cancelled = await Dialog.Show(() =>
        {
            var ret = new ConsolePanel() { Width = 70, Height = 15 };
            var form = ret.Add(new Form(FormGenerator.FromObject(currentToolClone)) { Width = 70, Height = 10 }).Fill(new Thickness(2, 2, 1, 1));
            form.Ready.SubscribeOnce(async () =>
            {
                await Task.Yield();
                ConsoleApp.Current.MoveFocus();
            });
            ret.Add(new Label() { CompositionMode = CompositionMode.BlendBackground, Text = ConsoleString.Parse("[Yellow]Press [B=Cyan][Black] enter [D][Yellow] to save") }).DockToLeft(padding: 1).DockToBottom(padding: 2);
            return ret;
        }, new DialogOptions()
        {
            AllowEnterToClose = true,
            AllowEscapeToClose = true,
        });

        if (cancelled) return false;

        currentTool = currentToolClone;
        UpdateWatch();
        return true;
    }

    private void UpdateCursor()
    {
        var x = cursor.X;
        cursor?.Dispose();
        cameraOperator?.Dispose();
        cursor = currentTool.Preview(this);
        cameraOperator = new CameraOperator(camera, cursor, cursor.Velocity, this, new CameraMovement[] { new CustomCameraMovement() });
        Place(cursor, x, top);
        cursor.MoveBy(0, 0, 200);
        UpdateWatch();
    }

    private void AddCursor()
    {
        top = true;
        cursor = currentTool.Preview(this);
        Place(cursor, camera.BigBounds.Left + 20, top);
        cursor.MoveBy(0, 0, 200);

        cameraOperator = new CameraOperator(camera, cursor, cursor.Velocity, this, new CameraMovement[] { new CustomCameraMovement() });
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, CursorUp, this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.W, CursorUp, this);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, CursorDown, this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.S, CursorDown, this);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.LeftArrow, ()=> CursorLeft(), this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.A, () => CursorLeft(), this);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.RightArrow, () => CursorRight(), this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.D, () => CursorRight(), this);
    }

    private void AddWatch()
    {
        watch = LayoutRoot.Add(new TextViewer() {CompositionMode = CompositionMode.BlendBackground, Background = RGB.DarkRed }).DockToBottom().FillHorizontally();
        UpdateWatch();
    }

    private void UpdateWatch() => watch.Text = JsonConvert.SerializeObject(CreateWatchData(), Formatting.Indented).ToJsonConsoleString().ToDifferentBackground(watch.Background);

    private object CreateWatchData() => new
    {
        X = cursor.X,
        Top = top,
        DirectiveCount = spec.Directives().Count(),
        SetWeapon = "ALT + W",
        SetEnemy = "ALT + E",
        Obstacle = "ALT + O",
        SetAmmo = "ALT + A",
        Stamp = "Enter",
        Save ="ALT + S"
    };

    private void CursorLeft()
    {
        cursor.MoveBy(-10, 0);
        cursor.Velocity.OnVelocityEnforced.Fire();
        UpdateWatch();
    }

    private void CursorRight()
    {
        cursor.MoveBy(10, 0);
        cursor.Velocity.OnVelocityEnforced.Fire();
        UpdateWatch();
    }

    private void CursorDown()
    {
        Place(cursor, cursor.Left, false);
        cursor.Velocity.OnVelocityEnforced.Fire();
        top = false;
        UpdateWatch();
    }

    private void CursorUp()
    {
        Place(cursor, cursor.Left, true);
        cursor.Velocity.OnVelocityEnforced.Fire();
        top = true;
        UpdateWatch();
    }
}

public class LevelEditorAction :  IUndoRedoAction
{
    private Action action;
    private Action<Dictionary<string, object>> undo;
    private Dictionary<string, object> state = new Dictionary<string, object>();

    public LevelEditorAction(Action<Dictionary<string, object>> action, Action<Dictionary<string,object>> undo)
    {
        this.action = () => action(state);
        this.undo = undo;
    }

    public void Do() => action();
    public void Undo() => undo(state);
    public void Redo() => throw new NotSupportedException();
}

public class LevelEditorConsole : CompactConsole
{
    protected override CommandLineArgumentsDefinition CreateDefinition() =>
        new CommandLineArgumentsDefinition(typeof(LevelEditorCommands));
}

public class LevelEditorCommands
{
    private LevelEditor Editor => ConsoleApp.Current as LevelEditor;

    [ArgActionMethod]
    public void Shift(int amount)
    {
        Editor.UndoRedoStack.Do(new LevelEditorAction(state =>
        {
            var left = Editor.Cursor.Left;
            state.Add(nameof(left),left);
            // update the spec
            Editor.Spec.Directives()
                .Where(d => d.X >= left)
                .ForEach(d => d.X += amount);
        }, state =>
        {
            var left = (float)state["left"];
            // update the spec
            Editor.Spec.Directives()
                .Where(d => d.X >= left)
                .ForEach(d => d.X -= amount);
        }));
    }

    [ArgActionMethod]
    public void Undo() => Editor.UndoRedoStack.Undo();
}


﻿namespace klooie;

/// <summary>
/// A filter whose implementation is defined inline via an action
/// </summary>
public class ConsoleControlFilter : IConsoleControlFilter
{
    /// <summary>
    /// The control that was just painted
    /// </summary>
    public ConsoleControl Control { get; set; }
    private Action<ConsoleBitmap> impl;

    /// <summary>
    /// Creates a new filter
    /// </summary>
    /// <param name="impl">the filter impl</param>
    public ConsoleControlFilter(Action<ConsoleBitmap> impl)
    {
        this.impl = impl;
    }

    /// <summary>
    /// Calls the filter impl action
    /// </summary>
    /// <param name="bmp">the bitmap to modify</param>
    public void Filter(ConsoleBitmap bmp) => impl(bmp);
}

/// <summary>
/// A class that represents a visual element within a CLI application
/// </summary>
public class ConsoleControl : Rectangular
{
    private Event _focused, _unfocused, _addedToVisualTree, _beforeAddedToVisualTree, _removedFromVisualTree, _beforeRemovedFromVisualTree, _ready, _tagsChanged;
    private Event<ConsoleKeyInfo> _keyInputReceived;
    private Container _parent;
    private RGB _bg, _fg, _focusColor, _focusContrastColor;
    private bool _hasFocus;
    private bool _tabSkip;
    private bool _canFocus;
    private bool _isVisible;
    private object _tag;
    private string _id;
    private CompositionMode _composition;
    private bool hasBeenAddedToVisualTree;
    private HashSet<string> tags;

    internal int FocusStackDepthInternal { get; set; }
    public int FocusStackDepth 
    { 
        get => FocusStackDepthInternal; 
        set 
        {
            if (Application != null) throw new NotSupportedException($"You cannot modify {nameof(FocusStackDepth)} after a control has been added to the application");
            FocusStackDepthInternal = value;
        }
    }

    /// <summary>
    /// returns true unless a dialog is on the screen above the control or if
    /// your application is making use of the focus stack in some other way.
    /// </summary>
    public bool IsFocusStackAtMyLevel => ConsoleApp.Current != null && FocusStackDepth == ConsoleApp.Current.FocusStackDepth;

    /// <summary>
    /// Used to stabilize the z-index sorting for painting
    /// </summary>
    internal int ParentIndex { get; set; }
    public List<IConsoleControlFilter> Filters { get; private set; } = new List<IConsoleControlFilter>();

    /// <summary>
    /// Controls how controls are painted when multiple controls overlap
    /// </summary>
    public CompositionMode CompositionMode { get { return _composition; } set { SetHardIf(ref _composition, value, value != _composition); } }

    /// <summary>
    /// Gets the Id of the control which can only be set at initialization time
    /// </summary>
    public string Id { get => _id; 
        set
        {
            if (_id != null) throw new ArgumentException("Id already set");
            if (value == null) throw new ArgumentNullException("value cannot be null");
            SetHardIf(ref _id, value, _id != value);
        }
    }

    /// <summary>
    /// An event that fires after this control gets focus
    /// </summary>
    public Event Focused { get => _focused ?? (_focused = new Event()); }

    /// <summary>
    /// An event that fires after this control loses focus
    /// </summary>
    public Event Unfocused { get => _unfocused ?? (_unfocused = new Event()); }

    /// <summary>
    /// An event that fires when this control is added to the visual tree of a ConsoleApp. 
    /// </summary>
    public Event AddedToVisualTree { get => _addedToVisualTree ?? (_addedToVisualTree = new Event()); }

    /// <summary>
    /// An event that fires just before this control is added to the visual tree of a ConsoleApp
    /// </summary>
    public Event BeforeAddedToVisualTree { get => _beforeAddedToVisualTree ?? (_beforeAddedToVisualTree = new Event()); }

    /// <summary>
    /// An event that fires when this control is removed from the visual tree of a ConsoleApp.
    /// </summary>
    public Event RemovedFromVisualTree { get => _removedFromVisualTree ?? (_removedFromVisualTree = new Event()); }

    /// <summary>
    /// An event that fires just before this control is removed from the visual tree of a ConsoleApp
    /// </summary>
    public Event BeforeRemovedFromVisualTree { get => _beforeRemovedFromVisualTree ?? (_beforeRemovedFromVisualTree = new Event()); }

    /// <summary>
    /// An event that fires when a key is pressed while this control has focus and the control has decided not to process
    /// the key press internally.
    /// </summary>
    public Event<ConsoleKeyInfo> KeyInputReceived { get => _keyInputReceived ?? (_keyInputReceived = new Event<ConsoleKeyInfo>()); }

    /// <summary>
    /// An event that fires any time its tags changes
    /// </summary>
    public Event TagsChanged { get => _tagsChanged ?? (_tagsChanged = new Event()); }

    /// <summary>
    /// Gets a reference to the application this control is a part of
    /// </summary>
    public ConsoleApp Application { get; internal set; }

    /// <summary>
    /// Gets a reference to this control's parent in the visual tree.  It will be null if this control is not in the visual tree 
    /// and also if this control is the root of the visual tree.
    /// </summary>
    public Container Parent { get { return _parent; } internal set { SetHardIf(ref _parent, value, _parent == null); } }

    /// <summary>
    /// Gets or sets the background color
    /// </summary>
    public RGB Background { get { return _bg; } set { SetHardIf(ref _bg, value, value != _bg); } }

    /// <summary>
    /// Gets or sets the foreground color
    /// </summary>
    public RGB Foreground { get { return _fg; } set { SetHardIf(ref _fg, value, value != _fg); } }

    /// <summary>
    /// An arbitrary reference to an object to associate with this control
    /// </summary>
    public object Tag { get { return _tag; } set { SetHardIf(ref _tag, value, ReferenceEquals(_tag, value) == false); } }

    /// <summary>
    /// An arbitrary set of tags, which can be interpreted as raw strings or key value pairs when the
    /// tag contains a colon ":"
    /// </summary>
    public IEnumerable<string> Tags => tags ?? Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets whether or not this control is visible.  Invisible controls are still fully functional, except that they
    /// don't get painted
    /// </summary>
    public virtual bool IsVisible { get { return _isVisible; } set { SetHardIf(ref _isVisible, value, _isVisible != value); } }

    /// <summary>
    /// Gets or sets whether or not this control can accept focus.  By default this is set to true, but can
    /// be overridden by derived classes to be false by default.
    /// </summary>
    public virtual bool CanFocus { get { return _canFocus; } set { SetHardIf(ref _canFocus, value, _canFocus != value); } }

    /// <summary>
    /// Gets or sets whether or not this control can accept focus via the tab key. By default this is set to false. This
    /// is useful if you to control how focus is managed for larger collections.
    /// </summary>
    public virtual bool TabSkip { get { return _tabSkip; } set { SetHardIf(ref _tabSkip, value, _tabSkip != value); } }

    /// <summary>
    /// Gets whether or not this control currently has focus
    /// </summary>
    public bool HasFocus { get { return _hasFocus; } internal set { SetHardIf(ref _hasFocus, value, _hasFocus != value); } }

    /// <summary>
    /// Gets the color used to indicate focus
    /// </summary>
    public RGB FocusColor { get { return _focusColor; } set { SetHardIf(ref _focusColor, value, _focusColor != value); } }

    /// <summary>
    /// Gets the color used to indicate focus contrast
    /// </summary>
    public RGB FocusContrastColor { get { return _focusContrastColor; } set { SetHardIf(ref _focusContrastColor, value, _focusContrastColor != value); } }

    /// <summary>
    /// The writer used to record the visual state of the control
    /// </summary>
    public ConsoleBitmapVideoWriter Recorder { get; private set; }

    /// <summary>
    /// An optional call back that lets you override the timestamp for each recorded frame. If not
    /// specified then the wallclock will be used
    /// </summary>
    public Func<TimeSpan> RecorderTimestampProvider { get; private set; }

    /// <summary>
    /// Set to true if the Control is in the process of being removed
    /// </summary>
    internal bool IsBeingRemoved { get; set; }

    /// <summary>
    /// An event that fires when this control is both added to an app and that app is running
    /// </summary>
    public Event Ready { get => _ready ?? (_ready = new Event()); }

    /// <summary>
    /// Gets the x coordinate of this control relative to the application root
    /// </summary>
    public int AbsoluteX
    {
        get
        {
            var ret = Parent != null ? Parent.Transform(this).X : this.X;
            ConsoleControl current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                var transformed = current.Parent != null ? current.Parent.Transform(current).X : current.X;
                ret += transformed;
            }
            return ret;
        }
    }

    /// <summary>
    /// Gets the y coordinate of this control relative to the application root
    /// </summary>
    public int AbsoluteY
    {
        get
        {
            var ret = Parent != null ? Parent.Transform(this).Y : this.Y;
            ConsoleControl current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                var transformed = current.Parent != null ? current.Parent.Transform(current).Y : current.Y;
                ret += transformed;
            }
            return ret;
        }
    }

    /// <summary>
    /// Gets all parents of this control
    /// </summary>
    public IEnumerable<Container> Anscestors
    {
        get
        {
            var parent = Parent;
            while(parent != null)
            {
                yield return parent;
                parent = parent.Parent;
            }
        }
    }

    /// <summary>
    /// Gets this controls bitmap, which can be painted onto its parent
    /// </summary>
    public ConsoleBitmap Bitmap { get; internal set; }

    /// <summary>
    /// Creates a ConsoleControl with an Id
    /// </summary>
    /// <param name="id">the id</param>
    public ConsoleControl(string id) : this()
    {
        this._id = id;
    }

    /// <summary>
    /// Creates a ConsoleControl with an initial set of tags
    /// </summary>
    /// <param name="initialTags">the tags</param>
    public ConsoleControl(IEnumerable<string> initialTags) : this()
    {
        AddTags(initialTags);
    }

    /// <summary>
    /// Creates a ConsoleControl with an Id and an initial set of tags
    /// </summary>
    /// <param name="id">the id</param>
    /// <param name="initialTags">the tags</param>
    public ConsoleControl(string id, IEnumerable<string> initialTags) : this()
    {
        this._id = id;
        AddTags(initialTags);
    }

    /// <summary>
    /// Creates a 1x1 ConsoleControl
    /// </summary>
    public ConsoleControl()
    {
        CanFocus = true;
        TabSkip = false;
        this.Bitmap = new ConsoleBitmap(1, 1);
        this.Width = Bitmap.Width;
        this.Height = Bitmap.Height;
        Background = DefaultColors.BackgroundColor;
        this.Foreground = DefaultColors.ForegroundColor;
        this.IsVisible = true;
        FocusColor = DefaultColors.FocusColor;
        FocusContrastColor = DefaultColors.FocusContrastColor;
        CompositionMode = CompositionMode.PaintOver;
        Subscribe(nameof(AnyProperty), () => Application?.RequestPaint(), this);
    }

    /// <summary>
    /// Enables recording the visual content of the control using the specified writer
    /// </summary>
    /// <param name="recorder">the writer to use</param>
    /// <param name="timestampFunc">an optional callback that will be called to determine the timestamp for each frame. If not specified the wall clock will be used.</param>
    /// <param name="lifetime">A lifetime that determines how long recording will last. Defaults to the lifetime of the control.</param>
    public void EnableRecording(ConsoleBitmapVideoWriter recorder, Func<TimeSpan> timestampFunc = null, ILifetimeManager lifetime = null)
    {
        if (Recorder != null)
        {
            throw new InvalidOperationException("This control is already being recorded");
        }
        var h = this.Height;
        var w = this.Width;
        this.Subscribe(nameof(Bounds), () =>
        {
            if (Width != w || Height != h)
            {
                throw new InvalidOperationException("You cannot resize a control that has recording enabled");
            }
        }, this);
        this.Recorder = recorder;
        this.RecorderTimestampProvider = timestampFunc;

        lifetime = lifetime ?? this.Manager;
        lifetime.OnDisposed(() =>
        {
            Recorder.TryFinish();
            Recorder = null;
        });
    }

    /// <summary>
    /// returns true if this control has been tagged with the given value
    /// </summary>
    /// <param name="tag">the valute to test</param>
    /// <returns>true if this control has been tagged with the given value</returns>
    public bool HasSimpleTag(string tag) => GetTagsLazy(false)?.Contains(tag) == true;

    /// <summary>
    /// Adds a tag to this control
    /// </summary>
    /// <param name="tag">the tag to add</param>
    public void AddTag(string tag)
    {
        GetTagsLazy(true).Add(tag);
        _tagsChanged?.Fire();
    }

    /// <summary>
    /// Adds a key / value tag
    /// </summary>
    /// <param name="key">the key</param>
    /// <param name="value">the value</param>
    /// <exception cref="ArgumentException"></exception>
    public void AddValueTag(string key, string value)
    {
        if (key.Contains(":")) throw new ArgumentException("key cannot contain a colon");
        AddTag($"{key}:{value}");
    }

    /// <summary>
    /// Adds a set of tags to this control
    /// </summary>
    /// <param name="tags">the tags to add</param>
    public void AddTags(IEnumerable<string> tags)
    {
        GetTagsLazy(true);
        tags.ForEach(t => this.tags.Add(t));
        _tagsChanged?.Fire();
    }

    /// <summary>
    /// Removes a tag from this control
    /// </summary>
    /// <param name="tag"></param>
    public bool RemoveTag(string tag)
    {
        var ret = GetTagsLazy(HasSimpleTag(tag) || HasValueTag(tag)) == null ? false : tags.Remove(tag);

        if(ret)
        {
            _tagsChanged?.Fire();
        }

        return ret;
    }

    /// <summary>
    /// Tests to see if there is a key value tag with the given key
    /// </summary>
    /// <param name="key">the key to check for</param>
    /// <returns>true if there is a key value tag with the given key</returns>
    public bool HasValueTag(string key) => GetTagsLazy(false)?.Where(t => t.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)).Any() == true;

    /// <summary>
    /// Tries to get the value for a given tag key
    /// </summary>
    /// <param name="key">the key to check for</param>
    /// <param name="value">the value to be populated if found</param>
    /// <returns>true if the tag was found and the out value was populated</returns>
    public bool TryGetTagValue(string key, out string value)
    {
        if (tags == null)
        {
            value = null;
            return false;
        }

        key = key.ToLower();
        if (HasValueTag(key))
        {
            var tag = tags.Where(t => t.ToLower().StartsWith(key + ":")).FirstOrDefault();
            value = ParseTagValue(tag);
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Tries to give this control focus. If the focus is in the visual tree, and is in the current focus layer, 
    /// and has it's CanFocus property to true then focus should be granted.
    /// </summary>
    public void Focus() => Application?.SetFocus(this);

    /// <summary>
    /// Tries to unfocus this control.
    /// </summary>
    public void Unfocus() => Application?.MoveFocus(true);

    public void SyncBackground(params ConsoleControl[] others)
        => others.ForEach(other => this.Sync(nameof(Background), () => other.Background = this.Background, other));

    public void SyncForeground(params ConsoleControl[] others)
        => others.ForEach(other => this.Sync(nameof(Foreground), () => other.Foreground = this.Foreground, other));

    /// <summary>
    /// You should override this method if you are building a custom control, from scratch, and need to control
    /// every detail of the painting process.  If possible, prefer to create your custom control by deriving from
    /// ConsolePanel, which will let you assemble a new control from others.
    /// </summary>
    /// <param name="context">The scoped bitmap that you can paint on</param>
    protected virtual void OnPaint(ConsoleBitmap context) { }

    protected override void OnPropertyChanged(string propertyName)
    {
        if (propertyName == nameof(Bounds))
        {
            ResizeBitmapOnBoundsChanged();
        }
        else if (propertyName == nameof(CanFocus) && IsExpired == false && CanFocus == false && HasFocus)
        {
            ConsoleApp.Current?.MoveFocus();
        }

        if (this.Application != null && this.Application.IsRunning && this.Application.IsDrainingOrDrained == false)
        {
            ConsoleApp.AssertAppThread(this.Application);
            this.Application.RequestPaint();
        }
    }

    internal void FireFocused(bool focused)
    {
        if (focused) _focused?.Fire();
        else _unfocused?.Fire();
    }

    internal void AddedToVisualTreeInternal()
    {
        if (hasBeenAddedToVisualTree)
        {
            throw new ObjectDisposedException("This control has already been added to a visual tree and cannot be reused.");
        }

        hasBeenAddedToVisualTree = true;
        if (Application.IsRunning)
        {
            _ready?.Fire();
        }
        else if (_ready != null)
        {
            Application.InvokeNextCycle(Ready.Fire);
        }
        _addedToVisualTree?.Fire();
    }

    internal void BeforeAddedToVisualTreeInternal()
    {
        _beforeAddedToVisualTree?.Fire();
    }

    internal void BeforeRemovedFromVisualTreeInternal()
    {
        _beforeRemovedFromVisualTree?.Fire();
    }

    internal void RemovedFromVisualTreeInternal()
    {
        _removedFromVisualTree?.Fire();
    }

    internal void Paint()
    {
        if (IsVisible == false || Height <= 0 || Width <= 0)
        {
            return;
        }

        Bitmap.Fill(new ConsoleCharacter(' ', null, Background));

        OnPaint(Bitmap);
        if (Recorder != null && Recorder.IsFinished == false)
        {
            Recorder.Window = new RectF(0, 0, Width, Height);
            Recorder.WriteFrame(Bitmap, false, RecorderTimestampProvider != null ? new TimeSpan?(RecorderTimestampProvider()) : new TimeSpan?());
        }
    }

    internal void HandleKeyInput(ConsoleKeyInfo info)
    {
        _keyInputReceived?.Fire(info);
    }

    internal Loc CalculateAbsolutePosition()
    {
        var x = X;
        var y = Y;

        var tempParent = Parent;
        while (tempParent != null)
        {
            x += tempParent.X;
            y += tempParent.Y;
            tempParent = tempParent.Parent;
        }

        return new Loc(x, y);
    }

    internal Loc CalculateRelativePosition(ConsoleControl parent)
    {
        var x = X;
        var y = Y;

        var tempParent = Parent;
        while (tempParent != null && tempParent != parent)
        {
            if (tempParent is ScrollablePanel)
            {
                throw new InvalidOperationException("Controls within scrollable panels cannot have their relative positions calculated");
            }

            x += tempParent.X;
            y += tempParent.Y;
            tempParent = tempParent.Parent;
        }

        return new Loc(x, y);
    }

    private void ResizeBitmapOnBoundsChanged()
    {
        if (ShouldContinue == false || Width <= 0 || Height <= 0) return;
        Bitmap.Resize(Width, Height);
    }

    private string ParseTagValue(string tag)
    {
        var splitIndex = tag.IndexOf(':');
        if (splitIndex <= 0) throw new ArgumentException("No tag value present for tag: " + tag);

        var val = tag.Substring(splitIndex + 1, tag.Length - (splitIndex + 1));
        return val;
    }

    private HashSet<string> GetTagsLazy(bool willWrite)
    {
        if (willWrite && tags == null)
        {
            tags = new HashSet<string>();
        }
        return tags;
    }
}

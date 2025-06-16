namespace klooie;

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
public partial class ConsoleControl : Rectangular
{
    private Event _focused, _unfocused,   _ready, _tagsChanged;
    private Event<ConsoleKeyInfo> _keyInputReceived;
    public bool HasBeenAddedToVisualTree { get; private set; }
    private HashSet<string> tags;

    internal int? FocusStackDepthInternal { get; set; }
    public int FocusStackDepth 
    { 
        get => FocusStackDepthInternal.HasValue ? FocusStackDepthInternal.Value : 1; 
        set 
        {
            // todo - find another way to do this
            //if (FocusStackDepthInternal.HasValue) throw new NotSupportedException($"You cannot modify {nameof(FocusStackDepth)} after a control has been added to the application");
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

    internal RecyclableList<IConsoleControlFilter> _filters;
    public List<IConsoleControlFilter> Filters
    {
        get
        {
            if (_filters != null) return _filters.Items;

            _filters = RecyclableListPool<IConsoleControlFilter>.Instance.Rent();
            return _filters.Items;
        }
    }
    public bool HasFilters => _filters != null && _filters.Count > 0;

    /// <summary>
    /// Controls how controls are painted when multiple controls overlap
    /// </summary>
    public CompositionMode CompositionMode { get; set; }

    /// <summary>
    /// Gets or sets the id of the control
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// An event that fires after this control gets focus
    /// </summary>
    public Event Focused { get => _focused ?? (_focused = Event.Create()); }

    /// <summary>
    /// An event that fires after this control loses focus
    /// </summary>
    public Event Unfocused { get => _unfocused ?? (_unfocused = Event.Create()); }

    /// <summary>
    /// An event that fires when this control is both added to an app and that app is running
    /// </summary>
    public Event Ready { get => _ready ?? (_ready = Event.Create()); }




    /// <summary>
    /// An event that fires when a key is pressed while this control has focus and the control has decided not to process
    /// the key press internally.
    /// </summary>
    public Event<ConsoleKeyInfo> KeyInputReceived { get => _keyInputReceived ?? (_keyInputReceived = Event<ConsoleKeyInfo>.Create()); }

    /// <summary>
    /// An event that fires any time its tags changes
    /// </summary>
    public Event TagsChanged { get => _tagsChanged ?? (_tagsChanged = Event.Create(out int _)); }

    /// <summary>
    /// Gets a reference to this control's parent in the visual tree.  It will be null if this control is not in the visual tree 
    /// and also if this control is the root of the visual tree.
    /// </summary>
    public Container Parent { get; internal set; }

    /// <summary>
    /// Gets or sets the background color
    /// </summary>
    public partial RGB Background { get; set; }

    /// <summary>
    /// Gets or sets the foreground color
    /// </summary>
    public partial RGB Foreground { get; set; }

    /// <summary>
    /// An arbitrary reference to an object to associate with this control
    /// </summary>
    public partial object Tag { get; set; }

    /// <summary>
    /// An arbitrary set of tags, which can be interpreted as raw strings or key value pairs when the
    /// tag contains a colon ":"
    /// </summary>
    public IEnumerable<string> Tags => tags ?? Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets whether or not this control is visible.  Invisible controls are still fully functional, except that they
    /// don't get painted
    /// </summary>
    public partial bool IsVisible { get; set; }

    /// <summary>
    /// Gets whether or not all parents of this control are visible
    /// </summary>
    public bool AreAllParentsVisible => Anscestors.All(a => a.IsVisible);

    /// <summary>
    /// Gets whether or not this control is visible and all parents are visible
    /// </summary>
    public bool IsVisibleAndAllParentsVisible => IsVisible && AreAllParentsVisible;

    /// <summary>
    /// Gets or sets whether or not this control can accept focus.  By default this is set to true, but can
    /// be overridden by derived classes to be false by default.
    /// </summary>
    public partial bool CanFocus { get; set; }

    /// <summary>
    /// Gets or sets whether or not this control can accept focus via the tab key. By default this is set to false. This
    /// is useful if you to control how focus is managed for larger collections.
    /// </summary>
    public bool TabSkip { get; set; }

    /// <summary>
    /// Gets whether or not this control currently has focus
    /// </summary>
    public partial bool HasFocus { get; set; }

    /// <summary>
    /// Gets the color used to indicate focus
    /// </summary>
    public partial RGB FocusColor { get; set; }

    /// <summary>
    /// Gets the color used to indicate focus contrast
    /// </summary>
    public partial RGB FocusContrastColor { get; set; }

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

    public Container Root
    {
        get
        {
            var parent = Parent;
            while (parent != null)
            {
                if (parent.Parent == null) return parent;
                parent = parent.Parent;
            }
            throw new InvalidOperationException("This control is not in the visual tree");
        }
    }

    private ConsoleBitmap _bitmap;
    /// <summary>
    /// Gets this controls bitmap, which can be painted onto its parent
    /// </summary>
    public ConsoleBitmap Bitmap
    { 
        get
        {
            _bitmap = _bitmap ?? ConsoleBitmap.Create(Width, Height);
            return _bitmap;
        }
        internal set
        {
            _bitmap = value;
        }
    }
    protected override void OnInit()
    {
        if(Parent != null) throw new InvalidOperationException($"Parent is not null, indicating this object has not been reset properly. HasBeenAddedToVisualTree == {HasBeenAddedToVisualTree}");
        base.OnInit();
        HasBeenAddedToVisualTree = false;
        CanFocus = true;
        HasFocus = false;
        TabSkip = false;
        this.Width = 1;
        this.Height = 1;
        Background = DefaultColors.BackgroundColor;
        this.Foreground = DefaultColors.ForegroundColor;
        this.IsVisible = true;
        FocusColor = DefaultColors.FocusColor;
        FocusContrastColor = DefaultColors.FocusContrastColor;
        CompositionMode = CompositionMode.PaintOver;
        
        BoundsChanged.Subscribe(this, ResizeBitmapOnBoundsChanged, this);
        CanFocusChanged.Subscribe(this, HandleCanFocusChanged, this);
        OnDisposed(this, ReturnEvents);
    }
     

    protected virtual void OnAnyPropertyChanged() { }

    private static void HandleCanFocusChanged(object me)
    {
        var _this = me as ConsoleControl;
        if (_this.HasFocus && _this.CanFocus == false) ConsoleApp.Current?.MoveFocus();
    }

  

    private static void ReturnEvents(object me)
    {
        var _this = me as ConsoleControl;
        if (_this._focused != null)
        {
            _this._focused.Dispose();
            _this._focused = null;
        }
        if (_this._unfocused != null)
        {
            _this._unfocused.Dispose();
            _this._unfocused = null;
        }
        if (_this._ready != null)
        {
            _this._ready.Dispose();
            _this._ready = null;
        }
        if (_this._tagsChanged != null)
        {
            _this._tagsChanged.Dispose();
            _this._tagsChanged = null;
        }

        if (_this._keyInputReceived != null)
        {
            _this._keyInputReceived.Dispose();
            _this._keyInputReceived = null;
        }

        _this.HasFocus = false;

        _this._filters?.Dispose();
        _this._filters = null;

        _this.tags?.Clear();
        _this.Tag = null;

        _this.Bitmap?.Dispose();
        _this.Bitmap = null;

        // This is here because controls can either be removed using Dispose() or by calling Remove from a parent's Controls collection.
        // In the case where Dispose() is called somebody needs to remove this control from its parent.
        // We could do this from within ConsolePanel, but it would require a lambda with a capture, which causes an allocation.
        // Doing it here looks a bit hacky, but that allocation is on a critical path.
        (_this.Parent as ConsolePanel)?.Controls.Remove(_this);
    }

    /// <summary>
    /// Creates a ConsoleControl with an Id
    /// </summary>
    /// <param name="id">the id</param>
    public ConsoleControl(string id) : this()
    {
        this.Id = id;
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
    public ConsoleControl(string id, IEnumerable<string> initialTags) : this(id)
    {
        AddTags(initialTags);
    }

    /// <summary>
    /// Creates a 1x1 ConsoleControl
    /// </summary>
    public ConsoleControl()
    {
        
    }

    /// <summary>
    /// Enables recording the visual content of the control using the specified writer
    /// </summary>
    /// <param name="recorder">the writer to use</param>
    /// <param name="timestampFunc">an optional callback that will be called to determine the timestamp for each frame. If not specified the wall clock will be used.</param>
    /// <param name="lifetime">A lifetime that determines how long recording will last. Defaults to the lifetime of the control.</param>
    public void EnableRecording(ConsoleBitmapVideoWriter recorder, Func<TimeSpan> timestampFunc = null, ILifetime lifetime = null)
    {
        if (Recorder != null)
        {
            throw new InvalidOperationException("This control is already being recorded");
        }
        var h = this.Height;
        var w = this.Width;
        BoundsChanged.Subscribe(() =>
        {
            if (Width != w || Height != h)
            {
                throw new InvalidOperationException("You cannot resize a control that has recording enabled");
            }
        }, this);
        this.Recorder = recorder;
        this.RecorderTimestampProvider = timestampFunc;

        lifetime = lifetime ?? this;
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
    public void Focus() => ConsoleApp.Current?.SetFocus(this);

    /// <summary>
    /// Tries to unfocus this control.
    /// </summary>
    public void Unfocus() => ConsoleApp.Current?.MoveFocus(true);

    public void SyncBackground(params ConsoleControl[] others)
        => others.ForEach(other => BackgroundChanged.Sync(() => other.Background = this.Background, other));

    public void SyncForeground(params ConsoleControl[] others)
        => others.ForEach(other => ForegroundChanged.Sync(() => other.Foreground = this.Foreground, other));

    /// <summary>
    /// You should override this method if you are building a custom control, from scratch, and need to control
    /// every detail of the painting process.  If possible, prefer to create your custom control by deriving from
    /// ConsolePanel, which will let you assemble a new control from others.
    /// </summary>
    /// <param name="context">The scoped bitmap that you can paint on</param>
    protected virtual void OnPaint(ConsoleBitmap context) { }



    internal void FireFocused(bool focused)
    {
        if (focused) _focused?.Fire();
        else _unfocused?.Fire();
    }

    internal void AddedToVisualTreeInternal()
    {
        HasBeenAddedToVisualTree = true;
        _ready?.Fire();
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

    private static void ResizeBitmapOnBoundsChanged(object me)
    {
        var _this = me as ConsoleControl;
        if (_this.Width <= 0 || _this.Height <= 0) return;
        if (_this.Bitmap == null)
        {
            _this.Bitmap = ConsoleBitmap.Create(_this.Width, _this.Height);
        }
        else
        {
            _this.Bitmap.Resize(_this.Width, _this.Height);
        }
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

 
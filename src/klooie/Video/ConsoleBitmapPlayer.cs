﻿namespace klooie;
/// <summary>
/// All states that the player can be in
/// </summary>
public enum PlayerState
{
    /// <summary>
    /// The initial state when there is no video loaded
    /// </summary>
    NotLoaded,
    /// <summary>
    /// A video failed to load
    /// </summary>
    Failed,
    /// <summary>
    /// A video is playing
    /// </summary>
    Playing,
    /// <summary>
    /// A video is buffering
    /// </summary>
    Buffering,
    /// <summary>
    /// A video is paused
    /// </summary>
    Paused,
    /// <summary>
    ///  A video is stopped
    /// </summary>
    Stopped,
}

/// <summary>
/// A control that can play console app recordings from a stream
/// </summary>
public sealed partial class ConsoleBitmapPlayer : ConsolePanel
{
    /// <summary>
    /// Gets the current state of the player
    /// </summary>
    public partial PlayerState State { get; set; }

    /// <summary>
    /// An event that fires when the player stops
    /// </summary>
    private Event stopped;
    public Event Stopped => stopped ??= Event.Create();

    /// <summary>
    /// An artificial delay that is added after each frame is loaded from the stream.  This can simulate
    /// a slow loading connection and is good for testing.  This should always be set to null when PowerArgs ships.
    /// </summary>
    internal TimeSpan? AfterFrameLoadDelay { get; set; } = null;

    /// <summary>
    /// Gets or sets the rewind and fast forward increment, defaults to 10 seconds
    /// </summary>
    public partial TimeSpan RewindAndFastForwardIncrement { get; set; }

    /// <summary>
    /// The bar that's rendered below the player.  It shows the current play cursor and loading progress.
    /// </summary>
    private PlayerProgressBar playerProgressBar;

    /// <summary>
    /// The border control that hosts the current frame inside of it
    /// </summary>
    private BorderPanel pictureFrame;

    /// <summary>
    /// The control that renders the current frame in the video
    /// </summary>
    private BitmapControl pictureInTheFrame;

    /// <summary>
    /// The buttons that appear under the player progress bar
    /// </summary>
    private Button seekToBeginningButton, seekBack10SButton, seekBackFrameButton, seekForward10SButton,seekForwardFrameButton, seekToEndButton, playButton;


    /// <summary>
    /// The duration of the currently loaded video.  This is set once the first frame of the video is loaded
    /// </summary>
    private TimeSpan? duration;

    public bool HasData => duration.HasValue;

    /// <summary>
    /// The in memory video data structure.  This is set once the first frame of the video is loaded
    /// </summary>
    private InMemoryConsoleBitmapVideo inMemoryVideo;

    /// <summary>
    /// The cursor position at the time playback started.  If the current state is not Playing then this value
    /// is meaningless.
    /// </summary>
    private TimeSpan playStartPosition;

    /// <summary>
    /// The wall clock time when playback started.  If the current state is not playing then this value is meaningless.
    /// </summary>
    private DateTime playStartTime;

    /// <summary>
    /// The most recent frame index received from the reader's TrySeek method
    /// </summary>
    private int lastFrameIndex;

    /// <summary>
    /// The error message to show if loading failed
    /// </summary>
    private string failedMessage;

    /// <summary>
    /// Gets or sets the current frame image
    /// </summary>
    private ConsoleBitmap CurrentFrame
    {
        get
        {
            return pictureInTheFrame.Bitmap;
        }
        set
        {
            pictureInTheFrame.Bitmap = value;
        }
    }

    private Event<TimeSpan> onFramePlayed;
    public Event<TimeSpan> OnFramePlayed => onFramePlayed ??= Event<TimeSpan>.Create();

    /// <summary>
    /// Creates a console bitmap player control with no video loaded
    /// </summary>
    public ConsoleBitmapPlayer(bool showButtonBar = true)
    {
        this.CanFocus = false;
        RewindAndFastForwardIncrement = TimeSpan.FromSeconds(10);
        pictureInTheFrame = new BitmapControl() { AutoSize = true, CanFocus = false };
        pictureFrame = Add(new BorderPanel(pictureInTheFrame)).Fill(padding: new Thickness(0, 0, 0, 2));
        pictureInTheFrame.CenterBoth();
        pictureFrame.BorderColor = RGB.DarkGray;

        playerProgressBar = Add(new PlayerProgressBar() { ShowPlayCursor = false }).FillHorizontally(padding: new Thickness(0, 0, 0, 0)).DockToBottom(padding: 1);

        var buttonBar = Add(new StackPanel() { CanFocus = false, Height = 1, Orientation = Orientation.Horizontal }).FillHorizontally().DockToBottom();

        seekToBeginningButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.Home)) { Text = "<<".ToConsoleString(), CanFocus = false });
        seekBackFrameButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.UpArrow)) { Text = "prev frame".ToConsoleString(), CanFocus = false });
        seekBack10SButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.LeftArrow)) { CanFocus = false });
        playButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.P)) { Text = "Play".ToConsoleString(), CanFocus = false });
        seekForward10SButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.RightArrow)) { CanFocus = false });
        seekForwardFrameButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.DownArrow)) { Text = "next frame".ToConsoleString(), CanFocus = false });
        seekToEndButton = buttonBar.Add(new Button(new KeyboardShortcut(ConsoleKey.End)) { Text = ">>".ToConsoleString(), CanFocus = false });

        if (showButtonBar)
        {
            seekToBeginningButton.Pressed.Subscribe(SeekToBeginningButtonPressed, this);
            seekBack10SButton.Pressed.Subscribe(Rewind, this);
            seekBackFrameButton.Pressed.Subscribe(RewindFrame, this);
            playButton.Pressed.Subscribe(PlayPressed, this);
            seekForward10SButton.Pressed.Subscribe(FastForward, this);
            seekForwardFrameButton.Pressed.Subscribe(FastForwardFrame, this);
            seekToEndButton.Pressed.Subscribe(SeekToEndButtonPressed, this);
        }
        else
        {
            buttonBar.IsVisible = false;
        }
        this.StateChanged.Subscribe(OnStateChanged, this);

        RewindAndFastForwardIncrementChanged.Sync(() =>
        {
            seekBack10SButton.Text = $"< {RewindAndFastForwardIncrement.TotalSeconds}s".ToConsoleString();
            seekForward10SButton.Text = $"{RewindAndFastForwardIncrement.TotalSeconds}s >".ToConsoleString();
        }, this);

        State = PlayerState.NotLoaded;
        StateChanged.Subscribe(() =>
        {
            if(State == PlayerState.Stopped)
            {
                Stopped.Fire();
            }
        }, this);
    }

    /// <summary>
    /// Plays the video
    /// </summary>
    public void Play() => PlayPressed();

    /// <summary>
    /// Seeks to the beginning of the video.  If the video is playing then it will continue playing
    /// from the beginning of the video
    /// </summary>
    private void SeekToBeginningButtonPressed()
    {
        if (duration.HasValue == false)
        {
            throw new InvalidOperationException("Seeking is not permitted before a video is loaded");
        }

        playStartPosition = TimeSpan.Zero;
        playStartTime = DateTime.UtcNow;
        playerProgressBar.PlayCursorPosition = 0;
        if (inMemoryVideo != null && inMemoryVideo.Frames.Count > 0)
        {
            CurrentFrame = inMemoryVideo.Frames[0].Bitmap;
        }
    }


    /// <summary>
    /// Seeks to the end of the video
    /// </summary>
    private void SeekToEndButtonPressed()
    {
        if (duration.HasValue == false)
        {
            throw new InvalidOperationException("Seeking is not permitted before a video is loaded");
        }

        playStartPosition = duration.Value;
        playStartTime = DateTime.UtcNow;
        playerProgressBar.PlayCursorPosition = Math.Min(1, playerProgressBar.LoadProgressPosition);
        if (inMemoryVideo != null && inMemoryVideo.Frames.Count > 0)
        {
            CurrentFrame = inMemoryVideo.Frames[inMemoryVideo.Frames.Count - 1].Bitmap;
        }
    }

    /// <summary>
    /// Rewinds the video by the amount defined by the RewindAndFastForwardIncrement.  If the
    /// video was playing then it will continue to play
    /// </summary>
    private void Rewind()
    {
        if (duration.HasValue == false)
        {
            throw new InvalidOperationException("Rewind is not permitted before a video is loaded");
        }

        var numSecondsBack = RewindAndFastForwardIncrement.TotalSeconds;
        var tenSecondsPercentage = numSecondsBack / duration.Value.TotalSeconds;
        if (tenSecondsPercentage > 1) tenSecondsPercentage = 1;

        var newCursorPosition = playerProgressBar.PlayCursorPosition - tenSecondsPercentage;
        if (newCursorPosition < 0) newCursorPosition = 0;
        playerProgressBar.PlayCursorPosition = newCursorPosition;

        playStartPosition = TimeSpan.FromSeconds(playerProgressBar.PlayCursorPosition * duration.Value.TotalSeconds);
        playStartTime = DateTime.UtcNow;
        CurrentFrame = inMemoryVideo.Frames.OrderBy(f => Math.Abs((f.FrameTime - playStartPosition).TotalSeconds)).First().Bitmap;
    }

    private void RewindFrame()
    {
        if (State == PlayerState.Playing) return;
        if (duration.HasValue == false) throw new InvalidOperationException("Rewind is not permitted before a video is loaded");
        if (lastFrameIndex == 0) return;

        var previousFrame = inMemoryVideo.Frames[lastFrameIndex - 1];
        playerProgressBar.PlayCursorPosition = previousFrame.FrameTime.TotalSeconds / inMemoryVideo.Duration.TotalSeconds;

        playStartPosition = previousFrame.FrameTime;
        playStartTime = DateTime.UtcNow;
        CurrentFrame = previousFrame.Bitmap;
        lastFrameIndex--;
    }

    /// <summary>
    /// Fast forwards the video by the amount defined by the RewindAndFastForwardIncrement.  If the
    /// video was playing then it will continue to play, unless it hits the end of the video.
    /// </summary>
    private void FastForward()
    {
        if (duration.HasValue == false)
        {
            throw new InvalidOperationException("Fast forward is not permitted before a video is loaded");
        }

        var numSecondsForward = RewindAndFastForwardIncrement.TotalSeconds;
        var tenSecondsPercentage = numSecondsForward / duration.Value.TotalSeconds;
        if (tenSecondsPercentage > 1) tenSecondsPercentage = 1;

        var newCursorPosition = playerProgressBar.PlayCursorPosition + tenSecondsPercentage;
        if (newCursorPosition > 1) newCursorPosition = 1;
        playerProgressBar.PlayCursorPosition = Math.Min(playerProgressBar.LoadProgressPosition, newCursorPosition);

        playStartPosition = TimeSpan.FromSeconds(playerProgressBar.PlayCursorPosition * duration.Value.TotalSeconds);
        playStartTime = DateTime.UtcNow;
        CurrentFrame = inMemoryVideo.Frames.OrderBy(f => Math.Abs((f.FrameTime - playStartPosition).TotalSeconds)).First().Bitmap;
    }

    private void FastForwardFrame()
    {
        if (State == PlayerState.Playing) return;
        if (duration.HasValue == false) throw new InvalidOperationException("Rewind is not permitted before a video is loaded");
        if (lastFrameIndex >= inMemoryVideo.Frames.Count - 1) return;

        var nextFrame = inMemoryVideo.Frames[lastFrameIndex + 1];
        playerProgressBar.PlayCursorPosition = nextFrame.FrameTime.TotalSeconds / inMemoryVideo.Duration.TotalSeconds;

        playStartPosition = nextFrame.FrameTime;
        playStartTime = DateTime.UtcNow;
        CurrentFrame = nextFrame.Bitmap;
        lastFrameIndex++;
    }

    /// <summary>
    /// The handler for the play button that handles play / pause toggling and resetting to the beginning
    /// if the player is currently stopped at the end of the video.
    /// </summary>
    private void PlayPressed()
    {
        if (duration.HasValue == false)
        {
            throw new InvalidOperationException("Playback is not permitted before a video is loaded");
        }

        if (State == PlayerState.Playing)
        {
            State = PlayerState.Paused;
        }
        else if (State == PlayerState.Paused || State == PlayerState.Buffering)
        {
            State = PlayerState.Playing;
        }
        else if (State == PlayerState.Stopped)
        {
            if (playerProgressBar.PlayCursorPosition == 1)
            {
                playerProgressBar.PlayCursorPosition = 0;
            }

            State = PlayerState.Playing;
        }
    }

    /// <summary>
    /// The state change handler that defines what happens whenever the player changes state
    /// </summary>
    private void OnStateChanged()
    {
        if (State == PlayerState.Playing)
        {
            if (duration.HasValue == false)
            {
                throw new InvalidOperationException("Playback is not permitted before a video is loaded");
            }
            ConsoleApp.Current.Invoke(PlayLoop);
        }
        else if (State == PlayerState.Stopped)
        {
            pictureFrame.BorderColor = RGB.Yellow;
            playButton.Text = "Play".ToConsoleString();
        }
        else if (State == PlayerState.Paused)
        {
            playButton.Text = "Play".ToConsoleString();
        }
        else if (State == PlayerState.NotLoaded)
        {
            playButton.Text = "Play".ToConsoleString();
            playButton.CanFocus = false;
        }
        else if (State == PlayerState.Buffering)
        {
            playButton.Text = "Play".ToConsoleString();
        }
        else if (State == PlayerState.Failed)
        {
            pictureFrame.BorderColor = RGB.Red;
            MessageDialog.Show(failedMessage.ToRed());
        }
        else
        {
            throw new Exception("Unknown state: " + State);
        }
    }

    private async Task PlayLoop()
    {
        playStartPosition = TimeSpan.FromSeconds(playerProgressBar.PlayCursorPosition * duration.Value.TotalSeconds);
        playStartTime = DateTime.UtcNow;
        lastFrameIndex = 0;
        await Task.Delay(1);
        if (State != PlayerState.Playing) return;

        while (State == PlayerState.Playing)
        {
            // start a play loop for as long as the state remains unchanged
            var now = DateTime.UtcNow;
            var delta = now - playStartTime;
            var newPlayerPosition = playStartPosition + delta;
            var videoLocationPercentage = Math.Round(100.0 * newPlayerPosition.TotalSeconds / duration.Value.TotalSeconds, 1, MidpointRounding.AwayFromZero);
            videoLocationPercentage = Math.Min(videoLocationPercentage, 100);
            playerProgressBar.PlayCursorPosition = videoLocationPercentage / 100.0;
            playButton.Text = $"Pause".ToConsoleString();

            InMemoryConsoleBitmapFrame seekedFrame;

            var prevFrameIndex = lastFrameIndex;
            if ((lastFrameIndex = inMemoryVideo.Seek(newPlayerPosition, out seekedFrame, lastFrameIndex >= 0 ? lastFrameIndex : 0)) < 0)
            {
                State = PlayerState.Buffering;
            }
            else if (prevFrameIndex != lastFrameIndex)
            {
                CurrentFrame = seekedFrame.Bitmap;
                OnFramePlayed.Fire(seekedFrame.FrameTime);
            }

            if (newPlayerPosition > duration)
            {
                State = PlayerState.Stopped;
            }
            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Loads a video from a given stream
    /// </summary>
    /// <param name="videoStream">the video stream</param>
    public Task Load(Stream videoStream)
    {
        var tcs = new TaskCompletionSource();
        if (ConsoleApp.Current == null)
        {
            throw new InvalidOperationException("Can't load until the control has been added to an application");
        }
        var app = ConsoleApp.Current;
        Task.Factory.StartNew(() =>
        {
            try
            {
                var reader = new ConsoleBitmapStreamReader(videoStream);
                reader.ReadToEnd((videoWithProgressInfo) =>
                {
                    inMemoryVideo = inMemoryVideo ?? videoWithProgressInfo;
                    this.duration = videoWithProgressInfo.Duration;
                    app.Invoke(() =>
                    {
                        if (this.CurrentFrame == null)
                        {
                            this.CurrentFrame = videoWithProgressInfo.Frames[0].Bitmap;
                            playerProgressBar.ShowPlayCursor = true;
                            playButton.CanFocus = true;
                            seekToBeginningButton.CanFocus = true;
                            seekBackFrameButton.CanFocus = true;
                            seekBack10SButton.CanFocus = true;
                            seekForward10SButton.CanFocus = true;
                            seekForwardFrameButton.CanFocus = true;
                            seekToEndButton.CanFocus = true;
                            State = PlayerState.Stopped;
                            if (app.FocusedControl == null)
                            {
                                app.SetFocus(playButton);
                            }
                        }

                        playerProgressBar.LoadProgressPosition = inMemoryVideo.LoadProgress;
                    });
                    if (AfterFrameLoadDelay.HasValue)
                    {
                        Thread.Sleep(AfterFrameLoadDelay.Value);
                    }
                });
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
#if DEBUG
                    failedMessage = ex.ToString();
#else
                    failedMessage = ex.Message;
#endif
                ConsoleApp.Current.InvokeNextCycle(() =>
                {
                    State = PlayerState.Failed;
                });
            }
        });
        return tcs.Task;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        stopped?.TryDispose();
        stopped = null;
        onFramePlayed?.TryDispose();
        onFramePlayed = null;
    }
}

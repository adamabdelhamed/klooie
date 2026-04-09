## Klooie
Klooie is a .NET framework for building interactive applications that run on a command line. It works by writing ANSI to standard output, which renders a visual image.

- `klooie.Windows/AudioPlaybackEngine.cs` now wraps the final mix in a conservative output-protection stage with fixed headroom and soft clipping before WASAPI. Prefer adjusting those constants before increasing device latency again.
- `klooie.Windows/AudioPlaybackEngine.cs` keeps the mixer graph alive across endpoint loss and rebuilds only the WASAPI output device. Startup failures, default-device changes, and playback-stop/device-state notifications all funnel into the same delayed recovery path.
- `klooie.Windows/RecyclableSampleProvider.cs` applies a tiny edge fade-in and release tail to sample playback so short sounds do not start or stop on hard waveform discontinuities. Keep those envelopes very short so game feel is unchanged.

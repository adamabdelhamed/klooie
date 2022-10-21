As of the time of this writing there is no good cross-platform audio library for .NET. 

For now, klooie does support sound, but only on Windows. You will need to use the klooie.Windows package which is separate from the main klooie package.

In klooie.Windows you will find a class called AudioPlaybackEngine. This class uses [NAudio](https://github.com/naudio/NAudio) to play sound. The ISoundProvider interface makes it easy to use this engine from a klooie application.

//#SoundSample
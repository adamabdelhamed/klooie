using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Audio)]
public class ScheduledSignalSourceMixerTests
{
    [TestInitialize]
    public void Init() => TestContextHelper.GlobalSetup();

    private class DummyVoice : SynthSignalSource
    {
        public int RenderCount;
        public override int Render(float[] buffer, int offset, int count)
        {
            RenderCount++;
            for(int i=offset;i<offset+count;i++)
                buffer[i]=0f;
            isDone = true;
            return count;
        }
    }

    [TestMethod]
    public void CancelBeforeScheduled_SkipsNote()
    {
        SoundProvider.Current = new NoOpSoundProvider();
        var mixer = new ScheduledSignalSourceMixer();
        var voice = new DummyVoice();
        var note = NoteExpression.Create(60,0,1,60);
        var sched = ScheduledNoteEvent.Create(0,note,voice);
        sched.Cancel();
        mixer.ScheduleNote(sched);
        var buffer = new float[SoundProvider.ChannelCount * 10];
        mixer.Read(buffer,0,buffer.Length);
        Assert.AreEqual(0, voice.RenderCount);
    }

    [TestMethod]
    public void CancelActiveVoice_StopsRendering()
    {
        SoundProvider.Current = new NoOpSoundProvider();
        var mixer = new ScheduledSignalSourceMixer();
        var voice = new DummyVoice();
        var note = NoteExpression.Create(60,0,1,60);
        var sched = ScheduledNoteEvent.Create(0,note,voice);
        mixer.ScheduleNote(sched);
        var buffer = new float[SoundProvider.ChannelCount * 10];
        mixer.Read(buffer,0,buffer.Length);
        Assert.AreEqual(1, voice.RenderCount);
        sched.Cancel();
        mixer.Read(buffer,0,buffer.Length);
        Assert.AreEqual(1, voice.RenderCount);
    }
}

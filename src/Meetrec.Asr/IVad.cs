namespace Meetrec.Asr;

public interface IVad
{
    IReadOnlyList<SpeechRegion> Detect(float[] samples, int sampleRate, VadOptions options);
}

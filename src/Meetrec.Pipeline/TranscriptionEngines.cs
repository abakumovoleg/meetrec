using Meetrec.Asr;
using Meetrec.Core.Models;
using Meetrec.Diarization;

namespace Meetrec.Pipeline;

// The transcriber is loaded on demand and released before diarization, to keep peak memory down.
public sealed record TranscriptionEngines(Func<ITranscriber> LoadTranscriber, IVad Vad, IDiarizer Diarizer)
{
    public static TranscriptionEngines FromModels(ModelStore store, TranscriptionOptions options) => new(
        () => WhisperTranscriber.Load(store.PathOf(ModelCatalog.WhisperModel(options.WhisperModel))),
        new SileroVad(store.PathOf(ModelCatalog.SileroVad)),
        new SpeakerDiarizer(store.PathOf(ModelCatalog.PyannoteSegmentation), store.PathOf(options.Diarization.Embedding)));
}

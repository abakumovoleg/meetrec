using System.Text;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public static class TranscriptWriter
{
    private static readonly IReadOnlyDictionary<string, ITranscriptFormatter> Formatters =
        new ITranscriptFormatter[] { new MarkdownFormatter(), new JsonFormatter(), new SrtFormatter() }
            .ToDictionary(f => f.Format, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> SupportedFormats => [.. Formatters.Keys];

    public static IReadOnlyList<string> WriteAll(
        string directory, Conversation conversation, IEnumerable<string> formats, string name)
    {
        var paths = new List<string>();
        foreach (var format in formats.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Formatters.TryGetValue(format, out var formatter))
            {
                throw new ArgumentException($"Неизвестный формат: {format}", nameof(formats));
            }

            var path = SessionLayout.TranscriptPath(directory, name, formatter.Format);
            try
            {
                using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                formatter.Write(conversation, writer);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new MeetrecException($"Не удалось записать {path}: {e.Message}", e);
            }

            paths.Add(path);
        }

        return paths;
    }
}

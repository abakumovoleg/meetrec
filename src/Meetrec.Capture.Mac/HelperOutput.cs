using Meetrec.Core.Capture;

namespace Meetrec.Capture.Mac;

// What the helper printed, line by line: the greeting must come first and speak our protocol; lines that are not
// protocol messages are kept for the error text if the helper fails.
internal sealed class HelperOutput
{
    public const int Protocol = 1;

    private const int MaxUnrecognized = 20;

    private readonly Lock _gate = new();
    private readonly List<string> _unrecognized = [];
    private bool _greeted;

    // Returns the message to act on: null for blank lines, the greeting and unrecognized lines.
    public HelperMessage? Accept(string? line)
    {
        switch (HelperMessage.Parse(line))
        {
            case null:
                return null;
            case HelperMessage.Hello hello when hello.Protocol != Protocol:
                throw Incompatible($"он говорит на протоколе {hello.Protocol} (версия {hello.Version ?? "?"}), а нужен {Protocol}");
            case HelperMessage.Hello:
                _greeted = true;
                return null;
            case HelperMessage.Unrecognized unrecognized:
                lock (_gate)
                {
                    if (_unrecognized.Count < MaxUnrecognized)
                    {
                        _unrecognized.Add(unrecognized.Line);
                    }
                }

                return null;
            case var message when !_greeted:
                throw Incompatible($"он не представился, а сразу прислал «{line}» — вероятно, он старее meetrec");
            case var message:
                return message;
        }
    }

    public string Explain(string reason)
    {
        lock (_gate)
        {
            return _unrecognized.Count == 0
                ? reason
                : $"{reason}\nНепонятный вывод хелпера:\n{string.Join('\n', _unrecognized)}";
        }
    }

    private static CaptureException Incompatible(string details) => new(
        $"Хелпер записи {MacCaptureBackend.HelperFileName} не подходит к этой версии meetrec: {details}. "
        + "Установите meetrec и meetrec-capture из одной сборки.");
}

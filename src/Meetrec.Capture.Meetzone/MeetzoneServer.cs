using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Meetrec.Core.Capture;

namespace Meetrec.Capture.Meetzone;

// WebSocket server on localhost for the service worker of the meetrec extension. A plain TcpListener with a
// hand-made handshake instead of HttpListener: on Windows that one needs http.sys and a URL reservation.
// Only extension pages may connect: any site open in the browser can reach localhost, but its Origin is its own.
public sealed class MeetzoneServer : IAsyncDisposable
{
    public const int DefaultPort = 47823;

    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxMessageBytes = 4 * 1024 * 1024;
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener;
    private readonly Action<MeetzoneMessage> _onMessage;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private WebSocket? _current;
    private TaskCompletionSource _served = CompletedSource();
    private Task _accept = Task.CompletedTask;
    private bool _recording = true;

    public MeetzoneServer(int port, Action<MeetzoneMessage> onMessage)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _onMessage = onMessage;
    }

    public event Action<Hello>? Connected;

    public event Action? Disconnected;

    // A broken message or connection is reported but does not stop the recording: the extension reconnects.
    public event Action<string>? Problem;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        try
        {
            _listener.Start();
        }
        catch (SocketException e)
        {
            throw new CaptureException($"Не удалось открыть порт {((IPEndPoint)_listener.LocalEndpoint).Port} для расширения: {e.Message}. Уже идёт другая запись Meetzone?");
        }

        _accept = AcceptLoopAsync(_stop.Token);
    }

    // Asks the page to stop sending audio; what was already sent is still delivered before the socket closes.
    public async Task StopAsync()
    {
        WebSocket? socket;
        lock (_gate)
        {
            _recording = false;
            socket = _current;
        }

        if (socket is not null)
        {
            await SendAsync(socket, MeetzoneMessage.Stop());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Only the outgoing half: the receive loop gets the reply, so that two receives never run at once.
            try
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "stop", timeout.Token);
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException or IOException)
            {
            }
        }

        Task served;
        lock (_gate)
        {
            served = _served.Task;
        }

        await Task.WhenAny(served, Task.Delay(TimeSpan.FromSeconds(3)));
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _accept;
        }
        catch (OperationCanceledException)
        {
        }

        _current?.Dispose();
        _stop.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            // One connection at a time: a new one means the service worker was restarted and the old socket is dead.
            await ServeAsync(client, ct);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        var stream = client.GetStream();
        WebSocket socket;
        try
        {
            if (await HandshakeAsync(stream, ct) is not { } accepted)
            {
                return;
            }

            socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.FromSeconds(20) });
            await stream.WriteAsync(accepted, ct);
        }
        catch (Exception e) when (e is IOException or SocketException)
        {
            return;
        }

        lock (_gate)
        {
            _current = socket;
            _served = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            await ReceiveAsync(socket, ct);
        }
        catch (Exception e) when (e is WebSocketException or IOException)
        {
            Problem?.Invoke($"соединение с расширением оборвалось: {e.Message}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                _current = null;
                _served.TrySetResult();
            }

            socket.Dispose();
            Disconnected?.Invoke();
        }
    }

    private async Task ReceiveAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                }

                return;
            }

            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "too big", ct);
                Problem?.Invoke("расширение прислало слишком большое сообщение");
                return;
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            await DispatchAsync(socket, text);
        }
    }

    private async Task DispatchAsync(WebSocket socket, string text)
    {
        MeetzoneMessage? parsed;
        try
        {
            parsed = MeetzoneMessage.Parse(text);
        }
        catch (CaptureException e)
        {
            Problem?.Invoke(e.Message);
            return;
        }

        if (parsed is Hello hello)
        {
            if (hello.Protocol != MeetzoneMessage.ProtocolVersion)
            {
                Problem?.Invoke($"расширение meetrec {hello.Version} говорит на протоколе {hello.Protocol}, а meetrec — на {MeetzoneMessage.ProtocolVersion}: обновите расширение из той же сборки, что и meetrec");
                await SendAsync(socket, MeetzoneMessage.Stop());
                return;
            }

            Connected?.Invoke(hello);
            bool recording;
            lock (_gate)
            {
                recording = _recording;
            }

            await SendAsync(socket, recording ? MeetzoneMessage.Start() : MeetzoneMessage.Stop());
            return;
        }

        if (parsed is not null)
        {
            _onMessage(parsed);
        }
    }

    private static async Task SendAsync(WebSocket socket, string text)
    {
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException)
        {
        }
    }

    // Returns the 101 response to send, or null after answering the request with an error.
    private static async Task<byte[]?> HandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadHeadersAsync(stream, ct);
        var lines = request.Split("\r\n");
        var headers = lines.Skip(1)
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (!lines[0].StartsWith("GET ", StringComparison.Ordinal)
            || !headers.TryGetValue("Sec-WebSocket-Key", out var key)
            || !headers.TryGetValue("Upgrade", out var upgrade) || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"), ct);
            return null;
        }

        if (!headers.TryGetValue("Origin", out var origin) || !IsExtension(origin))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"), ct);
            return null;
        }

        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid)));
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n");
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    internal static bool IsExtension(string origin) =>
        origin.StartsWith("chrome-extension://", StringComparison.Ordinal);

    private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < MaxHeaderBytes)
        {
            if (await stream.ReadAsync(one, ct) == 0)
            {
                throw new IOException("соединение закрыто до конца заголовков");
            }

            bytes.Add(one[0]);
            if (bytes.Count >= 4 && bytes[^4] == '\r' && bytes[^3] == '\n' && bytes[^2] == '\r' && bytes[^1] == '\n')
            {
                return Encoding.ASCII.GetString([.. bytes]);
            }
        }

        throw new IOException("слишком длинные заголовки");
    }
}

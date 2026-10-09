using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Meetrec.Capture.Meetzone;

namespace Meetrec.Tests;

public class MeetzoneServerTests
{
    private const string Hello = """{"t":"hello","protocol":1,"version":"0.1.0","url":"https://chatzone.o3t.ru/meet/x"}""";

    [Fact]
    public async Task Extension_SaysHello_GetsStartAndItsMessagesArrive()
    {
        // Arrange
        var received = new BlockingCollection<MeetzoneMessage>();
        await using var server = new MeetzoneServer(0, received.Add);
        var connected = new TaskCompletionSource<Hello>();
        server.Connected += h => connected.TrySetResult(h);
        server.Start();
        using var client = await ConnectAsync(server.Port, "chrome-extension://abcdef");

        // Act
        await SendAsync(client, Hello);
        var reply = await ReceiveAsync(client);
        await SendAsync(client, """{"t":"presence","endpoint":"e1","name":"Анна","login":"anna","own":true}""");

        // Assert
        Assert.Equal("""{"t":"start"}""", reply);
        Assert.Equal("0.1.0", (await connected.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Version);
        Assert.True(received.TryTake(out var message, Timeout));
        Assert.Equal(new Presence("e1", "Анна", "anna", true, false), message);
    }

    [Fact]
    public async Task Site_IsRefused()
    {
        // Arrange
        await using var server = new MeetzoneServer(0, _ => { });
        server.Start();

        // Act
        var error = await Record.ExceptionAsync(() => ConnectAsync(server.Port, "https://evil.example"));

        // Assert
        Assert.IsType<WebSocketException>(error);
    }

    [Fact]
    public async Task OtherProtocol_IsToldToStopAndReported()
    {
        // Arrange
        await using var server = new MeetzoneServer(0, _ => { });
        var problem = new TaskCompletionSource<string>();
        server.Problem += p => problem.TrySetResult(p);
        server.Start();
        using var client = await ConnectAsync(server.Port, "chrome-extension://abcdef");

        // Act
        await SendAsync(client, Hello.Replace("\"protocol\":1", "\"protocol\":2", StringComparison.Ordinal));
        var reply = await ReceiveAsync(client);

        // Assert
        Assert.Equal("""{"t":"stop"}""", reply);
        Assert.Contains("протоколе 2", await problem.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StopAsync_SendsStopAndCloses()
    {
        // Arrange
        await using var server = new MeetzoneServer(0, _ => { });
        server.Start();
        using var client = await ConnectAsync(server.Port, "chrome-extension://abcdef");
        await SendAsync(client, Hello);
        await ReceiveAsync(client);

        // Act
        var stopping = server.StopAsync();
        var reply = await ReceiveAsync(client);
        await ReceiveAsync(client);
        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await stopping.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("""{"t":"stop"}""", reply);
    }

    [Fact]
    public async Task ExtensionClosing_GetsCloseHandshake()
    {
        // Arrange
        await using var server = new MeetzoneServer(0, _ => { });
        var disconnected = new TaskCompletionSource();
        server.Disconnected += () => disconnected.TrySetResult();
        server.Start();
        using var client = await ConnectAsync(server.Port, "chrome-extension://abcdef");
        await SendAsync(client, Hello);
        await ReceiveAsync(client);

        // Act
        using var timeout = new CancellationTokenSource(Timeout);
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token);

        // Assert
        Assert.Equal(WebSocketState.Closed, client.State);
        await disconnected.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static async Task<ClientWebSocket> ConnectAsync(int port, string origin)
    {
        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Origin", origin);
        using var timeout = new CancellationTokenSource(Timeout);
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), timeout.Token);
        return client;
    }

    private static Task SendAsync(ClientWebSocket client, string text) =>
        client.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<string?> ReceiveAsync(ClientWebSocket client)
    {
        var buffer = new byte[4096];
        using var timeout = new CancellationTokenSource(Timeout);
        var result = await client.ReceiveAsync(buffer, timeout.Token);
        return result.MessageType == WebSocketMessageType.Close ? null : Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}

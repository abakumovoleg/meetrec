using System.IO.Compression;
using System.Net;
using Meetrec.Core.Models;

namespace Meetrec.Core.Tests;

public sealed class ModelStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Directory.CreateTempSubdirectory("meetrec-tests-").FullName, "models");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);

    private static ModelInfo FileModel(long size = 0) =>
        new("test-file", ModelKind.Vad, new Uri("https://example.test/model.onnx"), "model.onnx", ModelPackaging.File, size);

    private static ModelInfo ZipModel() =>
        new("test-zip", ModelKind.WhisperCoreMlEncoder, new Uri("https://example.test/enc.zip"), "enc.mlmodelc", ModelPackaging.ZippedDirectory, 0);

    private static byte[] ZipWith(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private sealed class Recorder : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Reports { get; } = [];

        public void Report(DownloadProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task EnsureAsync_MissingFile_DownloadsReportsProgressAndLeavesNoPartFile()
    {
        // Arrange
        var payload = Enumerable.Range(0, 3_000_000).Select(i => (byte)i).ToArray();
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Bytes(payload));
        var store = new ModelStore(_root);
        var progress = new Recorder();

        // Act
        var path = await store.EnsureAsync(FileModel(), new HttpClient(handler), progress, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Path.Combine(_root, "model.onnx"), path);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.True(store.IsInstalled(FileModel()));
        Assert.Equal(["model.onnx"], Directory.GetFileSystemEntries(_root).Select(Path.GetFileName));
        Assert.Equal(0, progress.Reports[0].BytesReceived);
        Assert.Equal(payload.Length, progress.Reports[^1].BytesReceived);
        Assert.All(progress.Reports, r => Assert.Equal(payload.Length, r.TotalBytes));
    }

    [Fact]
    public async Task EnsureAsync_AlreadyInstalled_DoesNotDownload()
    {
        // Arrange
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "model.onnx"), "есть", TestContext.Current.CancellationToken);
        var handler = new FakeHttpHandler(_ => throw new InvalidOperationException("сеть не нужна"));
        var store = new ModelStore(_root);

        // Act
        var path = await store.EnsureAsync(FileModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("есть", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task EnsureAsync_HttpError_ThrowsAndInstallsNothing()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(FileModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        var error = await Assert.ThrowsAsync<MeetrecException>(act);
        Assert.Contains("404", error.Message);
        Assert.False(store.IsInstalled(FileModel()));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task EnsureAsync_TruncatedBody_ThrowsAndInstallsNothing()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Truncated(new byte[1000], declaredLength: 5000));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(FileModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        var error = await Assert.ThrowsAsync<MeetrecException>(act);
        Assert.Contains("test-file", error.Message);
        Assert.False(store.IsInstalled(FileModel()));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task EnsureAsync_NetworkFailure_ThrowsWithModelAndInstallsNothing()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("Connection refused"));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(FileModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        var error = await Assert.ThrowsAsync<MeetrecException>(act);
        Assert.Contains("test-file", error.Message);
        Assert.Contains("Connection refused", error.Message);
        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.False(store.IsInstalled(FileModel()));
    }

    [Fact]
    public async Task EnsureAsync_CorruptZip_ThrowsAndCleansUp()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Bytes("это не zip"u8.ToArray()));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(ZipModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        var error = await Assert.ThrowsAsync<MeetrecException>(act);
        Assert.Contains("test-zip", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task EnsureAsync_Cancelled_InstallsNothing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Bytes(new byte[10]));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(FileModel(), new HttpClient(handler), null, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.False(store.IsInstalled(FileModel()));
    }

    [Fact]
    public async Task EnsureAsync_ZippedDirectory_ExtractsDirectoryAndCleansUp()
    {
        // Arrange
        var zip = ZipWith(("enc.mlmodelc/model.mil", "mil"), ("enc.mlmodelc/weights/weight.bin", "w"));
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Bytes(zip));
        var store = new ModelStore(_root);

        // Act
        var path = await store.EnsureAsync(ZipModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Path.Combine(_root, "enc.mlmodelc"), path);
        Assert.Equal("w", await File.ReadAllTextAsync(Path.Combine(path, "weights", "weight.bin"), TestContext.Current.CancellationToken));
        Assert.True(store.IsInstalled(ZipModel()));
        Assert.Equal(["enc.mlmodelc"], Directory.GetFileSystemEntries(_root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task EnsureAsync_ZipWithoutExpectedDirectory_ThrowsAndCleansUp()
    {
        // Arrange
        var zip = ZipWith(("other/model.mil", "mil"));
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Bytes(zip));
        var store = new ModelStore(_root);

        // Act
        var act = () => store.EnsureAsync(ZipModel(), new HttpClient(handler), null, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<MeetrecException>(act);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
}

using System.IO.Compression;

namespace Meetrec.Core.Models;

public sealed class ModelStore(string root)
{
    private const int BufferSize = 1 << 20;

    public string Root { get; } = root;

    public string PathOf(ModelInfo model) => Path.Combine(Root, model.InstallName);

    public bool IsInstalled(ModelInfo model) => model.Packaging switch
    {
        ModelPackaging.File => File.Exists(PathOf(model)),
        ModelPackaging.ZippedDirectory => Directory.Exists(PathOf(model)),
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    public async Task<string> EnsureAsync(ModelInfo model, HttpClient http, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var target = PathOf(model);
        if (IsInstalled(model))
        {
            return target;
        }

        Directory.CreateDirectory(Root);
        var part = target + ".part";
        try
        {
            await DownloadAsync(http, model, part, progress, ct);
            Install(model, part, target);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new MeetrecException($"Не удалось скачать {model.Id} ({model.Url}): {e.Message}", e);
        }
        finally
        {
            File.Delete(part);
        }

        return target;
    }

    private static async Task DownloadAsync(HttpClient http, ModelInfo model, string path, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        using var response = await http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new MeetrecException($"Не удалось скачать {model.Id} ({model.Url}): HTTP {(int)response.StatusCode}");
        }

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        long received = 0;
        progress?.Report(new DownloadProgress(model, 0, total));
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            progress?.Report(new DownloadProgress(model, received, total));
        }

        if (total is { } expected && received != expected)
        {
            throw new MeetrecException($"Загрузка {model.Id} оборвалась: получено {received} из {expected} байт");
        }
    }

    private void Install(ModelInfo model, string downloaded, string target)
    {
        switch (model.Packaging)
        {
            case ModelPackaging.File:
                File.Move(downloaded, target, overwrite: true);
                break;
            case ModelPackaging.ZippedDirectory:
                var staging = Path.Combine(Root, $".{model.InstallName}.extract");
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }

                try
                {
                    ZipFile.ExtractToDirectory(downloaded, staging);
                    var extracted = Path.Combine(staging, model.InstallName);
                    if (!Directory.Exists(extracted))
                    {
                        throw new MeetrecException($"В архиве {model.Id} нет папки {model.InstallName}");
                    }

                    Directory.Move(extracted, target);
                }
                finally
                {
                    if (Directory.Exists(staging))
                    {
                        Directory.Delete(staging, recursive: true);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model));
        }
    }
}

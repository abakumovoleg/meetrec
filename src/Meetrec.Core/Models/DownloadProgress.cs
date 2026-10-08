namespace Meetrec.Core.Models;

public readonly record struct DownloadProgress(ModelInfo Model, long BytesReceived, long? TotalBytes);

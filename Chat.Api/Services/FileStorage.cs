using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Chat.Api.Services;

/// <summary>Where attachment bytes live. Local disk for development, Azure Blob Storage in production.</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
}

public sealed class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    private string Dir => Path.Combine(env.ContentRootPath, "storage"); // outside wwwroot

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(Dir);
        await using var fs = File.Create(Path.Combine(Dir, key));
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Path.Combine(Dir, key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }
}

public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient container;

    public AzureBlobFileStorage(IConfiguration cfg)
    {
        container = new BlobContainerClient(
            cfg["Storage:AzureBlob:ConnectionString"],
            cfg["Storage:AzureBlob:Container"] ?? "attachments");
        container.CreateIfNotExists(PublicAccessType.None); // private: only this API can read files
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct) =>
        await container.GetBlobClient(key).UploadAsync(
            content,
            httpHeaders: new BlobHttpHeaders { ContentType = "application/octet-stream" },
            cancellationToken: ct);

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var blob = container.GetBlobClient(key);
        if (!await blob.ExistsAsync(ct)) return null;
        return await blob.OpenReadAsync(new BlobOpenReadOptions(false), ct);
    }
}

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace StudentArchive.Web.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string blobPath, IFormFile file,
        Dictionary<string, string> metadata);
    Task<string> GenerateDownloadUrlAsync(string blobPath);
    Task DeleteAsync(string blobPath);
    Task<IEnumerable<string>> ListStudentBlobsAsync(string studentNumber);
}

// Production implementation — uses real Azure Blob Storage
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly IBlobPathService _pathService;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        IBlobPathService pathService,
        IConfiguration config,
        ILogger<AzureBlobStorageService> logger)
    {
        var containerName = config["BlobStorage:ContainerName"]
            ?? throw new InvalidOperationException("BlobStorage:ContainerName not configured");

        _container = blobServiceClient.GetBlobContainerClient(containerName);
        _pathService = pathService;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string blobPath, IFormFile file,
        Dictionary<string, string> metadata)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            Metadata = metadata,
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        });

        _logger.LogInformation("Uploaded blob {BlobPath}", blobPath);
        return blobClient.Uri.ToString();
    }

    public async Task<string> GenerateDownloadUrlAsync(string blobPath)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync())
            throw new FileNotFoundException($"Blob not found: {blobPath}");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15),
            Protocol = SasProtocol.Https
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task DeleteAsync(string blobPath)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        _logger.LogInformation("Deleted blob {BlobPath}", blobPath);
    }

    public async Task<IEnumerable<string>> ListStudentBlobsAsync(string studentNumber)
    {
        var blobs = new List<string>();
        await foreach (var blob in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: $"{studentNumber}/", CancellationToken.None))
            blobs.Add(blob.Name);
        return blobs;
    }
}

// Demo implementation — stores files in local temp folder, no Azure needed
public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(
        IConfiguration config,
        ILogger<LocalBlobStorageService> logger)
    {
        // appsettings.Demo.json ships LocalPath as "", so treat blank as unset
        var configured = config["DemoStorage:LocalPath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "StudentArchiveDemo")
            : configured;

        Directory.CreateDirectory(_basePath);
        _logger = logger;
    }

    public async Task<string> UploadAsync(string blobPath, IFormFile file,
        Dictionary<string, string> metadata)
    {
        var fullPath = Path.Combine(_basePath, blobPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        _logger.LogInformation("[DEMO] Saved file to {Path}", fullPath);
        return $"/demo-files/{blobPath}";
    }

    public Task<string> GenerateDownloadUrlAsync(string blobPath)
    {
        // In demo mode, return a local download route
        return Task.FromResult($"/File/DemoDownload?path={Uri.EscapeDataString(blobPath)}");
    }

    public Task DeleteAsync(string blobPath)
    {
        var fullPath = Path.Combine(_basePath, blobPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<string>> ListStudentBlobsAsync(string studentNumber)
    {
        var studentPath = Path.Combine(_basePath, studentNumber);
        if (!Directory.Exists(studentPath))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(studentPath)
            .Select(f => $"{studentNumber}/{Path.GetFileName(f)}");
    }
}

namespace StudentArchive.Web.Services;

public interface IFileValidationService
{
    Task<(bool IsValid, string Error)> ValidateAsync(IFormFile file);
}

public class FileValidationService : IFileValidationService
{
    private const long MaxFileSizeBytes = 52_428_800; // 50 MB

    private static readonly Dictionary<string, byte[]> AllowedSignatures = new()
    {
        { "application/pdf",   new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        { "image/jpeg",        new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png",         new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                               new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                               new byte[] { 0x50, 0x4B, 0x03, 0x04 } }
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"
    };

    public async Task<(bool IsValid, string Error)> ValidateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "No file was provided.");

        if (file.Length > MaxFileSizeBytes)
            return (false, "File exceeds the 50 MB size limit.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return (false, $"File type '{extension}' is not permitted.");

        if (!AllowedSignatures.ContainsKey(file.ContentType))
            return (false, $"Content type '{file.ContentType}' is not permitted.");

        // Magic bytes check — read actual file header
        using var stream = file.OpenReadStream();
        var header = new byte[4];
        var bytesRead = await stream.ReadAsync(header, 0, 4);

        if (bytesRead < 3)
            return (false, "File is too small to be valid.");

        var expectedSignature = AllowedSignatures[file.ContentType];
        var matches = header.Take(expectedSignature.Length)
                            .SequenceEqual(expectedSignature);

        if (!matches)
            return (false, "File content does not match its declared type.");

        return (true, string.Empty);
    }
}

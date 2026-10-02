using System.Text.RegularExpressions;

namespace StudentArchive.Web.Services;

public interface IBlobPathService
{
    string BuildBlobPath(string studentNumber, string fileName);
    bool BlobBelongsToStudent(string blobPath, string studentNumber);
}

public class BlobPathService : IBlobPathService
{
    public string BuildBlobPath(string studentNumber, string fileName)
    {
        var sanitized = SanitizeFileName(fileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        // Format: STU001234/STU001234_20240315_143022_transcript.pdf
        return $"{studentNumber}/{studentNumber}_{timestamp}_{sanitized}";
    }

    public bool BlobBelongsToStudent(string blobPath, string studentNumber)
    {
        return blobPath.StartsWith($"{studentNumber}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(fileName);

        // Replace anything that isn't alphanumeric, dash, or underscore
        name = Regex.Replace(name, @"[^a-zA-Z0-9\-_]", "_");

        // Trim leading/trailing underscores
        name = name.Trim('_');

        // Ensure we have something left
        if (string.IsNullOrEmpty(name))
            name = "file";

        return $"{name}{ext}";
    }
}

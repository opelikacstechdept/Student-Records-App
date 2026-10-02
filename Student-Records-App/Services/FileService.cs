using Microsoft.EntityFrameworkCore;
using StudentArchive.Web.Data;
using StudentArchive.Web.Models;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Services;

/// <summary>
/// Handles all file upload, retrieval, and deletion operations for student files.
/// Coordinates between Azure Blob Storage and the SQL metadata store.
/// </summary>
public interface IFileService
{
    /// <summary>Uploads a file to blob storage and records metadata in SQL.</summary>
    Task<StudentFile> UploadAsync(string studentNumber, FileUploadViewModel vm,
        string uploadedByUserId, string uploadedByUserName);

    /// <summary>Returns a short-lived SAS download URL for a given file.</summary>
    Task<string> GetDownloadUrlAsync(int fileId, string studentNumber);

    /// <summary>Returns full metadata for a single file record.</summary>
    Task<StudentFile?> GetByIdAsync(int fileId);

    /// <summary>Permanently deletes a file from blob storage and removes its SQL record.</summary>
    Task DeleteAsync(int fileId, string requestingStudentNumber);

    /// <summary>Returns all dropdown options needed for the upload form.</summary>
    Task<FileUploadViewModel> BuildUploadViewModelAsync(string studentNumber, string studentName);
}

/// <summary>
/// Production and demo implementation of <see cref="IFileService"/>.
/// Uses injected <see cref="IBlobStorageService"/> so production vs demo
/// behaviour is determined by which implementation is registered at startup.
/// </summary>
public class FileService : IFileService
{
    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    private readonly StudentArchiveDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly IBlobPathService _blobPathService;
    private readonly IFileValidationService _validator;
    private readonly ILogger<FileService> _logger;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public FileService(
        StudentArchiveDbContext context,
        IBlobStorageService blobStorage,
        IBlobPathService blobPathService,
        IFileValidationService validator,
        ILogger<FileService> logger)
    {
        _context = context;
        _blobStorage = blobStorage;
        _blobPathService = blobPathService;
        _validator = validator;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public methods
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<StudentFile> UploadAsync(
        string studentNumber,
        FileUploadViewModel vm,
        string uploadedByUserId,
        string uploadedByUserName)
    {
        // Step 1 — validate the file before touching any storage
        var (isValid, error) = await _validator.ValidateAsync(vm.File!);
        if (!isValid)
            throw new InvalidOperationException(error);

        // Step 2 — resolve the student record
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber)
            ?? throw new InvalidOperationException(
                $"Student {studentNumber} not found.");

        // Step 3 — build the deterministic blob path under the student folder
        var blobPath = _blobPathService.BuildBlobPath(studentNumber, vm.File!.FileName);

        // Step 4 — attach metadata to the blob itself as a secondary record
        var blobMetadata = BuildBlobMetadata(
            studentNumber, uploadedByUserId, vm.File.FileName, vm);

        // Step 5 — stream the file to blob storage
        var blobUrl = await _blobStorage.UploadAsync(blobPath, vm.File, blobMetadata);

        _logger.LogInformation(
            "File uploaded to blob storage. Student: {StudentNumber}, Path: {BlobPath}",
            studentNumber, blobPath);

        // Step 6 — persist the SQL record
        var studentFile = new StudentFile
        {
            StudentId       = student.StudentId,
            BlobPath        = blobPath,
            BlobUrl         = blobUrl,
            FileName        = vm.File.FileName,
            ContentType     = vm.File.ContentType,
            DocumentType    = vm.DocumentType,
            AcademicYear    = vm.AcademicYear,
            FileSizeBytes   = vm.File.Length,
            UploadedAt      = DateTime.UtcNow,
            UploadedByUserId    = uploadedByUserId,
            UploadedByUserName  = uploadedByUserName
        };

        // Step 7 — add any additional flexible metadata key/value pairs
        AddFlexibleMetadata(studentFile, vm);

        _context.StudentFiles.Add(studentFile);
        await _context.SaveChangesAsync();

        return studentFile;
    }

    /// <inheritdoc/>
    public async Task<string> GetDownloadUrlAsync(int fileId, string studentNumber)
    {
        var file = await GetByIdAsync(fileId)
            ?? throw new FileNotFoundException($"File {fileId} not found.");

        // Security: confirm the file actually belongs to the requested student
        if (!_blobPathService.BlobBelongsToStudent(file.BlobPath, studentNumber))
            throw new UnauthorizedAccessException(
                "File does not belong to the requested student.");

        return await _blobStorage.GenerateDownloadUrlAsync(file.BlobPath);
    }

    /// <inheritdoc/>
    public async Task<StudentFile?> GetByIdAsync(int fileId) =>
        await _context.StudentFiles
            .Include(f => f.Metadata)
            .Include(f => f.Student)
            .FirstOrDefaultAsync(f => f.FileId == fileId);

    /// <inheritdoc/>
    public async Task DeleteAsync(int fileId, string requestingStudentNumber)
    {
        var file = await GetByIdAsync(fileId)
            ?? throw new FileNotFoundException($"File {fileId} not found.");

        if (!_blobPathService.BlobBelongsToStudent(file.BlobPath, requestingStudentNumber))
            throw new UnauthorizedAccessException(
                "File does not belong to the requested student.");

        // Delete from blob storage first; if this fails the SQL record is preserved
        await _blobStorage.DeleteAsync(file.BlobPath);

        _context.StudentFiles.Remove(file);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "File deleted. FileId: {FileId}, BlobPath: {BlobPath}", fileId, file.BlobPath);
    }

    /// <inheritdoc/>
    public async Task<FileUploadViewModel> BuildUploadViewModelAsync(
        string studentNumber, string studentName)
    {
        var years = GenerateAcademicYears();

        return new FileUploadViewModel
        {
            StudentNumber = studentNumber,
            StudentName   = studentName,
            DocumentTypeOptions = GetDocumentTypeOptions(),
            AcademicYearOptions = years
                .Select(y => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(y, y))
                .ToList(),
            SemesterOptions = GetSemesterOptions()
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the blob-level metadata dictionary written alongside the binary file.
    /// This is a secondary record; SQL is the authoritative metadata store.
    /// </summary>
    private static Dictionary<string, string> BuildBlobMetadata(
        string studentNumber,
        string uploadedByUserId,
        string originalFileName,
        FileUploadViewModel vm) => new()
    {
        { "StudentNumber",    studentNumber },
        { "UploadedBy",       uploadedByUserId },
        { "OriginalFileName", originalFileName },
        { "DocumentType",     vm.DocumentType },
        { "AcademicYear",     vm.AcademicYear }
    };

    /// <summary>
    /// Adds optional flexible key/value metadata rows to the file record.
    /// Only non-empty values are persisted.
    /// </summary>
    private static void AddFlexibleMetadata(StudentFile file, FileUploadViewModel vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.Semester))
            file.Metadata.Add(new FileMetadataItem { Key = "Semester", Value = vm.Semester });

        if (!string.IsNullOrWhiteSpace(vm.Department))
            file.Metadata.Add(new FileMetadataItem { Key = "Department", Value = vm.Department });

        if (!string.IsNullOrWhiteSpace(vm.Notes))
            file.Metadata.Add(new FileMetadataItem { Key = "Notes", Value = vm.Notes });
    }

    /// <summary>Generates a rolling 10-year list of academic years.</summary>
    private static List<string> GenerateAcademicYears()
    {
        var current = DateTime.UtcNow.Year;
        return Enumerable.Range(current - 8, 10)
            .Select(y => $"{y}-{y + 1}")
            .Reverse()
            .ToList();
    }

    private static List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetDocumentTypeOptions() =>
        new List<string>
        {
            "Transcript", "Enrollment Letter", "Financial Aid", "ID Scan",
            "Medical Record", "Disciplinary Record", "Correspondence", "Other"
        }
        .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(d, d))
        .ToList();

    private static List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetSemesterOptions() =>
        new List<string> { "Fall", "Spring", "Summer" }
        .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(s, s))
        .ToList();
}

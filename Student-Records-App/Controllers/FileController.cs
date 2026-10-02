using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentArchive.Web.Extensions;
using StudentArchive.Web.Models;
using StudentArchive.Web.Services;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Controllers;

/// <summary>
/// Handles file upload, download, and deletion for student records.
/// Upload actions require the Admin or Uploader role.
/// Download is available to all authenticated roles.
/// </summary>
[Authorize(Roles = "Admin,Uploader,Viewer")]
public class FileController : Controller
{
    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    private readonly IFileService _fileService;
    private readonly IStudentService _studentService;
    private readonly IAuditService _auditService;
    private readonly ILogger<FileController> _logger;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public FileController(
        IFileService fileService,
        IStudentService studentService,
        IAuditService auditService,
        ILogger<FileController> logger)
    {
        _fileService    = fileService;
        _studentService = studentService;
        _auditService   = auditService;
        _logger         = logger;
    }

    // -------------------------------------------------------------------------
    // Upload actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /File/Upload/{studentNumber}
    /// Renders the file upload form pre-populated with student details
    /// and dropdown options.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Uploader")]
    public async Task<IActionResult> Upload(string studentNumber)
    {
        var student = await _studentService.GetByStudentNumberAsync(studentNumber);
        if (student == null) return NotFound();

        var vm = await _fileService.BuildUploadViewModelAsync(
            studentNumber, student.FullName);

        return View(vm);
    }

    /// <summary>
    /// POST /File/Upload/{studentNumber}
    /// Validates, uploads, and records a student file.
    /// Rate-limited to 10 uploads per minute per user.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Uploader")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload(string studentNumber, FileUploadViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            // Rebuild dropdown options before returning the form
            var rebuilt = await _fileService.BuildUploadViewModelAsync(
                studentNumber, vm.StudentName);
            vm.DocumentTypeOptions = rebuilt.DocumentTypeOptions;
            vm.AcademicYearOptions = rebuilt.AcademicYearOptions;
            vm.SemesterOptions     = rebuilt.SemesterOptions;
            return View(vm);
        }

        try
        {
            var file = await _fileService.UploadAsync(
                studentNumber:      studentNumber,
                vm:                 vm,
                uploadedByUserId:   User.GetUserId(),
                uploadedByUserName: User.GetDisplayName());

            await _auditService.LogAsync(
                userId:     User.GetUserId(),
                userEmail:  User.GetEmail(),
                action:     AuditActions.FileUploaded,
                entityType: "StudentFile",
                entityId:   file.FileId.ToString(),
                ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                details: new
                {
                    studentNumber,
                    file.FileName,
                    file.DocumentType,
                    file.AcademicYear,
                    file.BlobPath
                });

            TempData["Success"] = $"{vm.File!.FileName} uploaded successfully.";
            return RedirectToAction("Detail", "Student", new { studentNumber });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var rebuilt = await _fileService.BuildUploadViewModelAsync(
                studentNumber, vm.StudentName);
            vm.DocumentTypeOptions = rebuilt.DocumentTypeOptions;
            vm.AcademicYearOptions = rebuilt.AcademicYearOptions;
            vm.SemesterOptions     = rebuilt.SemesterOptions;
            return View(vm);
        }
    }

    // -------------------------------------------------------------------------
    // Download actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /File/Download/{fileId}?studentNumber={studentNumber}
    /// Generates a 15-minute SAS token URL and redirects the user to it.
    /// The student number is verified against the file record before the
    /// SAS token is issued — prevents cross-student file access.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting("download")]
    public async Task<IActionResult> Download(int fileId, string studentNumber)
    {
        try
        {
            var downloadUrl = await _fileService.GetDownloadUrlAsync(fileId, studentNumber);

            await _auditService.LogAsync(
                userId:     User.GetUserId(),
                userEmail:  User.GetEmail(),
                action:     AuditActions.FileDownloaded,
                entityType: "StudentFile",
                entityId:   fileId.ToString(),
                ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                details:    new { fileId, studentNumber });

            // Redirect to the time-limited SAS URL — file is served directly from Azure
            return Redirect(downloadUrl);
        }
        catch (UnauthorizedAccessException)
        {
            await _auditService.LogAsync(
                userId:     User.GetUserId(),
                userEmail:  User.GetEmail(),
                action:     AuditActions.UnauthorizedAccess,
                entityType: "StudentFile",
                entityId:   fileId.ToString(),
                ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                details:    new { fileId, studentNumber, reason = "Cross-student blob access attempt" });

            _logger.LogWarning(
                "Unauthorized download attempt. User: {UserId}, FileId: {FileId}, StudentNumber: {SN}",
                User.GetUserId(), fileId, studentNumber);

            return Forbid();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// GET /File/DemoDownload
    /// Demo-mode only. Serves files from the local filesystem instead of Azure.
    /// This action is unreachable in production because the demo blob service
    /// is not registered.
    /// </summary>
    [HttpGet]
    public IActionResult DemoDownload(string path)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "StudentArchiveDemo");
        var fullPath = Path.GetFullPath(
            Path.Combine(basePath, path.Replace('/', Path.DirectorySeparatorChar)));

        // Path traversal guard — ensure the resolved path is still within basePath
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid file path.");

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var contentType = GetContentType(Path.GetExtension(fullPath));
        return PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath));
    }

    // -------------------------------------------------------------------------
    // Delete action
    // -------------------------------------------------------------------------

    /// <summary>
    /// POST /File/Delete
    /// Permanently deletes a file from blob storage and removes its SQL record.
    /// Restricted to Admin role only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int fileId, string studentNumber)
    {
        try
        {
            await _fileService.DeleteAsync(fileId, studentNumber);

            await _auditService.LogAsync(
                userId:     User.GetUserId(),
                userEmail:  User.GetEmail(),
                action:     "FileDeleted",
                entityType: "StudentFile",
                entityId:   fileId.ToString(),
                ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                details:    new { fileId, studentNumber });

            TempData["Success"] = "File deleted successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You do not have permission to delete this file.";
        }
        catch (FileNotFoundException)
        {
            TempData["Error"] = "File not found.";
        }

        return RedirectToAction("Detail", "Student", new { studentNumber });
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a file extension to its MIME content type for demo file serving.
    /// </summary>
    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf"  => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".jpg"  => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        _       => "application/octet-stream"
    };
}

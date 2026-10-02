using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentArchive.Web.Extensions;
using StudentArchive.Web.Models;
using StudentArchive.Web.Services;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Controllers;

/// <summary>
/// Manages student record creation and detail views.
/// Only administrators may create student records; every role may view
/// a student's detail page. File access is controlled separately
/// via <see cref="FileController"/>.
/// </summary>
/// <remarks>
/// Role restrictions are per action, not on the class: stacked [Authorize]
/// attributes are combined with AND, so a class-level Admin requirement
/// would also lock Uploaders and Viewers out of <see cref="Detail"/>.
/// </remarks>
[Authorize(Roles = "Admin,Uploader,Viewer")]
public class StudentController : Controller
{
    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    private readonly IStudentService _studentService;
    private readonly IAuditService _auditService;
    private readonly ILogger<StudentController> _logger;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public StudentController(
        IStudentService studentService,
        IAuditService auditService,
        ILogger<StudentController> logger)
    {
        _studentService = studentService;
        _auditService   = auditService;
        _logger         = logger;
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /Student/Add
    /// Renders the add-student form.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Add() => View(new AddStudentViewModel());

    /// <summary>
    /// POST /Student/Add
    /// Validates and persists a new student record.
    /// Redirects to the student detail page on success.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddStudentViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            var student = await _studentService.CreateAsync(vm, User.GetUserId());

            await _auditService.LogAsync(
                userId:     User.GetUserId(),
                userEmail:  User.GetEmail(),
                action:     AuditActions.StudentCreated,
                entityType: "Student",
                entityId:   student.StudentNumber,
                ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                details:    new { student.StudentNumber, student.FullName, student.Status });

            _logger.LogInformation(
                "Student created: {StudentNumber} by {UserId}",
                student.StudentNumber, User.GetUserId());

            TempData["Success"] = $"Student {student.StudentNumber} — {student.FullName} added successfully.";
            return RedirectToAction(nameof(Detail), new { studentNumber = student.StudentNumber });
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate student number — surface as a friendly validation error
            ModelState.AddModelError(nameof(vm.StudentNumber), ex.Message);
            return View(vm);
        }
    }

    /// <summary>
    /// GET /Student/Detail/{studentNumber}
    /// Shows the student record with a list of their uploaded files.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(string studentNumber)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
            return BadRequest();

        var vm = await _studentService.GetDetailAsync(studentNumber);

        if (vm == null)
        {
            _logger.LogWarning(
                "Student detail requested for unknown student: {StudentNumber}", studentNumber);
            return NotFound();
        }

        await _auditService.LogAsync(
            userId:     User.GetUserId(),
            userEmail:  User.GetEmail(),
            action:     AuditActions.FileViewed,
            entityType: "Student",
            entityId:   studentNumber,
            ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");

        return View(vm);
    }
}

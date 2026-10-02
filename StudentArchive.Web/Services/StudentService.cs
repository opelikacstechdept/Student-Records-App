using Microsoft.EntityFrameworkCore;
using StudentArchive.Web.Data;
using StudentArchive.Web.Models;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Services;

public interface IStudentService
{
    Task<Student> CreateAsync(AddStudentViewModel vm, string userId);
    Task<Student?> GetByStudentNumberAsync(string studentNumber);
    Task<Student?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(string studentNumber);
    Task<StudentDetailViewModel?> GetDetailAsync(string studentNumber);
}

public class StudentService : IStudentService
{
    private readonly StudentArchiveDbContext _context;

    public StudentService(StudentArchiveDbContext context)
    {
        _context = context;
    }

    public async Task<Student> CreateAsync(AddStudentViewModel vm, string userId)
    {
        if (await ExistsAsync(vm.StudentNumber))
            throw new InvalidOperationException(
                $"Student number {vm.StudentNumber} already exists.");

        var student = new Student
        {
            StudentNumber = vm.StudentNumber.Trim(),
            FirstName = vm.FirstName.Trim(),
            LastName = vm.LastName.Trim(),
            Status = vm.Status,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();
        return student;
    }

    public async Task<Student?> GetByStudentNumberAsync(string studentNumber) =>
        await _context.Students
            .Include(s => s.Files)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);

    public async Task<Student?> GetByIdAsync(int id) =>
        await _context.Students
            .Include(s => s.Files)
            .FirstOrDefaultAsync(s => s.StudentId == id);

    public async Task<bool> ExistsAsync(string studentNumber) =>
        await _context.Students.AnyAsync(s => s.StudentNumber == studentNumber);

    public async Task<StudentDetailViewModel?> GetDetailAsync(string studentNumber)
    {
        var student = await _context.Students
            .Include(s => s.Files)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);

        if (student == null) return null;

        return new StudentDetailViewModel
        {
            StudentId = student.StudentId,
            StudentNumber = student.StudentNumber,
            FullName = student.FullName,
            Status = student.Status,
            CreatedAt = student.CreatedAt,
            Files = student.Files.OrderByDescending(f => f.UploadedAt)
                .Select(f => new FileListItemViewModel
                {
                    FileId = f.FileId,
                    FileName = f.FileName,
                    DocumentType = f.DocumentType,
                    AcademicYear = f.AcademicYear,
                    FileSizeDisplay = f.FileSizeDisplay,
                    UploadedAt = f.UploadedAt,
                    UploadedByUserName = f.UploadedByUserName
                }).ToList()
        };
    }
}

using StudentArchive.Web.Models;

namespace StudentArchive.Web.Data;

/// <summary>
/// Seeds the in-memory database with realistic sample data for demo mode.
/// Only called when running under the Demo or Development environment.
/// Never referenced from production code paths.
/// </summary>
public static class DemoDataSeeder
{
    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates the database with sample students, files, and metadata.
    /// Idempotent — skips seeding if data already exists.
    /// </summary>
    public static async Task SeedAsync(StudentArchiveDbContext db, ILogger logger)
    {
        if (db.Students.Any())
        {
            logger.LogInformation("Demo data already seeded — skipping");
            return;
        }

        // -------------------------------------------------------------------------
        // Students
        // -------------------------------------------------------------------------

        var students = new List<Student>
        {
            new() { StudentNumber = "1001001", FirstName = "Alice",   LastName = "Johnson",   Status = "Active",    CreatedAt = DateTime.UtcNow.AddDays(-120), CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001002", FirstName = "Bob",     LastName = "Martinez",  Status = "Active",    CreatedAt = DateTime.UtcNow.AddDays(-90),  CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001003", FirstName = "Carol",   LastName = "Thompson",  Status = "Graduated", CreatedAt = DateTime.UtcNow.AddDays(-400), CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001004", FirstName = "David",   LastName = "Lee",       Status = "Active",    CreatedAt = DateTime.UtcNow.AddDays(-60),  CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001005", FirstName = "Emma",    LastName = "Wilson",    Status = "Inactive",  CreatedAt = DateTime.UtcNow.AddDays(-200), CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001006", FirstName = "Frank",   LastName = "Brown",     Status = "Active",    CreatedAt = DateTime.UtcNow.AddDays(-30),  CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001007", FirstName = "Grace",   LastName = "Davis",     Status = "Withdrawn", CreatedAt = DateTime.UtcNow.AddDays(-180), CreatedByUserId = "demo-admin" },
            new() { StudentNumber = "1001008", FirstName = "Henry",   LastName = "Garcia",    Status = "Active",    CreatedAt = DateTime.UtcNow.AddDays(-15),  CreatedByUserId = "demo-admin" },
        };

        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------------------
        // Files — attached to the first three students
        // -------------------------------------------------------------------------

        var files = new List<StudentFile>
        {
            // Alice Johnson — 3 files
            new()
            {
                StudentId           = students[0].StudentId,
                BlobPath            = "1001001/1001001_20240901_090000_transcript.pdf",
                BlobUrl             = "/demo-files/1001001/transcript.pdf",
                FileName            = "transcript.pdf",
                ContentType         = "application/pdf",
                DocumentType        = "Transcript",
                AcademicYear        = "2023-2024",
                FileSizeBytes       = 245_760,
                UploadedAt          = DateTime.UtcNow.AddDays(-30),
                UploadedByUserId    = "demo-uploader",
                UploadedByUserName  = "Demo Uploader"
            },
            new()
            {
                StudentId           = students[0].StudentId,
                BlobPath            = "1001001/1001001_20240902_100000_enrollment_letter.pdf",
                BlobUrl             = "/demo-files/1001001/enrollment_letter.pdf",
                FileName            = "enrollment_letter.pdf",
                ContentType         = "application/pdf",
                DocumentType        = "Enrollment Letter",
                AcademicYear        = "2024-2025",
                FileSizeBytes       = 98_304,
                UploadedAt          = DateTime.UtcNow.AddDays(-20),
                UploadedByUserId    = "demo-uploader",
                UploadedByUserName  = "Demo Uploader"
            },
            new()
            {
                StudentId           = students[0].StudentId,
                BlobPath            = "1001001/1001001_20240903_110000_financial_aid.docx",
                BlobUrl             = "/demo-files/1001001/financial_aid.docx",
                FileName            = "financial_aid.docx",
                ContentType         = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DocumentType        = "Financial Aid",
                AcademicYear        = "2024-2025",
                FileSizeBytes       = 512_000,
                UploadedAt          = DateTime.UtcNow.AddDays(-10),
                UploadedByUserId    = "demo-admin",
                UploadedByUserName  = "Demo Admin"
            },

            // Bob Martinez — 2 files
            new()
            {
                StudentId           = students[1].StudentId,
                BlobPath            = "1001002/1001002_20240901_090000_transcript.pdf",
                BlobUrl             = "/demo-files/1001002/transcript.pdf",
                FileName            = "transcript.pdf",
                ContentType         = "application/pdf",
                DocumentType        = "Transcript",
                AcademicYear        = "2023-2024",
                FileSizeBytes       = 189_440,
                UploadedAt          = DateTime.UtcNow.AddDays(-45),
                UploadedByUserId    = "demo-uploader",
                UploadedByUserName  = "Demo Uploader"
            },
            new()
            {
                StudentId           = students[1].StudentId,
                BlobPath            = "1001002/1001002_20240902_120000_id_scan.jpg",
                BlobUrl             = "/demo-files/1001002/id_scan.jpg",
                FileName            = "id_scan.jpg",
                ContentType         = "image/jpeg",
                DocumentType        = "ID Scan",
                AcademicYear        = "2024-2025",
                FileSizeBytes       = 1_048_576,
                UploadedAt          = DateTime.UtcNow.AddDays(-40),
                UploadedByUserId    = "demo-admin",
                UploadedByUserName  = "Demo Admin"
            },

            // Carol Thompson — 1 file
            new()
            {
                StudentId           = students[2].StudentId,
                BlobPath            = "1001003/1001003_20230601_090000_graduation_cert.pdf",
                BlobUrl             = "/demo-files/1001003/graduation_cert.pdf",
                FileName            = "graduation_cert.pdf",
                ContentType         = "application/pdf",
                DocumentType        = "Transcript",
                AcademicYear        = "2022-2023",
                FileSizeBytes       = 320_000,
                UploadedAt          = DateTime.UtcNow.AddDays(-300),
                UploadedByUserId    = "demo-admin",
                UploadedByUserName  = "Demo Admin"
            },
        };

        db.StudentFiles.AddRange(files);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------------------
        // Flexible metadata rows
        // -------------------------------------------------------------------------

        var metadata = new List<FileMetadataItem>
        {
            new() { FileId = files[0].FileId, Key = "Semester",   Value = "Fall"   },
            new() { FileId = files[0].FileId, Key = "Department", Value = "Engineering" },
            new() { FileId = files[1].FileId, Key = "Semester",   Value = "Spring" },
            new() { FileId = files[3].FileId, Key = "Semester",   Value = "Fall"   },
            new() { FileId = files[3].FileId, Key = "Department", Value = "Business" },
        };

        db.FileMetadata.AddRange(metadata);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Demo data seeded: {StudentCount} students, {FileCount} files",
            students.Count, files.Count);
    }
}

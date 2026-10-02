using Microsoft.EntityFrameworkCore;
using StudentArchive.Web.Models;

namespace StudentArchive.Web.Data;

public class StudentArchiveDbContext : DbContext
{
    public StudentArchiveDbContext(DbContextOptions<StudentArchiveDbContext> options)
        : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentFile> StudentFiles => Set<StudentFile>();
    public DbSet<FileMetadataItem> FileMetadata => Set<FileMetadataItem>();
    public DbSet<MetadataKey> MetadataKeys => Set<MetadataKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Student indexes
        modelBuilder.Entity<Student>(e =>
        {
            e.HasIndex(s => s.StudentNumber).IsUnique();
            e.HasIndex(s => new { s.LastName, s.FirstName });
            e.HasIndex(s => s.Status);
        });

        // StudentFile indexes
        modelBuilder.Entity<StudentFile>(e =>
        {
            // FileId doesn't match EF's Id / StudentFileId convention
            e.HasKey(f => f.FileId);
            e.HasIndex(f => f.StudentId);
            e.HasIndex(f => f.DocumentType);
            e.HasIndex(f => f.AcademicYear);
            e.HasIndex(f => f.UploadedAt);
            e.HasOne(f => f.Student)
             .WithMany(s => s.Files)
             .HasForeignKey(f => f.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // FileMetadata indexes
        modelBuilder.Entity<FileMetadataItem>(e =>
        {
            // MetadataId doesn't match EF's Id / FileMetadataItemId convention
            e.HasKey(m => m.MetadataId);
            e.HasIndex(m => new { m.Key, m.Value });
            e.HasIndex(m => m.FileId);
            e.HasOne(m => m.StudentFile)
             .WithMany(f => f.Metadata)
             .HasForeignKey(m => m.FileId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog indexes
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.Action);
        });

        // Seed metadata keys
        modelBuilder.Entity<MetadataKey>().HasData(
            new MetadataKey { MetadataKeyId = 1, Key = "DocumentType", DisplayName = "Document Type", SortOrder = 1 },
            new MetadataKey { MetadataKeyId = 2, Key = "AcademicYear", DisplayName = "Academic Year", SortOrder = 2 },
            new MetadataKey { MetadataKeyId = 3, Key = "Semester", DisplayName = "Semester", SortOrder = 3 },
            new MetadataKey { MetadataKeyId = 4, Key = "Department", DisplayName = "Department", SortOrder = 4 },
            new MetadataKey { MetadataKeyId = 5, Key = "SubmittedBy", DisplayName = "Submitted By", SortOrder = 5 }
        );
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentArchive.Web.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling at design time.
/// Without it, the tools would run Program.cs, which in production mode needs
/// Entra, Key Vault and Blob configuration just to build the host.
/// </summary>
/// <remarks>
/// Generating a migration never connects to the database, so a placeholder
/// connection string is fine. To run <c>dotnet ef database update</c> against a
/// real server, set the <c>ConnectionStrings__DefaultConnection</c> environment variable.
/// </remarks>
public class StudentArchiveDbContextFactory : IDesignTimeDbContextFactory<StudentArchiveDbContext>
{
    public StudentArchiveDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=StudentArchive;Trusted_Connection=True;";

        var options = new DbContextOptionsBuilder<StudentArchiveDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new StudentArchiveDbContext(options);
    }
}

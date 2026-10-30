using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OneCover.Enrollment.Api.Data;

public sealed class EnrollmentDbContextFactory : IDesignTimeDbContextFactory<EnrollmentDbContext>
{
    public EnrollmentDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<EnrollmentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("EnrollmentDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=OneCoverEnrollmentDb;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new EnrollmentDbContext(options);
    }
}

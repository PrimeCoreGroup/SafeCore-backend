using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SafeCore.Shared.Data;

public sealed class SafeCoreDbContextFactory : IDesignTimeDbContextFactory<SafeCoreDbContext>
{
    public SafeCoreDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json"))
            && File.Exists(Path.Combine(basePath, "SafeCore", "appsettings.json")))
            basePath = Path.Combine(basePath, "SafeCore");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<SafeCoreDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection in User Secrets or the environment before running EF database commands.");
        var options = new DbContextOptionsBuilder<SafeCoreDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new SafeCoreDbContext(options);
    }
}

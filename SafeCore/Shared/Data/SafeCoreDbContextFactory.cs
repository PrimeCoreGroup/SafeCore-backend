using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SafeCore.Shared.Data;

public sealed class SafeCoreDbContextFactory : IDesignTimeDbContextFactory<SafeCoreDbContext>
{
    public SafeCoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SafeCoreDbContext>()
            .UseMySQL("Server=localhost;Database=safecore_design_time;User ID=design;Password=design-only;")
            .Options;
        return new SafeCoreDbContext(options);
    }
}

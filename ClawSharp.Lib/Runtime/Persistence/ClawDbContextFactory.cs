using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClawSharp.Lib.Runtime;

internal sealed class ClawDbContextFactory : IDesignTimeDbContextFactory<ClawDbContext>
{
    public ClawDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClawDbContext>();
        // 指向一个临时或默认路径，仅用于迁移工具分析
        optionsBuilder.UseSqlite("Data Source=design_time.db");

        return new ClawDbContext(optionsBuilder.Options);
    }
}

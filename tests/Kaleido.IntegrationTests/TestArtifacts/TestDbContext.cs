using Microsoft.EntityFrameworkCore;

namespace Kaleido.IntegrationTests.TestArtifacts;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities { get; set; }
}

public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

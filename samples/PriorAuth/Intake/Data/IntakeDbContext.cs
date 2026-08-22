using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Data;

public sealed class IntakeDbContext(
    DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
}

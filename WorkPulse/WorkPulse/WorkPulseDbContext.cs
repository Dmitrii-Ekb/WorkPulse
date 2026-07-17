using Microsoft.EntityFrameworkCore;

namespace WorkPulse;

public class WorkPulseDbContext : DbContext
{
    public WorkPulseDbContext(
        DbContextOptions<WorkPulseDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<WorkTask> Tasks { get; set; } = null!;
}
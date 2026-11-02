using Microsoft.EntityFrameworkCore;
using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Data;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(item => item.Email).IsUnique();
            user.Property(item => item.Email).HasMaxLength(256).IsRequired();
            user.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
            user.Property(item => item.Role).HasConversion<string>().HasMaxLength(20);
            user.Property(item => item.EmploymentType).HasConversion<string>().HasMaxLength(20);
            user.Property(item => item.FirstName).HasMaxLength(100).IsRequired();
            user.Property(item => item.LastName).HasMaxLength(100).IsRequired();
            user.Property(item => item.AnnualSalary).HasPrecision(18, 2);
            user.Property(item => item.SsnLast4).HasMaxLength(4).IsFixedLength().IsRequired();
        });
    }
}

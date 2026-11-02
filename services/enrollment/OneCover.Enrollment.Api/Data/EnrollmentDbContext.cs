using Microsoft.EntityFrameworkCore;
using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Data;

public sealed class EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options) : DbContext(options)
{
    private static readonly CoverageTier[] AllTiers = Enum.GetValues<CoverageTier>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanRate> PlanRates => Set<PlanRate>();
    public DbSet<CoverageEnrollment> Enrollments => Set<CoverageEnrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(plan =>
        {
            plan.Property(item => item.Name).HasMaxLength(100).IsRequired();
            plan.Property(item => item.Type).HasConversion<string>().HasMaxLength(30);
            plan.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            plan.Property(item => item.BenefitPercent).HasPrecision(5, 4);
            plan.Property(item => item.WeeklyMaximum).HasPrecision(18, 2);
            plan.HasMany(item => item.Rates).WithOne(item => item.Plan)
                .HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanRate>(rate =>
        {
            rate.HasKey(item => new { item.PlanId, item.Tier });
            rate.Property(item => item.Tier).HasConversion<string>().HasMaxLength(30);
            rate.Property(item => item.MonthlyPremium).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CoverageEnrollment>(enrollment =>
        {
            enrollment.Property(item => item.Tier).HasConversion<string>().HasMaxLength(30);
            enrollment.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            enrollment.Property(item => item.MonthlyPremiumSnapshot).HasPrecision(18, 2);
            enrollment.HasOne(item => item.Plan).WithMany().HasForeignKey(item => item.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            enrollment.HasIndex(item => new { item.MemberId, item.PlanId })
                .IsUnique().HasFilter("[Status] = 'Active'");
        });

        var planTypes = new[]
        {
            PlanType.STD, PlanType.LTD, PlanType.Life, PlanType.Accident,
            PlanType.Dental, PlanType.Vision, PlanType.CriticalIllness, PlanType.Pet
        };
        var plans = planTypes.Select((type, index) => new Plan
        {
            Id = index + 1,
            Name = type switch
            {
                PlanType.STD => "Short-Term Disability",
                PlanType.LTD => "Long-Term Disability",
                PlanType.CriticalIllness => "Critical Illness",
                _ => type.ToString()
            },
            Type = type,
            Description = type switch
            {
                PlanType.STD => "Replaces part of your income during a short-term illness or injury.",
                PlanType.LTD => "Replaces part of your income during a long-term illness or injury.",
                _ => $"Sample {type} coverage for eligible members."
            },
            BenefitPercent = type is PlanType.STD or PlanType.LTD ? 0.60m : 0m,
            WeeklyMaximum = type switch
            {
                PlanType.STD => 1500m,
                PlanType.LTD => 2300m,
                _ => 0m
            },
            EliminationDays = type switch
            {
                PlanType.STD => 7,
                PlanType.LTD => 90,
                _ => 0
            },
            IsActive = true
        }).ToArray();
        modelBuilder.Entity<Plan>().HasData(plans);

        var basePremiums = new decimal[] { 35m, 25m, 10m, 15m, 30m, 12m, 18m, 8m };
        var tierMultipliers = new decimal[] { 1m, 1.7m, 1.5m, 2.2m };
        var rates = new List<PlanRate>();
        for (var planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            var availableTiers = plans[planIndex].IsDisability
                ? new[] { CoverageTier.EmployeeOnly }
                : AllTiers;
            foreach (var tier in availableTiers)
            {
                var tierIndex = Array.IndexOf(AllTiers, tier);
                rates.Add(new PlanRate
                {
                    PlanId = plans[planIndex].Id,
                    Tier = tier,
                    MonthlyPremium = decimal.Round(basePremiums[planIndex] * tierMultipliers[tierIndex], 2)
                });
            }
        }

        modelBuilder.Entity<PlanRate>().HasData(rates);
    }
}

using Microsoft.EntityFrameworkCore;
using PetalPal.Sample.Api.Models;
using SqlOS.AuthServer.Interfaces;
using SqlOS.Extensions;
using SqlOS.Fga.Interfaces;
using SqlOS.Fga.Models;

namespace PetalPal.Sample.Api.Data;

public sealed class PetalPalDbContext(DbContextOptions<PetalPalDbContext> options)
    : DbContext(options), ISqlOSAuthServerDbContext, ISqlOSFgaDbContext
{
    public DbSet<Garden> Gardens => Set<Garden>();
    public DbSet<Plant> Plants => Set<Plant>();

    public IQueryable<SqlOSFgaAccessibleResource> IsResourceAccessible(
        string resourceId,
        string subjectIds,
        string permissionId)
        => FromExpression(() => IsResourceAccessible(resourceId, subjectIds, permissionId));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseSqlOS(GetType());

        modelBuilder.Entity<Garden>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.OwnerSubjectId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.ResourceId).IsUnique();
            entity.HasIndex(x => x.OwnerSubjectId).IsUnique();
        });

        modelBuilder.Entity<Plant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Mood).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.ResourceId).IsUnique();
            entity.HasIndex(x => new { x.GardenId, x.CreatedAt });
            entity.HasOne(x => x.Garden)
                .WithMany(x => x.Plants)
                .HasForeignKey(x => x.GardenId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

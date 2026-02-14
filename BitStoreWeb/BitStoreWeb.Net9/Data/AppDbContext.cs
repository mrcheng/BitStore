using BitStoreWeb.Net9.Models;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Bucket> Buckets => Set<Bucket>();
    public DbSet<BucketRecord> BucketRecords => Set<BucketRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
        });

        modelBuilder.Entity<Bucket>(entity =>
        {
            entity.ToTable("Buckets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            entity.Property(x => x.WriteApiKey).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.OwnerUserId, x.Name }).IsUnique();
            entity.HasOne(x => x.OwnerUser)
                .WithMany(x => x.Buckets)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BucketRecord>(entity =>
        {
            entity.ToTable("BucketRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).HasMaxLength(8);
            entity.HasIndex(x => new { x.BucketId, x.CreatedUtc });
            entity.HasOne(x => x.Bucket)
                .WithMany(x => x.Records)
                .HasForeignKey(x => x.BucketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

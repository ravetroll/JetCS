using System;
using System.Collections.Generic;
using JetCS.Domain;
using Microsoft.EntityFrameworkCore;

namespace JetCS.Persistence;

public partial class JetCSDbContext : DbContext
{
    public JetCSDbContext(DbContextOptions<JetCSDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Database> Databases { get; set; }

    public virtual DbSet<DatabaseLogin> DatabaseLogins { get; set; }

    public virtual DbSet<Login> Logins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Database>(entity =>
        {
            entity.Property(e => e.DatabaseId).HasColumnType("counter");
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<DatabaseLogin>(entity =>
        {
            entity.HasKey(e => e.DatabaseLoginId).HasName("PrimaryKey");

            entity.HasIndex(e => new { e.DatabaseId, e.LoginId }, "DatabaseLogin").IsUnique();

            entity.Property(e => e.DatabaseLoginId).HasColumnType("counter");

            entity.HasOne(d => d.Database).WithMany(p => p.DatabaseLogins)
                .HasForeignKey(d => d.DatabaseId)
                .HasConstraintName("DatabasesDatabaseLogins");

            entity.HasOne(d => d.Login).WithMany(p => p.DatabaseLogins)
                .HasForeignKey(d => d.LoginId)
                .HasConstraintName("LoginsDatabaseLogins");
        });

        modelBuilder.Entity<Login>(entity =>
        {
            entity.HasKey(e => e.LoginId).HasName("PrimaryKey");

            entity.HasIndex(e => e.LoginName, "uniqlogin").IsUnique();

            entity.Property(e => e.LoginId).HasColumnType("counter");
            entity.Property(e => e.Hash).HasMaxLength(255);
            entity.Property(e => e.IsAdmin)
                .HasDefaultValueSql("No")
                .HasColumnType("bit");
            entity.Property(e => e.Salt).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

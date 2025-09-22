using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KernelPlaygroundApp.Models.Keycloak;

public partial class KeycloakContext : DbContext, ITenantContext
{
    public KeycloakContext(DbContextOptions<KeycloakContext> options)
        : base(options)
    {
    }

    public virtual DbSet<FabricConfig> FabricConfigs { get; set; }

    public virtual DbSet<RealmClient> RealmClients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FabricConfig>(entity =>
        {
            entity.HasKey(e => new { e.RealmId, e.RealmClientId });

            entity.ToTable("FabricConfigs", "prophecy");

            entity.Property(e => e.RealmId)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.RealmClientId)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasDefaultValue("tenantapp", "DF_FabricConfigs_RealmClientName");
            entity.Property(e => e.ClientId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.ClientName)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.ClientSecret)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Location)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Resource)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.SubscriptionId)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.TenantId)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.WorkspaceId)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.WorkspaceName)
                .HasMaxLength(64)
                .IsUnicode(false);
        });

        modelBuilder.Entity<RealmClient>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("RealmClients", "prophecy");

            entity.Property(e => e.Authority)
                .HasMaxLength(307)
                .IsUnicode(false);
            entity.Property(e => e.RealmClientId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RealmClientSecret)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RealmId)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.RealmName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

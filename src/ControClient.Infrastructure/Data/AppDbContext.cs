using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Data;

/// <summary>
/// Contexto EF Core. Nombres en snake_case para que PostgreSQL y las consultas
/// manuales compartan el mismo vocabulario.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ClientGrant> Grants => Set<ClientGrant>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.ToTable("clients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(200);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_clients_name");
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EntraObjectId).HasColumnName("entra_object_id").HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(x => x.Role).HasColumnName("role").HasConversion<int>();
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.HasIndex(x => x.EntraObjectId).IsUnique().HasDatabaseName("ux_users_entra_object_id");
        });

        modelBuilder.Entity<ClientGrant>(e =>
        {
            e.ToTable("client_grants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Actions).HasColumnName("actions").HasConversion<int>();
            e.Property(x => x.GrantedAt).HasColumnName("granted_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.GrantedBy).HasColumnName("granted_by").HasMaxLength(200);
            e.HasIndex(x => new { x.UserId, x.ClientId }).IsUnique().HasDatabaseName("ux_client_grants_user_client");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_client_grants_user");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_client_grants_client");
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.ActorObjectId).HasColumnName("actor_object_id").HasMaxLength(100);
            e.Property(x => x.ActorName).HasColumnName("actor_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Entity).HasColumnName("entity").HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            e.Property(x => x.Result).HasColumnName("result").HasMaxLength(40).IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            e.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_audit_events_occurred_at");
            e.HasIndex(x => x.ActorObjectId).HasDatabaseName("ix_audit_events_actor");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_audit_events_client");
        });
    }
}


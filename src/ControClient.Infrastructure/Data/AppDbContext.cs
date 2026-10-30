using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Documents;
using ControClient.Domain.Forms;
using ControClient.Domain.Groups;
using ControClient.Domain.Users;
using ControClient.Domain.Vault;
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
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupGrant> GroupGrants => Set<GroupGrant>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<RecordInstance> Records => Set<RecordInstance>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<VaultItem> VaultItems => Set<VaultItem>();
    public DbSet<VaultVersion> VaultVersions => Set<VaultVersion>();
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

            e.Property(x => x.TaxId).HasColumnName("tax_id").HasMaxLength(30);
            e.Property(x => x.Address).HasColumnName("address").HasMaxLength(300);
            e.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
            e.Property(x => x.City).HasColumnName("city").HasMaxLength(120);
            e.Property(x => x.Province).HasColumnName("province").HasMaxLength(120);
            e.Property(x => x.Country).HasColumnName("country").HasMaxLength(120);
            e.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(200);
            e.Property(x => x.ContactRole).HasColumnName("contact_role").HasMaxLength(120);
            e.Property(x => x.ContactEmail).HasColumnName("contact_email").HasMaxLength(200);
            e.Property(x => x.ContactPhone).HasColumnName("contact_phone").HasMaxLength(40);
            e.Property(x => x.ContactMobile).HasColumnName("contact_mobile").HasMaxLength(40);
            e.Property(x => x.Website).HasColumnName("website").HasMaxLength(200);
            e.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);

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

        modelBuilder.Entity<Group>(e =>
        {
            e.ToTable("groups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_groups_name");

            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_group_members_group");
            e.HasMany(x => x.Grants).WithOne().HasForeignKey(g => g.GroupId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_group_grants_group");
        });

        modelBuilder.Entity<GroupMember>(e =>
        {
            e.ToTable("group_members");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.GroupId).HasColumnName("group_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.AddedAt).HasColumnName("added_at");
            e.Property(x => x.AddedBy).HasColumnName("added_by").HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique().HasDatabaseName("ux_group_members_group_user");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_group_members_user");
        });

        modelBuilder.Entity<GroupGrant>(e =>
        {
            e.ToTable("group_grants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.GroupId).HasColumnName("group_id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Actions).HasColumnName("actions").HasConversion<int>();
            e.Property(x => x.GrantedAt).HasColumnName("granted_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.GrantedBy).HasColumnName("granted_by").HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.GroupId, x.ClientId }).IsUnique().HasDatabaseName("ux_group_grants_group_client");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_group_grants_client");
        });

        modelBuilder.Entity<FormTemplate>(e =>
        {
            e.ToTable("form_templates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.Key, x.Version }).IsUnique().HasDatabaseName("ux_form_templates_key_version");

            e.OwnsMany(t => t.Fields, b =>
            {
                b.ToTable("field_definitions");
                b.WithOwner().HasForeignKey("template_id");
                b.HasKey(f => f.Id);
                b.Property(f => f.Id).HasColumnName("id");
                b.Property(f => f.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
                b.Property(f => f.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
                b.Property(f => f.Type).HasColumnName("type").HasConversion<int>();
                b.Property(f => f.IsRequired).HasColumnName("is_required");
                b.Property(f => f.Order).HasColumnName("order_index");
                b.Property(f => f.OptionsJson).HasColumnName("options_json");
                b.Property(f => f.HelpText).HasColumnName("help_text").HasMaxLength(500);
                b.HasIndex("template_id").HasDatabaseName("ix_field_definitions_template");
            });
        });

        modelBuilder.Entity<RecordInstance>(e =>
        {
            e.ToTable("record_instances");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.TemplateId).HasColumnName("template_id");
            e.Property(x => x.TemplateVersion).HasColumnName("template_version");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.ValuesJson).HasColumnName("values_json").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Version).HasColumnName("row_version").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.ClientId).HasDatabaseName("ix_record_instances_client");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_record_instances_client");
            e.HasOne<FormTemplate>().WithMany().HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_record_instances_template");
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.Classification).HasColumnName("classification").HasConversion<int>();
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.ClientId).HasDatabaseName("ix_documents_client");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_documents_client");

            e.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.DocumentId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_document_versions_document");
        });

        modelBuilder.Entity<DocumentVersion>(e =>
        {
            e.ToTable("document_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(300).IsRequired();
            e.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(200).IsRequired();
            e.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            e.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
            e.Property(x => x.StoragePath).HasColumnName("storage_path").HasMaxLength(300).IsRequired();
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.Property(x => x.UploadedBy).HasColumnName("uploaded_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.ScanStatus).HasColumnName("scan_status").HasConversion<int>();
            e.HasIndex(x => new { x.DocumentId, x.Version }).IsUnique().HasDatabaseName("ux_document_versions_document_version");
            e.HasIndex(x => x.Sha256).HasDatabaseName("ix_document_versions_sha256");
        });

        modelBuilder.Entity<VaultItem>(e =>
        {
            e.ToTable("vault_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<int>();
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(200);
            e.Property(x => x.Url).HasColumnName("url").HasMaxLength(500);
            e.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsArchived).HasColumnName("is_archived");
            e.HasIndex(x => x.ClientId).HasDatabaseName("ix_vault_items_client");
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_vault_items_client");
            e.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.ItemId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_vault_versions_item");
        });

        modelBuilder.Entity<VaultVersion>(e =>
        {
            e.ToTable("vault_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.Nonce).HasColumnName("nonce").HasColumnType("bytea").IsRequired();
            e.Property(x => x.Ciphertext).HasColumnName("ciphertext").HasColumnType("bytea").IsRequired();
            e.Property(x => x.DekNonce).HasColumnName("dek_nonce").HasColumnType("bytea").IsRequired();
            e.Property(x => x.WrappedDek).HasColumnName("wrapped_dek").HasColumnType("bytea").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(500);
            e.HasIndex(x => new { x.ItemId, x.Version }).IsUnique().HasDatabaseName("ux_vault_versions_item_version");
        });
    }
}


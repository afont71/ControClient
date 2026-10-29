using ControClient.Domain.Documents;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Infrastructure.Documents;

/// <summary>
/// Alta, versiones y descarga de documentos. Limita tamaño y bloquea ejecutables;
/// el contenido se guarda en disco y la base solo referencia ruta y hash.
/// </summary>
public sealed class DocumentService(AppDbContext db, DocumentStorage storage, IConfiguration configuration)
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".scr", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jar", ".lnk", ".iso"
    };

    private long MaxBytes => configuration.GetValue("Documents:MaxBytes", 50L * 1024 * 1024);

    public sealed record DocumentSummary(Guid Id, Guid ClientId, string Name, DocumentClassification Classification, int CurrentVersion, int VersionCount, long SizeBytes, string? LastFileName, DateTimeOffset UpdatedAt, string CreatedBy);
    public sealed record VersionInfo(Guid Id, int Version, string FileName, string ContentType, long SizeBytes, string Sha256, string ScanStatus, DateTimeOffset UploadedAt, string UploadedBy);
    public sealed record DocumentDetail(Guid Id, Guid ClientId, string Name, string? Description, DocumentClassification Classification, DateTimeOffset CreatedAt, string CreatedBy, IReadOnlyList<VersionInfo> Versions);

    public async Task<(Document? Document, VersionInfo? Version, string? Error)> CreateAsync(
        Guid clientId,
        string name,
        DocumentClassification classification,
        string fileName,
        string? contentType,
        Stream content,
        long declaredSize,
        string uploadedBy,
        CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return (null, null, "El cliente no existe.");

        var validation = Validate(fileName, declaredSize);
        if (validation is not null)
            return (null, null, validation);

        var (relative, sha, size) = await storage.SaveAsync(content, ct);
        if (size > MaxBytes)
        {
            storage.Delete(relative);
            return (null, null, $"El fichero supera el máximo permitido ({MaxBytes / (1024 * 1024)} MB).");
        }

        try
        {
            var document = new Document(clientId, name, classification, uploadedBy);
            var version = document.AddVersion(fileName, contentType ?? "application/octet-stream", size, sha, relative, uploadedBy);

            db.Documents.Add(document);
            await db.SaveChangesAsync(ct);

            return (document, ToInfo(version), null);
        }
        catch
        {
            storage.Delete(relative); // no dejar huérfanos si falla la base
            throw;
        }
    }

    public async Task<(VersionInfo? Version, string? Error)> AddVersionAsync(
        Guid documentId,
        string fileName,
        string? contentType,
        Stream content,
        long declaredSize,
        string uploadedBy,
        CancellationToken ct = default)
    {
        var document = await db.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
            return (null, "El documento no existe.");

        var validation = Validate(fileName, declaredSize);
        if (validation is not null)
            return (null, validation);

        var (relative, sha, size) = await storage.SaveAsync(content, ct);
        if (size > MaxBytes)
        {
            storage.Delete(relative);
            return (null, $"El fichero supera el máximo permitido ({MaxBytes / (1024 * 1024)} MB).");
        }

        try
        {
            var version = document.AddVersion(fileName, contentType ?? "application/octet-stream", size, sha, relative, uploadedBy);
            await db.SaveChangesAsync(ct);
            return (ToInfo(version), null);
        }
        catch
        {
            storage.Delete(relative);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(Guid clientId, CancellationToken ct = default)
    {
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.ClientId == clientId)
            .Select(d => new
            {
                d.Id, d.ClientId, d.Name, d.Classification, d.UpdatedAt, d.CreatedAt, d.CreatedBy,
                Versions = d.Versions.OrderByDescending(v => v.Version).ToList()
            })
            .ToListAsync(ct);

        return rows.Select(d =>
        {
            var current = d.Versions.FirstOrDefault();
            return new DocumentSummary(
                d.Id, d.ClientId, d.Name, d.Classification,
                current?.Version ?? 0, d.Versions.Count, current?.SizeBytes ?? 0,
                current?.FileName, d.UpdatedAt ?? d.CreatedAt, d.CreatedBy);
        })
        .OrderBy(d => d.Name)
        .ToList();
    }

    public async Task<DocumentDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var document = await db.Documents.AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document is null)
            return null;

        return new DocumentDetail(
            document.Id, document.ClientId, document.Name, document.Description,
            document.Classification, document.CreatedAt, document.CreatedBy,
            document.Versions.OrderByDescending(v => v.Version).Select(ToInfo).ToList());
    }

    public async Task<(Stream? Content, string? FileName, string? ContentType, string? Error)> OpenAsync(
        Guid documentId, int? version, CancellationToken ct = default)
    {
        var query = db.Documents.AsNoTracking().Include(d => d.Versions).Where(d => d.Id == documentId);
        var document = await query.FirstOrDefaultAsync(ct);
        if (document is null)
            return (null, null, null, "El documento no existe.");

        var selected = version is null
            ? document.Versions.OrderByDescending(v => v.Version).FirstOrDefault()
            : document.Versions.FirstOrDefault(v => v.Version == version);

        if (selected is null)
            return (null, null, null, "La versión no existe.");

        if (!File.Exists(Path.Combine(storage.Root, selected.StoragePath)))
            return (null, null, null, "El contenido no está disponible en el almacén.");

        return (storage.OpenRead(selected.StoragePath), selected.FileName, selected.ContentType, null);
    }

    private string? Validate(string fileName, long declaredSize)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "El nombre del fichero es obligatorio.";

        var extension = Path.GetExtension(fileName);
        if (BlockedExtensions.Contains(extension))
            return $"No se admiten ficheros {extension}: guárdalos como adjunto fuera de la aplicación.";

        if (declaredSize > MaxBytes)
            return $"El fichero supera el máximo permitido ({MaxBytes / (1024 * 1024)} MB).";

        return null;
    }

    private static VersionInfo ToInfo(DocumentVersion v) =>
        new(v.Id, v.Version, v.FileName, v.ContentType, v.SizeBytes, v.Sha256, v.ScanStatus.ToString(), v.UploadedAt, v.UploadedBy);
}

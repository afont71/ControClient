namespace ControClient.Domain.Documents;

/// <summary>Clasificación del documento; condiciona la vista segura y el acceso.</summary>
public enum DocumentClassification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Sensitive = 3
}

/// <summary>Estado del análisis antimalware. En v1 no hay motor conectado.</summary>
public enum ScanStatus
{
    NotScanned = 0,
    Clean = 1,
    Quarantined = 2
}

/// <summary>
/// Documento de un cliente. El contenido vive fuera de la base de datos (§13.1);
/// aquí se guardan metadatos, versiones y hashes para integridad y procedencia.
/// </summary>
public sealed class Document
{

    private Document() { }

    public Document(Guid clientId, string name, DocumentClassification classification, string createdBy, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del documento es obligatorio.", nameof(name));

        ClientId = clientId;
        Name = name.Trim();
        Classification = classification;
        CreatedBy = createdBy;
        Description = description;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DocumentClassification Classification { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    public List<DocumentVersion> Versions { get; private set; } = [];

    public DocumentVersion AddVersion(
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string storagePath,
        string uploadedBy,
        ScanStatus scanStatus = ScanStatus.NotScanned)
    {
        var version = new DocumentVersion(
            Id, Versions.Count == 0 ? 1 : Versions.Max(v => v.Version) + 1,
            fileName, contentType, sizeBytes, sha256, storagePath, uploadedBy, scanStatus);

        Versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        return version;
    }

    public void Reclassify(DocumentClassification classification)
    {
        Classification = classification;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>Versión concreta de un documento. El hash identifica el contenido.</summary>
public sealed class DocumentVersion
{
    private DocumentVersion() { }

    internal DocumentVersion(
        Guid documentId,
        int version,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string storagePath,
        string uploadedBy,
        ScanStatus scanStatus)
    {
        DocumentId = documentId;
        Version = version;
        FileName = Path.GetFileName(fileName);
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        StoragePath = storagePath;
        UploadedBy = uploadedBy;
        ScanStatus = scanStatus;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DocumentId { get; private set; }
    public int Version { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = "application/octet-stream";
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Ruta relativa dentro del almacén; nunca una URL externa (MIG-02).</summary>
    public string StoragePath { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string UploadedBy { get; private set; } = string.Empty;
    public ScanStatus ScanStatus { get; private set; }
}

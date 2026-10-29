using System.Security.Cryptography;

namespace ControClient.Infrastructure.Storage;

/// <summary>
/// Almacén de ficheros en disco, con rutas derivadas del hash (ADR-003).
/// No usa nombres de usuario en la ruta y no sobrescribe: dos cargas con el mismo
/// contenido comparten fichero y se distinguen por sus versiones en la base.
/// </summary>
public sealed class DocumentStorage
{
    private readonly string _root;

    public DocumentStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    /// <summary>Guarda el contenido y devuelve ruta relativa, hash y tamaño.</summary>
    public async Task<(string RelativePath, string Sha256, long Size)> SaveAsync(Stream content, CancellationToken ct = default)
    {
        var temp = Path.Combine(_root, $".upload-{Guid.NewGuid():N}.tmp");
        string sha;

        try
        {
            await using (var file = File.Create(temp))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    hash.AppendData(buffer, 0, read);
                }

                sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            var relative = RelativePathFor(sha);
            var target = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // Contenido idéntico: ya existe, se descarta el temporal.
            if (File.Exists(target))
            {
                File.Delete(temp);
            }
            else
            {
                File.Move(temp, target);
            }

            return (relative, sha, new FileInfo(target).Length);
        }
        catch
        {
            if (File.Exists(temp)) File.Delete(temp);
            throw;
        }
    }

    public Stream OpenRead(string relativePath)
    {
        var full = Resolve(relativePath);
        return File.OpenRead(full);
    }

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full)) File.Delete(full);
    }

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de almacén fuera de la raíz permitida.");

        return full;
    }

    private static string RelativePathFor(string sha) =>
        Path.Combine(sha[..2], sha.Substring(2, 2), sha);
}

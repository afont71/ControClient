using System.Security.Cryptography;
using System.Text;
using ControClient.Infrastructure.Storage;

namespace ControClient.Tests;

public class DocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cc-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task Guarda_hash_y_ruta_derivada_del_hash()
    {
        var storage = new DocumentStorage(_root);
        var bytes = Encoding.UTF8.GetBytes("contenido de prueba");

        var (relative, sha, size) = await storage.SaveAsync(new MemoryStream(bytes));

        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expected, sha);
        Assert.Equal(bytes.Length, size);
        Assert.StartsWith(sha[..2] + Path.DirectorySeparatorChar, relative);
    }

    [Fact]
    public async Task Contenido_identico_no_duplica_fichero()
    {
        var storage = new DocumentStorage(_root);
        var bytes = Encoding.UTF8.GetBytes("mismo contenido");

        var first = await storage.SaveAsync(new MemoryStream(bytes));
        var second = await storage.SaveAsync(new MemoryStream(bytes));

        Assert.Equal(first.RelativePath, second.RelativePath);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Abre_y_elimina_el_contenido()
    {
        var storage = new DocumentStorage(_root);
        var bytes = Encoding.UTF8.GetBytes("leer y borrar");

        var (relative, _, _) = await storage.SaveAsync(new MemoryStream(bytes));

        using (var stream = storage.OpenRead(relative))
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("leer y borrar", await reader.ReadToEndAsync());
        }

        storage.Delete(relative);
        Assert.False(File.Exists(Path.Combine(_root, relative)));
    }

    [Fact]
    public void Rechaza_rutas_que_salen_de_la_raiz()
    {
        var storage = new DocumentStorage(_root);
        Assert.Throws<InvalidOperationException>(() => storage.OpenRead("..\\..\\windows\\win.ini"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

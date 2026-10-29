using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Tests;

public class UserProvisionerTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"prov-{Guid.NewGuid():N}")
            .Options);

    private static IConfiguration Config(string? bootstrap = null, bool requirePreRegistration = false) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminObjectId"] = bootstrap,
            ["Authentication:RequirePreRegistration"] = requirePreRegistration.ToString()
        }).Build();

    [Fact]
    public async Task En_modo_cerrado_una_cuenta_sin_preregistro_no_entra()
    {
        await using var db = Db();
        var provisioner = new UserProvisioner(db, Config(requirePreRegistration: true));

        await Assert.ThrowsAsync<UnprovisionedUserException>(
            () => provisioner.GetOrCreateAsync("oid-desconocido", "Intruso", "intruso@ideonetworks.com"));

        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task El_administrador_inicial_entra_aunque_el_modo_sea_cerrado()
    {
        await using var db = Db();
        var provisioner = new UserProvisioner(db, Config(bootstrap: "oid-admin", requirePreRegistration: true));

        var user = await provisioner.GetOrCreateAsync("oid-admin", "Jefa", "jefa@ideonetworks.com");

        Assert.Equal(AppRole.Administrator, user.Role);
    }

    [Fact]
    public async Task En_modo_cerrado_el_preregistrado_si_entra()
    {
        await using var db = Db();
        db.Users.Add(new AppUser("pending:ana@ideonetworks.com", "Ana", "ana@ideonetworks.com", AppRole.Technician));
        await db.SaveChangesAsync();

        var provisioner = new UserProvisioner(db, Config(requirePreRegistration: true));
        var user = await provisioner.GetOrCreateAsync("oid-ana", "Ana", "ana@ideonetworks.com");

        Assert.Equal(AppRole.Technician, user.Role);
        Assert.False(user.IsPending);
    }

    [Fact]
    public async Task Vincula_la_ficha_preregistrada_por_correo_y_conserva_el_rol()
    {
        await using var db = Db();

        // Pre-registro: pending + correo, con rol Technician ya decidido.
        db.Users.Add(new AppUser("pending:ana@ideonetworks.com", "Ana", "ana@ideonetworks.com", AppRole.Technician));
        await db.SaveChangesAsync();

        var provisioner = new UserProvisioner(db, Config());

        var user = await provisioner.GetOrCreateAsync("508b602b-1633-42dc-b693-688c9f0183f4", "Ana Font", "ANA@ideonetworks.com");

        Assert.Equal("508b602b-1633-42dc-b693-688c9f0183f4", user.EntraObjectId);
        Assert.Equal(AppRole.Technician, user.Role);
        Assert.True(user.IsActive);
        Assert.NotNull(user.LastLoginAt);

        // No debe haber creado una segunda fila.
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Sin_preregistro_crea_usuario_nuevo_como_lectura()
    {
        await using var db = Db();
        var provisioner = new UserProvisioner(db, Config());

        var user = await provisioner.GetOrCreateAsync("oid-desconocido", "Nuevo", "nuevo@ideonetworks.com");

        Assert.Equal(AppRole.ReadOnly, user.Role);
        Assert.False(user.IsPending);
    }

    [Fact]
    public async Task El_preregistro_no_se_reutiliza_para_otro_correo()
    {
        await using var db = Db();
        db.Users.Add(new AppUser("pending:ana@ideonetworks.com", "Ana", "ana@ideonetworks.com", AppRole.Technician));
        await db.SaveChangesAsync();

        var provisioner = new UserProvisioner(db, Config());
        var other = await provisioner.GetOrCreateAsync("oid-otro", "Otro", "otro@ideonetworks.com");

        Assert.Equal(AppRole.ReadOnly, other.Role);
        Assert.Equal(2, await db.Users.CountAsync());
    }
}

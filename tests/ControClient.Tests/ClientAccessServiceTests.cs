using ControClient.Domain.Clients;
using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Tests;

public class ClientAccessServiceTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"acc-{Guid.NewGuid():N}")
            .Options);

    private static readonly Guid ClientA = Guid.NewGuid();
    private static readonly Guid ClientB = Guid.NewGuid();

    private static AppUser User(AppRole role) => new($"oid-{Guid.NewGuid():N}", role.ToString(), null, role);

    [Fact]
    public async Task Administrador_ve_todos_y_edita_sin_concesion()
    {
        await using var db = Db();
        var admin = User(AppRole.Administrator);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.Null(await service.GetVisibleClientIdsAsync(admin));
        Assert.True(await service.CanViewAsync(admin, ClientA));
        Assert.True(await service.CanEditAsync(admin, ClientA));
    }

    [Fact]
    public async Task Tecnico_solo_ve_sus_clientes_concedidos()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        db.Users.Add(tech);
        db.Grants.Add(new ClientGrant(tech.Id, ClientA, GrantActions.View | GrantActions.Edit));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());
        var visible = await service.GetVisibleClientIdsAsync(tech);

        Assert.NotNull(visible);
        Assert.Contains(ClientA, visible!);
        Assert.DoesNotContain(ClientB, visible);
        Assert.True(await service.CanViewAsync(tech, ClientA));
        Assert.False(await service.CanViewAsync(tech, ClientB));
    }

    [Fact]
    public async Task Concesion_caducada_no_da_acceso()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        db.Users.Add(tech);
        db.Grants.Add(new ClientGrant(
            tech.Id, ClientA, GrantActions.View | GrantActions.Edit,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.False(await service.CanViewAsync(tech, ClientA));
    }

    [Fact]
    public async Task Ver_no_implica_editar()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        db.Users.Add(tech);
        db.Grants.Add(new ClientGrant(tech.Id, ClientA, GrantActions.View));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.True(await service.CanViewAsync(tech, ClientA));
        Assert.False(await service.CanEditAsync(tech, ClientA));
    }

    [Fact]
    public async Task Revelar_exige_rol_y_permiso_explicito()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        db.Users.Add(tech);
        db.Grants.Add(new ClientGrant(tech.Id, ClientA, GrantActions.View | GrantActions.Edit));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        // Ve y edita, pero no revela: falta RevealSecret en la concesión.
        Assert.False(await service.CanRevealAsync(tech, ClientA));
    }

    [Fact]
    public async Task Administrador_revela_si_tiene_concesion()
    {
        await using var db = Db();
        var admin = User(AppRole.Administrator);
        db.Users.Add(admin);
        db.Grants.Add(new ClientGrant(admin.Id, ClientA, GrantActions.View | GrantActions.Edit | GrantActions.RevealSecret));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.True(await service.CanRevealAsync(admin, ClientA));
    }

    [Fact]
    public async Task El_alta_del_usuario_aplica_rol_por_defecto_y_bootstrap()
    {
        await using var db = Db();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdminObjectId"] = "oid-admin",
                // Modo abierto: aquí se comprueba el rol por defecto, no el cierre.
                ["Authentication:RequirePreRegistration"] = "false"
            })
            .Build();

        var provisioner = new UserProvisioner(db, config);

        var admin = await provisioner.GetOrCreateAsync("oid-admin", "Jefa", null);
        var unknown = await provisioner.GetOrCreateAsync("oid-otro", "Técnico", null);

        Assert.Equal(AppRole.Administrator, admin.Role);
        Assert.Equal(AppRole.ReadOnly, unknown.Role);
    }
}

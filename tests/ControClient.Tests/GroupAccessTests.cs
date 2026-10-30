using ControClient.Domain.Clients;
using ControClient.Domain.Forms;
using ControClient.Domain.Groups;
using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Tests;

public class GroupAccessTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"groups-{Guid.NewGuid():N}")
            .Options);

    private static AppUser User(AppRole role) => new($"oid-{Guid.NewGuid():N}", role.ToString(), null, role);

    private static Client Client(string name) => new(name, null);

    [Fact]
    public async Task Un_miembro_hereda_el_cliente_del_grupo()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Bodegas");
        var group = new Group("Soporte", null, "admin");

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View | GrantActions.Edit, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        var visible = await service.GetVisibleClientIdsAsync(tech);
        Assert.NotNull(visible);
        Assert.Contains(client.Id, visible!);
        Assert.True(await service.CanViewAsync(tech, client.Id));
        Assert.True(await service.CanEditAsync(tech, client.Id));
    }

    [Fact]
    public async Task Salir_del_grupo_revoca_el_acceso()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Institut");
        var group = new Group("Redes", null, "admin");

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var member = new GroupMember(group.Id, tech.Id, "admin");
        db.GroupMembers.Add(member);
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());
        Assert.True(await service.CanViewAsync(tech, client.Id));

        db.GroupMembers.Remove(member);
        await db.SaveChangesAsync();

        Assert.False(await service.CanViewAsync(tech, client.Id));
    }

    [Fact]
    public async Task Un_grupo_inactivo_no_concede()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("TASA");
        var group = new Group("Antiguo", null, "admin");

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View, "admin"));
        await db.SaveChangesAsync();

        group.Deactivate();
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());
        Assert.False(await service.CanViewAsync(tech, client.Id));
    }

    [Fact]
    public async Task Las_acciones_se_suman_entre_concesion_directa_y_grupo()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Sambil");
        var group = new Group("Redes", null, "admin");

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        // Directa: solo ver. Grupo: ver + revelar. El resultado debe permitir revelar.
        db.Grants.Add(new ClientGrant(tech.Id, client.Id, GrantActions.View, "admin"));
        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View | GrantActions.RevealSecret, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());
        Assert.True(await service.CanRevealAsync(tech, client.Id));
        Assert.False(await service.CanEditAsync(tech, client.Id));
    }

    [Fact]
    public async Task Permite_editar_una_ficha_concreta_con_el_cliente_en_solo_lectura()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Bodegas");
        var group = new Group("Soporte", null, "admin");
        var template = new FormTemplate("licencia", "Licencia", null,
        [
            new FieldDefinition("producto", "Producto", FieldType.Text, required: true, order: 1)
        ], version: 1);

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync();

        var editable = new RecordInstance(client.Id, template, "Office", "{\"producto\":\"Office\"}", "admin");
        var otro = new RecordInstance(client.Id, template, "Acronis", "{\"producto\":\"Acronis\"}", "admin");
        db.Records.AddRange(editable, otro);

        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View, "admin"));
        db.RecordGrants.Add(new RecordGrant(group.Id, editable.Id, GrantActions.View | GrantActions.Edit, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.False(await service.CanEditAsync(tech, client.Id));
        Assert.True(await service.CanViewRecordAsync(tech, client.Id, editable.Id));
        Assert.True(await service.CanEditRecordAsync(tech, client.Id, editable.Id));
        Assert.False(await service.CanEditRecordAsync(tech, client.Id, otro.Id));
    }

    [Fact]
    public async Task Una_ficha_sin_ver_retira_el_acceso_aunque_el_cliente_lo_tenga()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Bodegas");
        var group = new Group("Soporte", null, "admin");
        var template = new FormTemplate("licencia", "Licencia", null,
        [
            new FieldDefinition("producto", "Producto", FieldType.Text, required: true, order: 1)
        ], version: 1);

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync();

        var oculta = new RecordInstance(client.Id, template, "Oculta", "{\"producto\":\"X\"}", "admin");
        db.Records.Add(oculta);

        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id,
            GrantActions.View | GrantActions.Edit, "admin"));
        db.RecordGrants.Add(new RecordGrant(group.Id, oculta.Id, GrantActions.None, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.True(await service.CanEditAsync(tech, client.Id));
        Assert.False(await service.CanViewRecordAsync(tech, client.Id, oculta.Id));
        Assert.False(await service.CanEditRecordAsync(tech, client.Id, oculta.Id));
    }

    [Fact]
    public async Task El_permiso_de_la_ficha_manda_al_revelar()
    {
        await using var db = Db();
        var tech = User(AppRole.Technician);
        var client = Client("Bodegas");
        var group = new Group("Soporte", null, "admin");
        var template = new FormTemplate("credencial", "Credencial", null,
        [
            new FieldDefinition("clave", "Clave", FieldType.Text, required: true, order: 1, isSecret: true)
        ], version: 1);

        db.Users.Add(tech);
        db.Clients.Add(client);
        db.Groups.Add(group);
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync();

        var conPermiso = new RecordInstance(client.Id, template, "A", "{}", "admin");
        var sinPermiso = new RecordInstance(client.Id, template, "B", "{}", "admin");
        db.Records.AddRange(conPermiso, sinPermiso);

        db.GroupMembers.Add(new GroupMember(group.Id, tech.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id,
            GrantActions.View | GrantActions.RevealSecret, "admin"));
        db.RecordGrants.Add(new RecordGrant(group.Id, conPermiso.Id,
            GrantActions.View | GrantActions.RevealSecret, "admin"));
        db.RecordGrants.Add(new RecordGrant(group.Id, sinPermiso.Id, GrantActions.None, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        Assert.True(await service.CanRevealRecordAsync(tech, client.Id, conPermiso.Id));
        Assert.False(await service.CanRevealRecordAsync(tech, client.Id, sinPermiso.Id));
    }

    [Fact]
    public async Task El_administrador_revela_si_la_concesion_lo_permite()
    {
        await using var db = Db();
        var admin = User(AppRole.Administrator);
        var client = Client("PG");
        var group = new Group("Redes", null, "admin");

        db.Users.Add(admin);
        db.Clients.Add(client);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        db.GroupMembers.Add(new GroupMember(group.Id, admin.Id, "admin"));
        db.GroupGrants.Add(new GroupGrant(group.Id, client.Id, GrantActions.View | GrantActions.Edit | GrantActions.RevealSecret, "admin"));
        await db.SaveChangesAsync();

        var service = new ClientAccessService(db, new ClientAccessPolicy());

        // Revelar depende de la concesión, no del rol: si el grupo la tiene, puede.
        Assert.True(await service.CanRevealAsync(admin, client.Id));
        Assert.True(await service.CanViewAsync(admin, client.Id));
    }
}

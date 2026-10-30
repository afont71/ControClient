using ControClient.Domain.Groups;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Grupo de sistema «Administradores»: acceso total y no editable. Sus miembros
/// se sincronizan con el rol Administrator.
/// </summary>
public static class SystemGroupSeeder
{
    public const string Name = "Administradores";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.IsSystem, ct);
        if (group is null)
        {
            group = new Group(Name, "Grupo de sistema con acceso total", "sistema", isSystem: true);
            db.Groups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var admins = await db.Users.Where(u => u.Role == AppRole.Administrator && u.IsActive).ToListAsync(ct);
        var members = await db.GroupMembers.Where(m => m.GroupId == group.Id).Select(m => m.UserId).ToListAsync(ct);

        foreach (var admin in admins.Where(a => !members.Contains(a.Id)))
            db.GroupMembers.Add(new GroupMember(group.Id, admin.Id, "sistema"));

        await db.SaveChangesAsync(ct);
    }
}
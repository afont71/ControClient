using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Crea el usuario en el primer inicio de sesión. El primer administrador se
/// define por configuración (Authentication:BootstrapAdminObjectId); el resto
/// entra como solo lectura hasta que se le asigne rol (denegación por defecto).
/// </summary>
public sealed class UserProvisioner(AppDbContext db, IConfiguration configuration) : IUserProvisioner
{
    public async Task<AppUser> GetOrCreateAsync(
        string objectId,
        string displayName,
        string? email,
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == objectId, ct);

        if (user is null)
        {
            var bootstrapAdmin = configuration["Authentication:BootstrapAdminObjectId"];
            var defaultRole = string.Equals(bootstrapAdmin, objectId, StringComparison.Ordinal)
                ? AppRole.Administrator
                : AppRole.ReadOnly;

            user = new AppUser(objectId, displayName, email, defaultRole);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return user;
        }

        user.UpdateProfile(displayName, email);

        var shouldSave = db.Entry(user).Properties.Any(p => p.IsModified);
        if (user.LastLoginAt is null || user.LastLoginAt < DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            user.RegisterLogin();
            shouldSave = true;
        }

        if (shouldSave)
            await db.SaveChangesAsync(ct);

        return user;
    }
}

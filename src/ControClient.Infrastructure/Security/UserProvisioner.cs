using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Alta del usuario en el primer inicio de sesión. Si existe una ficha
/// pre-registrada por correo (<c>pending:correo</c>), se vincula con la identidad
/// real y conserva el rol y los permisos ya preparados. El primer administrador
/// se define por configuración.
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

        if (user is null && !string.IsNullOrWhiteSpace(email))
        {
            // Ficha preparada por un administrador antes del primer acceso.
            var normalized = email.Trim().ToLowerInvariant();
            var pending = await db.Users.FirstOrDefaultAsync(
                u => u.EntraObjectId.ToLower() == "pending:" + normalized, ct);

            if (pending is not null)
            {
                pending.LinkEntraIdentity(objectId);
                pending.UpdateProfile(displayName, email);
                pending.Activate();
                pending.RegisterLogin();
                await db.SaveChangesAsync(ct);
                return pending;
            }

            // Misma persona con otro identificador de Entra (cuenta migrada, alias
            // o cambio de tenant): se adopta el nuevo oid conservando rol y permisos.
            var byEmail = await db.Users.FirstOrDefaultAsync(
                u => u.Email != null && u.Email.ToLower() == normalized && u.EntraObjectId != objectId, ct);

            if (byEmail is not null)
            {
                byEmail.LinkEntraIdentity(objectId);
                byEmail.UpdateProfile(displayName, email);
                byEmail.Activate();
                byEmail.RegisterLogin();
                await db.SaveChangesAsync(ct);
                return byEmail;
            }
        }

        if (user is null)
        {
            var bootstrapAdmin = configuration["Authentication:BootstrapAdminObjectId"];
            var isBootstrapAdmin = string.Equals(bootstrapAdmin, objectId, StringComparison.Ordinal);

            // Modo cerrado: sin ficha previa ni pre-registro, no se entra.
            var requirePreRegistration = configuration.GetValue("Authentication:RequirePreRegistration", true);
            if (requirePreRegistration && !isBootstrapAdmin)
                throw new UnprovisionedUserException(email ?? objectId);

            var defaultRole = isBootstrapAdmin ? AppRole.Administrator : AppRole.ReadOnly;

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

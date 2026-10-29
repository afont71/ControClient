using System.Text.Json;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;

namespace ControClient.Infrastructure.Forms;

/// <summary>
/// Plantillas iniciales para poder empezar sin configurar nada. Se ejecuta una
/// sola vez (si no hay plantillas) y no sobrescribe cambios del usuario.
/// </summary>
public static class DefaultTemplateSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (db.FormTemplates.Any())
            return;

        var servidor = new FormTemplate("servidor", "Servidor", "Servidor físico, virtual o cloud de un cliente.",
        [
            new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
            new FieldDefinition("hostname", "Hostname", FieldType.Text, order: 2),
            new FieldDefinition("ip", "Dirección IP", FieldType.Ip, order: 3),
            new FieldDefinition("sistema", "Sistema operativo", FieldType.Select, order: 4,
                options: JsonSerializer.Serialize(new[] { "Windows Server", "Linux", "Otro" })),
            new FieldDefinition("rol", "Función", FieldType.Select, order: 5,
                options: JsonSerializer.Serialize(new[] { "Dominio", "Base de datos", "Web", "Ficheros", "Backup", "Otro" })),
            new FieldDefinition("notas", "Notas", FieldType.LongText, order: 6)
        ]);

        var vpn = new FormTemplate("vpn", "VPN", "Acceso remoto configurado para el cliente.",
        [
            new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
            new FieldDefinition("tipo", "Tipo", FieldType.Select, required: true, order: 2,
                options: JsonSerializer.Serialize(new[] { "OpenVPN", "WireGuard", "IPsec", "Otro" })),
            new FieldDefinition("endpoint", "Endpoint", FieldType.Text, order: 3),
            new FieldDefinition("red", "Red interna", FieldType.Text, order: 4),
            new FieldDefinition("notas", "Notas", FieldType.LongText, order: 5)
        ]);

        var correo = new FormTemplate("correo", "Correo", "Dominio de correo y proveedor del cliente.",
        [
            new FieldDefinition("dominio", "Dominio", FieldType.Text, required: true, order: 1),
            new FieldDefinition("proveedor", "Proveedor", FieldType.Select, required: true, order: 2,
                options: JsonSerializer.Serialize(new[] { "Microsoft 365", "Google", "Plesk", "Otro" })),
            new FieldDefinition("usuarios", "Nº de buzones", FieldType.Number, order: 3),
            new FieldDefinition("notas", "Notas", FieldType.LongText, order: 4)
        ]);

        db.FormTemplates.AddRange(servidor, vpn, correo);
        await db.SaveChangesAsync(ct);
    }
}

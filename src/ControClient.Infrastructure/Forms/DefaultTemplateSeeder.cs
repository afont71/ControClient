using System.Text.Json;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Forms;

/// <summary>
/// Plantillas iniciales. Es idempotente por clave: añade las que falten y no toca
/// las existentes (así no se pisan versiones creadas por el usuario).
/// </summary>
public static class DefaultTemplateSeeder
{
    private static string Options(params string[] options) => JsonSerializer.Serialize(options);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FormTemplates.Select(t => t.Key).Distinct().ToListAsync(ct);
        var keys = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Add(FormTemplate template) => db.FormTemplates.Add(template);

        if (!keys.Contains("servidor"))
        {
            Add(new FormTemplate("servidor", "Servidor", "Servidor físico, virtual o cloud de un cliente.",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("hostname", "Hostname", FieldType.Text, order: 2),
                new FieldDefinition("ip", "Dirección IP", FieldType.Ip, order: 3),
                new FieldDefinition("sistema", "Sistema operativo", FieldType.Select, order: 4,
                    options: Options("Windows Server", "Linux", "Otro")),
                new FieldDefinition("rol", "Función", FieldType.Select, order: 5,
                    options: Options("Dominio", "Base de datos", "Web", "Ficheros", "Backup", "Otro")),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 6)
            ]));
        }

        if (!keys.Contains("vpn"))
        {
            Add(new FormTemplate("vpn", "VPN", "Acceso remoto configurado para el cliente.",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("tipo", "Tipo", FieldType.Select, required: true, order: 2,
                    options: Options("OpenVPN", "WireGuard", "IPsec", "Otro")),
                new FieldDefinition("endpoint", "Endpoint", FieldType.Text, order: 3),
                new FieldDefinition("red", "Red interna", FieldType.Text, order: 4),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 5)
            ]));
        }

        if (!keys.Contains("correo"))
        {
            Add(new FormTemplate("correo", "Correo", "Dominio de correo y proveedor del cliente.",
            [
                new FieldDefinition("dominio", "Dominio", FieldType.Text, required: true, order: 1),
                new FieldDefinition("proveedor", "Proveedor", FieldType.Select, required: true, order: 2,
                    options: Options("Microsoft 365", "Google", "Plesk", "Otro")),
                new FieldDefinition("usuarios", "Nº de buzones", FieldType.Number, order: 3),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 4)
            ]));
        }

        if (!keys.Contains("equipo"))
        {
            Add(new FormTemplate("equipo", "Equipo", "Puesto de trabajo, portátil, switch, impresora u otro dispositivo.",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("tipo", "Tipo", FieldType.Select, required: true, order: 2,
                    options: Options("Sobremesa", "Portátil", "Switch", "Router", "Firewall", "Impresora", "Otro")),
                new FieldDefinition("marca", "Marca", FieldType.Text, order: 3),
                new FieldDefinition("modelo", "Modelo", FieldType.Text, order: 4),
                new FieldDefinition("serie", "Nº de serie", FieldType.Text, order: 5),
                new FieldDefinition("ip", "Dirección IP", FieldType.Ip, order: 6),
                new FieldDefinition("usuario", "Usuario asignado", FieldType.Email, order: 7),
                new FieldDefinition("compra", "Fecha de compra", FieldType.Date, order: 8),
                new FieldDefinition("garantia", "Fin de garantía", FieldType.Date, order: 9),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 10)
            ]));
        }

        if (!keys.Contains("servicio"))
        {
            Add(new FormTemplate("servicio", "Servicio", "Servicio contratado o prestado al cliente (backup, hosting, dominio…).",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("tipo", "Tipo", FieldType.Select, required: true, order: 2,
                    options: Options("Correo", "Backup", "Antivirus", "Hosting", "Dominio", "ERP", "Otro")),
                new FieldDefinition("proveedor", "Proveedor", FieldType.Text, order: 3),
                new FieldDefinition("alta", "Fecha de alta", FieldType.Date, order: 4),
                new FieldDefinition("renovacion", "Próxima renovación", FieldType.Date, order: 5),
                new FieldDefinition("coste", "Coste anual (€)", FieldType.Number, order: 6),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 7)
            ]));
        }

        if (!keys.Contains("licencia"))
        {
            Add(new FormTemplate("licencia", "Licencia", "Licencia de software y su renovación.",
            [
                new FieldDefinition("producto", "Producto", FieldType.Text, required: true, order: 1),
                new FieldDefinition("tipo", "Modalidad", FieldType.Select, order: 2,
                    options: Options("Suscripción", "Perpetua", "OEM", "Otro")),
                new FieldDefinition("cantidad", "Cantidad", FieldType.Number, order: 3),
                new FieldDefinition("proveedor", "Proveedor", FieldType.Text, order: 4),
                new FieldDefinition("renovacion", "Fecha de finalización", FieldType.Date, order: 5),
                new FieldDefinition("aviso", "Avisar antes de", FieldType.Select, order: 6,
                    options: Options("7 días", "15 días", "30 días", "60 días", "90 días", "6 meses", "1 año")),
                new FieldDefinition("notas", "Notas (sin claves ni contraseñas)", FieldType.LongText, order: 7)
            ]));
        }

        if (!keys.Contains("contacto"))
        {
            Add(new FormTemplate("contacto", "Contacto", "Persona de contacto del cliente.",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("puesto", "Puesto", FieldType.Text, order: 2),
                new FieldDefinition("correo", "Correo", FieldType.Email, order: 3),
                new FieldDefinition("telefono", "Teléfono", FieldType.Text, order: 4),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 5)
            ]));
        }

        if (!keys.Contains("red"))
        {
            Add(new FormTemplate("red", "Red", "Red, VLAN o rango de direcciones del cliente.",
            [
                new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
                new FieldDefinition("rango", "Rango", FieldType.Text, order: 2),
                new FieldDefinition("vlan", "VLAN", FieldType.Text, order: 3),
                new FieldDefinition("gateway", "Gateway", FieldType.Ip, order: 4),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 5)
            ]));
        }

        // Migración de la plantilla de licencia: si la última versión no tiene el
        // aviso previo, se crea una versión nueva conservando los campos actuales.
        var latestLicense = await db.FormTemplates
            .Where(t => t.Key == "licencia")
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(ct);

        if (latestLicense is not null && latestLicense.Fields.All(f => f.Key != "aviso"))
        {
            var fields = latestLicense.Fields.OrderBy(f => f.Order).Select(f =>
                new FieldDefinition(f.Key, f.Label, f.Type, f.IsRequired, f.Order, f.OptionsJson, f.HelpText)).ToList();

            fields.Insert(fields.Count - 1, new FieldDefinition("aviso", "Avisar antes de", FieldType.Select,
                required: false, order: fields.Count, options: Options("7 días", "15 días", "30 días", "60 días", "90 días", "6 meses", "1 año")));

            db.FormTemplates.Add(new FormTemplate("licencia", latestLicense.Name, latestLicense.Description,
                fields, latestLicense.Version + 1));
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }
}

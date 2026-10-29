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
                new FieldDefinition("renovacion", "Renovación", FieldType.Date, order: 5),
                new FieldDefinition("notas", "Notas (sin claves ni contraseñas)", FieldType.LongText, order: 6)
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

        if (!keys.Contains("vencimiento"))
        {
            Add(new FormTemplate("vencimiento", "Vencimiento", "Fecha a vigilar: contrato, dominio, certificado, licencia…",
            [
                new FieldDefinition("concepto", "Concepto", FieldType.Text, required: true, order: 1),
                new FieldDefinition("tipo", "Tipo", FieldType.Select, required: true, order: 2,
                    options: Options("Contrato", "Dominio", "Certificado SSL", "Licencia", "Seguro", "Otro")),
                new FieldDefinition("fecha", "Fecha de vencimiento", FieldType.Date, required: true, order: 3),
                new FieldDefinition("responsable", "Responsable", FieldType.Email, order: 4),
                new FieldDefinition("notas", "Notas", FieldType.LongText, order: 5)
            ]));
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }
}

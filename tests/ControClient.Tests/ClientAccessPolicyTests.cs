using ControClient.Domain.Security;
using ControClient.Domain.Users;

namespace ControClient.Tests;

public class ClientAccessPolicyTests
{
    private readonly ClientAccessPolicy _policy = new();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid[] _granted;

    public ClientAccessPolicyTests() => _granted = [_clientId];

    private static AppUser User(AppRole role) =>
        new($"oid-{role}", role.ToString(), null, role);

    [Fact]
    public void Administrador_ve_clientes_sin_concesion()
    {
        Assert.True(_policy.CanViewClient(User(AppRole.Administrator), Guid.NewGuid(), []));
    }

    [Fact]
    public void Tecnico_sin_concesion_no_ve_el_cliente()
    {
        Assert.False(_policy.CanViewClient(User(AppRole.Technician), _clientId, []));
    }

    [Fact]
    public void Tecnico_con_concesion_ve_y_edita()
    {
        Assert.True(_policy.CanViewClient(User(AppRole.Technician), _clientId, _granted));
        Assert.True(_policy.CanEditClient(User(AppRole.Technician), _clientId, _granted));
    }

    [Fact]
    public void Auditor_ve_pero_no_edita()
    {
        Assert.True(_policy.CanViewClient(User(AppRole.Auditor), _clientId, []));
        Assert.False(_policy.CanEditClient(User(AppRole.Auditor), _clientId, []));
    }

    [Fact]
    public void Administrador_no_puede_revelar_secretos()
    {
        // Matriz §3.3: separación entre administración y acceso a secretos (SEC-06).
        Assert.False(_policy.CanRevealSecret(User(AppRole.Administrator), _clientId, _granted));
    }

    [Fact]
    public void Tecnico_con_concesion_puede_revelar()
    {
        Assert.True(_policy.CanRevealSecret(User(AppRole.Technician), _clientId, _granted));
    }

    [Fact]
    public void Custodio_sin_concesion_no_revela()
    {
        Assert.False(_policy.CanRevealSecret(User(AppRole.VaultCustodian), _clientId, []));
    }

    [Fact]
    public void Usuario_desactivado_no_ve_nada()
    {
        var user = User(AppRole.Administrator);
        user.Deactivate();
        Assert.False(_policy.CanViewClient(user, _clientId, _granted));
    }

    [Fact]
    public void Lector_no_edita()
    {
        Assert.False(_policy.CanEditClient(User(AppRole.ReadOnly), _clientId, _granted));
    }
}

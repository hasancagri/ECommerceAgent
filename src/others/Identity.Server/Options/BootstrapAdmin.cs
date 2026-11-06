namespace Identity.Server.Options;

// 030: acilista seed edilen bootstrap admin kimligi — section "BootstrapAdmin". Email/parola bosken
// (config'te tanimsiz) admin olusturma atlanir; bu yuzden alanlar zorunlu (Required) DEGIL.
public class BootstrapAdmin
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

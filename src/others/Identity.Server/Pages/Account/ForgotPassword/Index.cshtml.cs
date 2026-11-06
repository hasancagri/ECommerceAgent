using Microsoft.AspNetCore.WebUtilities;

namespace Identity.Server.Pages.ForgotPassword;

// 005 FR-009: şifremi-unuttum — adım 1 (e-posta al, reset bağlantısı üret). Bu dilimde e-posta
// GÖNDERİLMEZ: bağlantı ILogger'a yazılır (mock; SMTP kararı vault 03'te açık). Hesabın varlığı
// yanıttan SIZDIRILMAZ — e-posta kayıtlı olsa da olmasa da aynı onay mesajı gösterilir.
[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<Index> _logger;

    [BindProperty]
    [Required(ErrorMessage = "E-posta zorunlu")]
    [EmailAddress]
    public string? Email { get; set; }

    public bool Submitted { get; private set; }

    public Index(UserManager<ApplicationUser> userManager, ILogger<Index> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Email!);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = Url.Page("/Account/ResetPassword/Index",
                pageHandler: null,
                values: new { email = Email, token = encoded },
                protocol: Request.Scheme)!;
            // Mock teslim: bağlantı log'a (token süreli + tek kullanımlık — Identity sağlar).
            _logger.LogInformation("Şifre sıfırlama bağlantısı (mock e-posta, {Email}): {Link}", Email, link);
        }

        Submitted = true;
        return Page();
    }
}

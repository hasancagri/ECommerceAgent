using Microsoft.AspNetCore.Authentication.Google;

namespace Identity.Server.Pages.ExternalLogin;

// 006: Google ile giriş — challenge (OnGet) + dönüş (OnGetCallback). Hesap kuralları sırayla:
// (1) external login zaten bağlıysa o hesaba gir; (2) DOĞRULANMIŞ e-posta mevcut kullanıcıyla
// eşleşiyorsa login'i o hesaba BAĞLA (çift hesap imkansız); (3) yoksa şifresiz customer yarat.
// Doğrulanmamış e-posta + mevcut hesap = REDDET (hesap ele geçirme riski) — şifreyle girilsin.
[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GoogleAuth _googleAuth;
    private readonly ILogger<Index> _logger;

    public Index(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        GoogleAuth googleAuth,
        ILogger<Index> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _googleAuth = googleAuth;
        _logger = logger;
    }

    // Login sayfasındaki "Google ile devam et" buraya gelir → Google'a challenge.
    public IActionResult OnGet(string? returnUrl)
    {
        if (!_googleAuth.IsConfigured)
            return RedirectToPage("/Account/Login/Index", new { returnUrl });

        var redirectUrl = Url.Page("/Account/ExternalLogin/Index", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUrl);
        return new ChallengeResult(GoogleDefaults.AuthenticationScheme, properties);
    }

    // Google'dan dönüş. remoteError = kullanıcı onayı iptal etti vb. → login sayfasına mesajla.
    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl, string? remoteError)
    {
        if (remoteError is not null)
            return BackToLogin(returnUrl, $"Google girişi tamamlanamadı: {remoteError}");

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return BackToLogin(returnUrl, "Google girişi tamamlanamadı. Lütfen tekrar deneyin.");

        // (1) Bu Google hesabı daha önce bağlanmış → doğrudan gir.
        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
            return RedirectLocal(returnUrl);

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return BackToLogin(returnUrl, "Google hesabından e-posta alınamadı.");

        var emailVerified = string.Equals(
            info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // (2) Aynı e-postalı hesap var: yalnız DOĞRULANMIŞ e-postayla bağla; değilse reddet.
            if (!emailVerified)
                return BackToLogin(returnUrl, "Bu e-posta kayıtlı; Google e-postan doğrulanmamış olduğundan bağlanamadı. Şifrenle giriş yap.");

            var linked = await _userManager.AddLoginAsync(existing, info);
            if (!linked.Succeeded)
                return BackToLogin(returnUrl, "Google hesabı bağlanamadı. Şifrenle giriş yap.");

            await _signInManager.SignInAsync(existing, isPersistent: false);
            _logger.LogInformation("Google login mevcut hesaba bağlandı: {Email}", email);
            return RedirectLocal(returnUrl);
        }

        // (3) Yeni kullanıcı: şifresiz customer (bootstrap admin seed'iyle aynı claim deseni).
        var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = emailVerified };
        var created = await _userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            _logger.LogWarning("Google kullanıcısı oluşturulamadı: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Code)));
            return BackToLogin(returnUrl, "Hesap oluşturulamadı. Lütfen tekrar deneyin.");
        }

        await _userManager.AddToRoleAsync(user, RoleAssignmentService.CustomerRole);
        await _userManager.AddClaimsAsync(user,
            [new Claim(Claims.Name, displayName), new Claim(Claims.Email, email)]);
        await _userManager.AddLoginAsync(user, info);

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("Google ile yeni customer hesabı açıldı: {Email}", email);
        return RedirectLocal(returnUrl);
    }

    private IActionResult BackToLogin(string? returnUrl, string message)
    {
        TempData["ExternalLoginError"] = message;
        return RedirectToPage("/Account/Login/Index", new { returnUrl });
    }

    private IActionResult RedirectLocal(string? returnUrl)
        => Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
}

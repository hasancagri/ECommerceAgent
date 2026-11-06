using Microsoft.AspNetCore.WebUtilities;

namespace Identity.Server.Pages.ResetPassword;

// 005 FR-009: şifremi-unuttum — adım 2 (bağlantıdaki token ile yeni şifre). Token Identity'nin
// DataProtection reset token'ı: süreli ve tek kullanımlık; geçersiz/ikinci kullanım nazik hatayla düşer.
[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Yeni şifre zorunlu")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    public bool Succeeded { get; private set; }

    public Index(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public IActionResult OnGet()
        => string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token)
            ? RedirectToPage("/Account/ForgotPassword/Index")
            : Page();

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
            return RedirectToPage("/Account/ForgotPassword/Index");

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null)
        {
            // Hesap varlığını sızdırma: aynı nazik hata.
            ModelState.AddModelError(string.Empty, "Bağlantı geçersiz ya da süresi dolmuş.");
            return Page();
        }

        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Token));
        var result = await _userManager.ResetPasswordAsync(user, token, NewPassword!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        Succeeded = true;
        return Page();
    }
}

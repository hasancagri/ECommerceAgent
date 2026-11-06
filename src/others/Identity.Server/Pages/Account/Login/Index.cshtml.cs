namespace Identity.Server.Pages.Login;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private const string StoreHome = "https://localhost:7042/";

    private readonly SignInManager<ApplicationUser> _signInManager;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public bool AllowRememberLogin => LoginOptions.AllowRememberLogin;

    public Index(SignInManager<ApplicationUser> signInManager) => _signInManager = signInManager;

    // FLOW.md Süreç 3 girişi — authorize Challenge'ının indiği ekran; returnUrl = authorize devamı.
    public void OnGet(string? returnUrl) => Input = new InputModel { ReturnUrl = returnUrl };

    // FLOW.md Süreç 3 — parola YALNIZ burada girilir (agent asla görmez; OAuth delegasyonunun
    // amacı bu). Başarıda cookie kurulur, authorize'a dönülür → akış Süreç 4'ten sürer.
    public async Task<IActionResult> OnPost()
    {
        // "Cancel" → giriş yapmadan mağazaya dön.
        if (Input.Button != "login")
            return Redirect(StoreHome);

        if (!ModelState.IsValid)
            return Page();

        // ASP.NET Identity ile doğrula; başarısızlık nedenine göre kilit/2FA ayrıştırılır.
        var result = await _signInManager.PasswordSignInAsync(
            Input.Username!, Input.Password!, Input.RememberLogin, lockoutOnFailure: true);

        if (result.Succeeded)
            // returnUrl = authorize devamı (yerel path). Oraya dön → token akışı tamamlanır.
            return Redirect(Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl! : StoreHome);

        var message = result switch
        {
            { IsLockedOut: true } => "Account is locked out. Please try again later.",
            { IsNotAllowed: true } => "Login not allowed. The account may require confirmation.",
            { RequiresTwoFactor: true } => "Two-factor authentication is required.",
            _ => LoginOptions.InvalidCredentialsErrorMessage,
        };
        ModelState.AddModelError(string.Empty, message);
        return Page();
    }
}
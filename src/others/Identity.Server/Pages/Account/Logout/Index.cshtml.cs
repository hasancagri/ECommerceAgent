namespace Identity.Server.Pages.Logout;

// IdP'de doğrudan çıkış (nav linki). OIDC signout akışı ise /connect/logout end-session ucundan geçer.
[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public Index(SignInManager<ApplicationUser> signInManager) => _signInManager = signInManager;

    public async Task<IActionResult> OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            await _signInManager.SignOutAsync();

        return Redirect("https://localhost:7042/");
    }
}
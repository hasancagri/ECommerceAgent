namespace Identity.Server.Pages.Create;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private const string StoreHome = "https://localhost:7042/";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public Index(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // FLOW.md Süreç 1 girişi — authorize'ın prompt=create yönlendirmesinin indiği ekran.
    public void OnGet(string? returnUrl) => Input = new InputModel { ReturnUrl = returnUrl };

    // FLOW.md Süreç 1-2 — kullanıcı doğar, sunucu customer rolünü atar (2; seçilemez),
    // doğrudan login olup temizlenmiş returnUrl ile authorize'a dönülür (create döngüsü yok).
    public async Task<IActionResult> OnPost()
    {
        if (Input.Button != "create")
            return Redirect(StoreHome);

        if (await _userManager.FindByNameAsync(Input.Username!) is not null)
            ModelState.AddModelError("Input.Username", "Invalid username");

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser { UserName = Input.Username, Email = Input.Email };

        var createResult = await _userManager.CreateAsync(user, Input.Password!);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        // name/email kullanıcının claim'lerine yazılır (token/userinfo bunları okur).
        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(Input.Name)) claims.Add(new Claim(Claims.Name, Input.Name));
        if (!string.IsNullOrWhiteSpace(Input.Email)) claims.Add(new Claim(Claims.Email, Input.Email));
        if (claims.Count > 0) await _userManager.AddClaimsAsync(user, claims);

        // 030 RBAC: yeni kullanıcı otomatik tek rol olarak customer alır (sunucu atar, seçilemez).
        await _userManager.AddToRoleAsync(user, Rbac.RoleAssignmentService.CustomerRole);

        // Aktivasyon-mail YOK: kayıt sonrası doğrudan login (FR-014).
        await _signInManager.SignInAsync(user, isPersistent: false);

        // returnUrl = create-prompt'u temizlenmiş authorize devamı (yerel). Oraya dön → token akışı biter.
        return Redirect(Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl! : StoreHome);
    }
}
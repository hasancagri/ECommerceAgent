namespace Identity.Server.Pages.Admin.Roles;

// 030 RBAC: bir rolün scope demetini işaretle. Seçenekler KnownScopes'tan (kapalı liste);
// serbest metin girişi yok → uyumsuzluk imkansız (FR-006).
[SecurityHeaders]
public class Scopes(RoleAssignmentService svc, RoleManager<IdentityRole> roleManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = default!;

    public string RoleName { get; private set; } = "";

    public IReadOnlyList<ScopeCheck> Items { get; private set; } = [];

    [BindProperty]
    public List<string> Selected { get; set; } = [];

    public string? Error { get; set; }

    public record ScopeCheck(string Name, string Description, bool Checked);

    public async Task<IActionResult> OnGet()
    {
        var role = await roleManager.FindByIdAsync(Id);
        if (role is null) return NotFound();

        RoleName = role.Name!;
        var current = (await svc.GetRoleScopesAsync(Id)).ToHashSet();
        Items = [.. KnownScopes.All.Select(s => new ScopeCheck(s.Name, s.Description, current.Contains(s.Name)))];
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var result = await svc.SetRoleScopesAsync(Id, Selected);
        if (!result.Success)
        {
            Error = result.Error;
            await OnGet();
            return Page();
        }
        return RedirectToPage("/Admin/Roles/Index");
    }
}
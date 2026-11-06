namespace Identity.Server.Pages.Admin.Users;

// 030 RBAC: kullanıcı listesi + tek-rol atama. Son admin kilidi servis içinde (INV-4).
[SecurityHeaders]
public class Index(UserManager<ApplicationUser> userManager, RoleAssignmentService svc) : PageModel
{
    public IReadOnlyList<UserRow> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public string? Error { get; set; }

    public record UserRow(string Id, string UserName, string? Role);

    public async Task OnGet() => await LoadAsync();

    public async Task<IActionResult> OnPostSetRole(string userId, string role)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            Error = "Kullanıcı bulunamadı.";
            await LoadAsync();
            return Page();
        }

        var result = await svc.SetUserRoleAsync(user, role);
        if (!result.Success)
        {
            Error = result.Error;
            await LoadAsync();
            return Page();
        }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Roles = [.. (await svc.ListRolesAsync()).Select(r => r.Name!)];

        var rows = new List<UserRow>();
        foreach (var user in await userManager.Users.ToListAsync())
            rows.Add(new UserRow(user.Id, user.UserName ?? "", await svc.GetUserRoleAsync(user)));
        Users = rows;
    }
}
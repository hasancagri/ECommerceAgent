namespace Identity.Server.Pages.Admin.Roles;

// 030 RBAC: rol listesi + yeni rol + silme. /Admin AuthorizeFolder ile admin rolü ister.
[SecurityHeaders]
public class Index(RoleAssignmentService svc) : PageModel
{
    public IReadOnlyList<RoleRow> Roles { get; private set; } = [];

    [BindProperty]
    public string? NewRoleName { get; set; }

    public string? Error { get; set; }

    public record RoleRow(string Id, string Name, int ScopeCount, bool IsSeed);

    public async Task OnGet() => await LoadAsync();

    public async Task<IActionResult> OnPostCreate()
    {
        var result = await svc.CreateRoleAsync(NewRoleName ?? "");
        if (!result.Success)
        {
            Error = result.Error;
            await LoadAsync();
            return Page();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDelete(string id)
    {
        var result = await svc.DeleteRoleAsync(id);
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
        var rows = new List<RoleRow>();
        foreach (var role in await svc.ListRolesAsync())
        {
            var scopes = await svc.GetRoleScopesAsync(role.Id);
            rows.Add(new RoleRow(role.Id, role.Name!, scopes.Count, RoleAssignmentService.IsSeedRole(role.Name)));
        }
        Roles = rows;
    }
}
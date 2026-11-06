namespace Identity.Server.Pages.Error;

[AllowAnonymous]
[SecurityHeaders]
public class Index : PageModel
{
    public string? Error { get; set; }

    public void OnGet(string? errorId) => Error = errorId;
}
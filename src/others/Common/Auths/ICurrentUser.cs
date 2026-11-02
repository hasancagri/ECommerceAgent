using Common.Dependencies;

namespace Common.Auths;

public interface ICurrentUser : ITransientDependency
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    ICurrentUser Load(ClaimsPrincipal principal);
}
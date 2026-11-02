namespace Common.Utils.Authorization;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}
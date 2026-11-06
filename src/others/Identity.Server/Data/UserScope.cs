namespace Identity.Server;

// Kullanıcının kayıtta seçtiği yetki. Kullanıcının efektif scope setini oluşturur.
// Anahtar bu scope'ları miras alır (rol yok — yalnızca scope; anayasa V).
public class UserScope
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string Scope { get; private set; } = default!;

    private UserScope() { }

    public static UserScope Create(string userId, string scope) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Scope = scope,
    };
}
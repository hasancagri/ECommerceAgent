namespace Identity.Server;

// Kullanıcıya bağlı opak dış anahtar. Scope taşımaz — yetki kullanıcının UserScopes'undan gelir.
// Ham anahtar saklanmaz; yalnızca KeyHash (SHA-256). Son kullanma tarihi yoktur (yalnızca iptal).
public class ApiKey
{
    public Guid Id { get; private set; }
    public string KeyHash { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string? Name { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private ApiKey() { }

    public static ApiKey Create(string userId, string keyHash, string? name) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        KeyHash = keyHash,
        Name = name,
        IsRevoked = false,
        CreatedAt = DateTime.UtcNow,
    };

    // Idempotent: zaten iptalliyse no-op.
    public void Revoke()
    {
        if (IsRevoked)
            return;

        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
}
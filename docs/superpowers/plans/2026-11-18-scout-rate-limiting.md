# ScoutAgent Rate Limiting (A+B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `/api/chat/v1/messages`'e burst (dakika) + kalıcı günlük rate limit ekle — tek kullanıcı/IP OpenAI maliyetini yakmasın.

**Architecture:** A = ASP.NET `AddRateLimiter` (in-memory FixedWindow, partition sub|ip). B = Marten `ScoutUsageDaily` doc + `DailyUsageGuard` (gün-başına kalıcı cap). İkisi de `/messages` endpoint katmanında; SendMessage handler değişmez. scout-agent tek-pod (in-proc).

**Tech Stack:** .NET 10, ASP.NET RateLimiter (System.Threading.RateLimiting), Marten (scoutDb), Scrutor DI, xUnit+Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-18-scout-rate-limiting-design.md`

## Global Constraints
- Limitler: anonim **5/dk + 30/gün (IP)**, login **20/dk + 200/gün (sub)**. ScoutOptions'ta config'li.
- Partition key: login → `u:{sub}`, anonim → `ip:{ip}`. A (partition) + B (doc id öneki) aynı key'i kullanır.
- IP = `HttpContext.Connection.RemoteIpAddress` (ForwardedHeaders açık → X-Forwarded-For gerçek IP).
- 429 gövdesi sabit: `{ "message": "Çok fazla istek, biraz sonra tekrar dene." }` (sayaç/sır sızdırmaz).
- Gün = **UTC** (`DateTime.UtcNow`→`DateOnly`); rollover Id'deki tarihten.
- `IConfiguration` doğrudan okuma YOK (Options pattern); DI Scrutor marker (`AddAllDependencies`).

## Review Focus
- **Hem userId hem ip null** (teorik) → key boş kalmamalı: Task 1 testi `ip:` fallback ve guard.
- **UTC gün rollover** → gece yarısı cap sıfırlanır, local-saat kayması yok: Task 1 key testi UTC tarihle.
- **Rate limiter partition bellek şişmesi** (çok IP) → FixedWindow replenishing + `AutoReplenishment`; Task 3 global limiter yerine partition başına pencere, not düş.
- **429 kaynağı A mı B mi** — ikisi de aynı gövde döndürür (kullanıcı ayırt etmez): Task 2+3 aynı mesaj sabiti.
- **ForwardedHeaders olmadan anon IP = proxy IP** (hepsi aynı partition) → canlıda XFF doğrula (Task 3 live).

---

### Task 1: ScoutOptions limitleri + ScoutUsageKey (pure, TDD)

**Files:**
- Modify: `src/agents/ScoutAgent.Api/Options/ScoutOptions.cs`
- Create: `src/agents/ScoutAgent.Api/RateLimiting/ScoutUsageKey.cs`
- Test: `tests/ScoutAgent.Api.Tests/ScoutUsageKeyTests.cs`

**Interfaces:**
- Produces: `ScoutOptions` yeni alanlar `AnonPerMinute`(5), `AnonPerDay`(30), `UserPerMinute`(20), `UserPerDay`(200) (int, default'lu).
- Produces: `static string ScoutUsageKey.Partition(Guid? userId, string? ip)` → login `"u:{userId}"`, anon `"ip:{ip}"`, ikisi de yoksa `"ip:unknown"`.

- [ ] **Step 1: Failing test** — `tests/ScoutAgent.Api.Tests/ScoutUsageKeyTests.cs`
```csharp
public class ScoutUsageKeyTests
{
    [Fact] public void Partition_WithUserId_ReturnsUserKey() =>
        ScoutUsageKey.Partition(Guid.Parse("11111111-1111-1111-1111-111111111111"), "9.9.9.9")
            .ShouldBe("u:11111111-1111-1111-1111-111111111111");
    [Fact] public void Partition_AnonymousWithIp_ReturnsIpKey() =>
        ScoutUsageKey.Partition(null, "9.9.9.9").ShouldBe("ip:9.9.9.9");
    [Fact] public void Partition_NoUserNoIp_ReturnsUnknown() =>
        ScoutUsageKey.Partition(null, null).ShouldBe("ip:unknown");
}
```

- [ ] **Step 2: Run → FAIL** `dotnet test tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj --filter ScoutUsageKeyTests` — "ScoutUsageKey yok".

- [ ] **Step 3: Implement** `ScoutUsageKey.Partition` (userId varsa+Empty değilse `u:`, değilse ip `ip:` + null→`unknown`) + ScoutOptions'a 4 int alanı ekle.

- [ ] **Step 4: Run → PASS** (aynı filtre).

- [ ] **Step 5: Commit** `feat(scout): rate-limit options + ScoutUsageKey partition (pure)`

---

### Task 2: ScoutUsageDaily doc + DailyUsageGuard + endpoint B wiring

**Files:**
- Create: `src/agents/ScoutAgent.Api/RateLimiting/ScoutUsageDaily.cs`
- Create: `src/agents/ScoutAgent.Api/RateLimiting/DailyUsageGuard.cs`
- Modify: `src/agents/ScoutAgent.Api/ChatEndpointExtension.cs` (/messages)
- Modify: `src/agents/ScoutAgent.Api/Constants/ScoutResourceConstants.cs` (429 mesajı)

**Interfaces:**
- Consumes: `ScoutUsageKey.Partition` + `ScoutOptions` (Task 1).
- Produces: `ScoutUsageDaily` (Marten doc: `string Id`, `int Count`).
- Produces: `DailyUsageGuard : IScopedDependency` · `Task<bool> TryConsumeAsync(string partition, int dailyCap, CancellationToken ct)` — bugünün (UTC) doc id'si `{partition}:{yyyyMMdd}`; load-or-create; `Count >= dailyCap` → false; değilse `Count++` + store + `SaveChangesAsync` → true.

- [ ] **Step 1: ScoutUsageDaily + DailyUsageGuard yaz** — guard `IDocumentSession` enjekte eder; id `$"{partition}:{DateTime.UtcNow:yyyyMMdd}"`. (Marten yolu — unit test YOK, proje kuralı; canlı doğrulanır.)

- [ ] **Step 2: /messages endpoint'inde B guard'ı** — `currentUser.Load` sonrası: `var ip = http.Connection.RemoteIpAddress?.ToString(); var partition = ScoutUsageKey.Partition(userId, ip); var cap = userId is null ? options.AnonPerDay : options.UserPerDay;` → `if (!await guard.TryConsumeAsync(partition, cap, ct)) return Results.Json(new { message = <429 sabiti> }, statusCode: 429);` MaxMessageLength guard'ından SONRA, `bus.InvokeAsync`'ten ÖNCE. `DailyUsageGuard guard` parametresini endpoint handler'a ekle.

- [ ] **Step 3: 429 mesaj sabiti** — `ScoutResourceConstants`'a `TOO_MANY_REQUESTS = "Çok fazla istek, biraz sonra tekrar dene."` ekle, endpoint onu kullanır.

- [ ] **Step 4: Build** `dotnet build src/agents/ScoutAgent.Api/ScoutAgent.Api.csproj` → 0 hata.

- [ ] **Step 5: Commit** `feat(scout): günlük kalıcı rate cap (ScoutUsageDaily + DailyUsageGuard, B)`

---

### Task 3: AddRateLimiter burst (A)

**Files:**
- Modify: `src/agents/ScoutAgent.Api/Program.cs`
- Modify: `src/agents/ScoutAgent.Api/ChatEndpointExtension.cs` (/messages `.RequireRateLimiting`)

**Interfaces:**
- Consumes: `ScoutUsageKey.Partition` + `ScoutOptions` (Task 1), 429 mesaj sabiti (Task 2).
- Produces: policy adı `"chat"`.

- [ ] **Step 1: Program.cs AddRateLimiter** — `builder.Services.AddRateLimiter(o => { o.AddPolicy("chat", httpContext => { ... }); o.OnRejected = ... 429 json ... })`. Policy: `CurrentUser`'dan sub çöz (http.User), `var key = ScoutUsageKey.Partition(userId, ip);` → `RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = userId is null ? opts.AnonPerMinute : opts.UserPerMinute, Window = TimeSpan.FromMinutes(1), AutoReplenishment = true })`. ScoutOptions'ı `IOptions`/snapshot'tan al (policy factory içinde `httpContext.RequestServices`).

- [ ] **Step 2: Pipeline** — `app.UseRateLimiter();` satırını `app.UseAuthorization();` SONRASI, `app.MapChatEndpoints();` ÖNCESİ ekle (partition user'ı bilsin diye auth'tan sonra).

- [ ] **Step 3: /messages'e `.RequireRateLimiting("chat")`** zincirle.

- [ ] **Step 4: Build** `dotnet build` → 0 hata.

- [ ] **Step 5: Commit** `feat(scout): dakika-pencere burst rate limit (AddRateLimiter, A)`

---

### Task 4: Canlı doğrulama (Aspire/cluster)

**Files:** (operasyonel — kod yok)

- [ ] **Step 1: Aspire'da ya da canlıda** `/api/chat/v1/messages`'e hızlı ard arda istek: anonim 6. istek 1dk içinde → **429** (A). Normal 1 istek → 200.
- [ ] **Step 2: Günlük** — AnonPerDay'i geçici 2'ye düşürüp (config) 3. istek → 429 (B); doğrulayınca 30'a geri al. (Veya scoutDb `ScoutUsageDaily` Count'u gözle.)
- [ ] **Step 3: 429 gövdesi** = sabit mesaj, sayaç/sır yok.
- [ ] **Step 4: ScoutOptions limit env'leri canlı manifest'e** (opsiyonel) — default'lar kodda; değişiklik gerekirse `10-services.yaml` scout-agent env `ScoutOptions__AnonPerMinute` vb.

## Notlar
- Rate limiter policy factory ScoutOptions'ı `RequestServices.GetRequiredService<IOptions<ScoutOptions>>()` ile çözer (DI-dostu).
- B (günlük) kalıcı, A (burst) in-memory (restart'ta sıfır — kabul; burst koruması kısa-ömür zaten).

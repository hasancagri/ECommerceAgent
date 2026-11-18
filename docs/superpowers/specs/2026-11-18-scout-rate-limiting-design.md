# Tasarım: ScoutAgent Rate Limiting (A+B)

**Tarih:** 2026-11-18 · **Durum:** onay bekliyor · **Boyut:** küçük-orta (tek BC)

## Niyet + neden
Canlı scout-agent `/messages` yalnız `MaxMessageLength` guard'ına sahip — **request-rate limiti yok**.
Tek kötüye-kullanıcı kısa mesaj spam'leyerek OpenAI faturasını yakabilir. OpenAI hard-limit (dashboard)
nihai tavan; app tarafı = adalet + tek kullanıcının tavanı yakmasını önleme.

## Hedef
- Burst spam durur (dakika-pencere, kullanıcı/IP başına).
- Toplam günlük maliyet sınırlı (gün-başına kalıcı tavan).
- Anonim (kimliksiz, en riskli) sıkı; login gevşek.

## Hedef-değil
- Dağıtık/çok-pod rate limit (scout tek-pod, in-proc — in-memory yeter).
- Gateway-kenarı limit (kullanıcı kimliği orada yok).
- OpenAI dashboard limiti (kod değil; kullanıcı platform.openai.com'dan hard $10/soft $5 koyar).

## Limitler (kilitli)
| | dakika (burst, A) | gün (kalıcı, B) | partition |
|---|---|---|---|
| Anonim | 5/dk | 30/gün | IP |
| Login | 20/dk | 200/gün | kullanıcı (sub) |

## Yaklaşım (A+B, kilitli)
- **A — ASP.NET `AddRateLimiter`** (in-memory, System.Threading.RateLimiting). `/messages`'e
  partition'lı FixedWindow(1dk): key = login ise `sub`, değilse client IP; limit auth'a göre. Aşınca 429.
  IP = `HttpContext.Connection.RemoteIpAddress` (ForwardedHeaders açık → X-Forwarded-For gerçek IP).
- **B — Kalıcı günlük cap** (Marten scoutDb): doc `ScoutUsageDaily` (Id = `{key}:{yyyyMMdd}`, Count).
  Guard `DailyUsageGuard.TryConsumeAsync(key, cap)` → doc oku/artır, cap aşılırsa reddet. `/messages`
  endpoint'inde LLM çağrısından ÖNCE çağrılır; aşınca 429 (anlaşılır mesaj).

## Benim çağrılarım (builder-seviyesi)
- A ve B **ikisi de `/messages` endpoint katmanında** (HttpContext = IP + user orada; SendMessage
  handler'ı değişmez). B guard'ı A'dan sonra (burst önce ucuz reddedilir, sonra günlük sayaç artar).
- Key çözümü: `currentUser.Id != Empty ? "u:"+sub : "ip:"+RemoteIp`. İkisi ayrı partition namespace.
- Sayılar `ScoutOptions`'a (AnonPerMinute/AnonPerDay/UserPerMinute/UserPerDay) — config'li, Options pattern.
- `DailyUsageGuard` = `IScopedDependency` (Scrutor), scoutDb `IDocumentSession`. Gün rollover = Id'deki tarih.
- 429 gövdesi: `{ message: "Çok fazla istek, biraz sonra tekrar dene." }` (sır/sayaç sızdırmaz).
- Wolverine keşif: guard handler değil (endpoint'ten çağrılır), tarama kaydı gerekmez.

## TDD
- `DailyUsageGuard.TryConsumeAsync` saf mantığı (artır / cap-altı izin / cap-üstü red / gün rollover)
  test-first (`DailyUsageGuardTests`). Rate limiter config (Program.cs) = altyapı, canlı doğrulanır.

## Builder'a bırakılan
- ASP.NET rate limiter policy adı + 429 OnRejected mesaj kablosu.
- `ScoutUsageDaily` Marten doc alias (InvariantGlobalization ı-tuzağı global çözülü, ekstra gerekmez).
- Guard'ın atomik artışı (Marten optimistic/`Increment` patch veya load-modify-store; tek-pod yeter).

## Dış değişiklik (kullanıcı, kod değil)
- OpenAI dashboard: **platform.openai.com/settings/organization/limits** → hard $10 / soft $5.

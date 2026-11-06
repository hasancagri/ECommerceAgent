# Contract — /Admin yönetim yüzeyi (server-rendered, cookie)

Anayasa İlke V istisnası (admin ekranları cookie-auth) aynen taşınır; `/Admin/*` → `AdminRole` policy.

## Sayfalar

- **/Admin/Users**: kullanıcı listesi + rol atama (tek-rol). Son admin'in rolü düşürülemez (INV-4) — nazik hata.
- **/Admin/Roles**: rol listesi + oluştur/sil. Seed roller (customer/admin) ve atanmış kullanıcısı olan roller silinemez.
- **/Admin/Roles/Scopes**: rol→scope checkbox'ları; kaynak KnownScopes (kapalı liste). Bilinmeyen scope yazılamaz (INV-1).

## Davranış sözleşmesi

- customer token/cookie ile `/Admin/*` → erişim reddi (403/redirect).
- Rol→scope değişikliği ANINDA DB'ye; yeni token'lar yeni demeti taşır (mevcut token'lar süresi dolana dek eski — spec SC-005 kabulü).
- Kayıt ekranından admin doğmaz; admin yalnız BootstrapAdmin seed'i.

## Account sayfaları

- **/Account/Login** (tek giriş ekranı — ayrı admin login YOK) · **/Account/Create** (→ customer) · **/Account/Logout** · **/Account/AccessDenied**.
- Şifre sıfırlama: bağlantı e-postası bu dilimde LOG'a yazılır (mock — R10); token tek kullanımlık + süreli.
- Consent sayfası YOK (tüm istemciler Implicit).

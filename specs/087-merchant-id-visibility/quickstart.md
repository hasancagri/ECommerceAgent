# Quickstart / Doğrulama: MerchantId Görünürlük (ECommerce bacağı)

Feature'ın uçtan uca çalıştığını kanıtlayan senaryolar. Detay: [data-model](./data-model.md), [contracts](./contracts/).

## Ön koşul

- Aspire AppHost ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).
- Secret store'da `DropShopOnboarding:BootstrapRegistrationKey` + `CallbackSecret` + `CallbackBaseUrl` set (user-secrets).
- PG bacağı (AgentPlatform) register ucu + onay-MCP + HMAC-callback göndereni hazır (ayrı plan). Yoksa callback ucu stub/manuel POST ile doğrulanır.

## Senaryo 1 — Kayıt başlatma (store→PG), credential gelmeden

1. Admin AI istemcisinden `start_onboarding` (register) tool'unu çağır.
2. Beklenen: Customer.Api `MerchantInformation.RegistrationStatus=Pending`, `PendingCorrelationId` dolu; PG'ye `X-Registration-Key`'li POST gitti (202).
3. **Doğrula:** tool dönüşünde MerchantId/Key YOK; log/trace'te IBAN/Key YOK (grep). `AdminGetMerchantStatus` → status Pending, key alanı yok.

## Senaryo 2 — Credential callback (PG→store)

1. PG onayı simüle et: callback ucuna geçerli HMAC'li POST (doğru `CallbackSecret` + register'daki `correlationId`).
2. Beklenen: 200; `MerchantInformation` → `CredentialsVerified=true`, `RegistrationStatus=Active`, MerchantId+Key persist.
3. **Doğrula:** değerler hiçbir ekranda/chat'te görünmez; yalnız DB + gRPC yolunda.

## Senaryo 3 — Callback güvenliği (negatif)

1. Yanlış imzayla POST → **400**, persist YOK.
2. Eşleşmeyen `correlationId` → nötr red, persist YOK.
3. Aynı geçerli callback'i iki kez POST → ikincisi idempotent no-op (çift kayıt yok).

## Senaryo 4 — Ödeme yolu değişmedi

1. Aktif merchant ile hosted ödeme tetikle.
2. Beklenen: Payment `MerchantKeyClient` gRPC ile key'i çeker (`X-Api-Key`), ödeme çalışır; key hiçbir agent/tool dönüşünde değil.

## Senaryo 5 — 078 ekranı sökülü

1. `GET /merchant-credentials/{token}` → **404** (route yok).
2. `AdminRequestCredentialEntryLink` tool'u listede YOK.
3. **Doğrula:** `Pages/CredentialEntry/*`, `CredentialEntrySession`, `SubmitMerchantCredentials` kod tabanında yok (grep boş).

## Domain testleri (İlke VI — test-first)

`MerchantInformation`: `StartRegistration` (tek-aktif guard), `ApplyCredentialsFromCallback` (correlation-eşleşme, idempotent çift-callback, nötr red). xUnit + Shouldly, `tests/Customer.Api.Tests` (veya mevcut proje).
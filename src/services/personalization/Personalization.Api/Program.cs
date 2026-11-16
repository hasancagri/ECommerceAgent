var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddPersonalizationMarten();

// Mesajlaşma (Wolverine + RabbitMQ: UserSearched tüketici) → Extensions/MessagingExtensions.cs.
builder.AddPersonalizationMessaging();

// Dış HTTP yüzeyi YOK (012): Personalization yalnız tüketici — arama logu biriktirir, serving sonraki dilim.

var app = builder.Build();
app.MapDefaultEndpoints();

await app.RunAsync();

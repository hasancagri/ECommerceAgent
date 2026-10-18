namespace Catalog.Api.Options;

// 091: açılış katalog seed'i config'i — section "SeedOptions". Yol AppHost'tan env ile gelir
// (commit'li Seeding/Data/books.json'un mutlak yolu). Dosya YOKSA seed sessizce atlanır (K7:
// prod/seed-kapalı meşru senaryo); parse hatası fail-fast.
public class SeedOptions
{
    // Açılış seed'i anahtar — false = hiç koşma (dosya varlığından bağımsız).
    public bool Enabled { get; set; } = true;

    // Seed JSON'un mutlak yolu; boş = seed atlanır.
    public string SourceJsonPath { get; set; } = string.Empty;
}
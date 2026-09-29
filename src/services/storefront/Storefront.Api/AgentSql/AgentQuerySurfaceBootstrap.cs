namespace Storefront.Api.AgentSql;

// R1: izinli vitrin yüzeyi kurulumu — idempotent view DDL + kısıtlı rol + GRANT'lar.
// IHostedService (BackgroundService değil): StartAsync bitmeden app trafiğe çıkmaz; AddMarten
// SONRASI kayıt sırası Marten'ın ApplyAllDatabaseChangesOnStartup'ının önce koşmasını garantiler
// (mt_doc tabloları view'dan önce var olur). Weasel'a EMANET DEĞİL (spike: elle nesneyi siliyor);
// ayrı view Weasel'ın sahiplenmediği nesnedir, dokunmaz.
public class AgentQuerySurfaceBootstrap(
    string ownerConnectionString,
    AgentQueryOption options,
    ILogger<AgentQuerySurfaceBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schema = SchemaConstants.StorefrontSchemaName.ToLowerInvariant();
        // Rol adı SQL kimliğidir (config'ten, kullanıcı girdisi değil); şifre literal'i quote'lanır.
        var role = options.RoleName;
        var password = options.RolePassword.Replace("'", "''");

        await using var conn = new Npgsql.NpgsqlConnection(ownerConnectionString);
        await conn.OpenAsync(cancellationToken);

        // 1) View (DROP+CREATE: kolon değişikliğinde CREATE OR REPLACE kırılır; grant'lar aşağıda yeniden).
        await Execute(conn, StorefrontSellableSchema.BuildViewDdl(schema), cancellationToken);

        // 1b) HNSW ifade-indeksi (kNN 9sn→35ms, canlı ölçüm). İlk kurulum dakikalar sürebilir —
        //     komut timeout'u bilinçli açık (0). Weasel'ın bu indeksi silip silmediği restart'ta
        //     doğrulandı: mt_ öneksiz yabancı indekse dokunmuyor; silse de IF NOT EXISTS geri kurar.
        await Execute(conn, StorefrontSellableSchema.BuildEmbeddingIndexDdl(schema), cancellationToken,
            commandTimeoutSeconds: 0);

        // 2) Kısıtlı rol: yoksa oluştur, şifreyi her açılışta tazele (rotasyon = user-secrets değişimi).
        //    search_path rol seviyesinde → asistan SQL'i şema öneki yazmaz. public ŞART: pgvector
        //    tipi/operatörleri (<=>) public'te yaşar — yoksa anlamsal sorgu 42704/42883 verir
        //    (canlı bulgu); public'te role verilmiş tablo grant'ı yok, okuma yüzeyi genişlemez.
        await Execute(conn, $"""
            do $$
            begin
                if not exists (select from pg_roles where rolname = '{role}') then
                    create role {role} login password '{password}';
                end if;
            end $$;
            alter role {role} with login password '{password}';
            alter role {role} set search_path = {schema}, public;
            """, cancellationToken);

        // 3) Yetki: TEK yetki view SELECT'i (FR-005 yapısal). Önce her şeyi geri al (view yeniden
        //    yaratıldığında + geçmiş bir grant sızıntısında temiz zemin), sonra dar grant.
        await Execute(conn, $"""
            revoke all on all tables in schema {schema} from {role};
            grant usage on schema {schema} to {role};
            grant select on {schema}.{StorefrontSellableSchema.ViewName} to {role};
            """, cancellationToken);

        logger.LogInformation(
            "Agent sorgu yüzeyi hazır: {Schema}.{View} + kısıtlı rol {Role} (yalnız view SELECT)",
            schema, StorefrontSellableSchema.ViewName, role);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task Execute(Npgsql.NpgsqlConnection conn, string sql, CancellationToken ct,
        int? commandTimeoutSeconds = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (commandTimeoutSeconds is not null)
            cmd.CommandTimeout = commandTimeoutSeconds.Value;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

// Kısıtlı bağlantının DI sarmalayıcısı — sahip DataSource'la (Marten) karışmasın diye adlandırılmış tip.
public sealed class AgentQueryConnectionSource(Npgsql.NpgsqlDataSource dataSource) : IAsyncDisposable
{
    public Npgsql.NpgsqlDataSource DataSource { get; } = dataSource;

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}

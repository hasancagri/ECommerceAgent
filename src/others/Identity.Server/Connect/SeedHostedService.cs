namespace Identity.Server.Connect;

// Açılışta idempotent client + scope seed (Duende in-memory store'un karşılığı).
// Varsa güncelle, yoksa yarat; secret'lar bugünkü düz değerler (store hash'ler).
public sealed class SeedHostedService(IServiceProvider provider) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = provider.CreateScope();
        var apps = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        // Scope'lar (audience/resource eşlemesiyle) — ListResourcesAsync bunlardan 'aud' üretir.
        foreach (var name in Config.AllApiScopes)
        {
            var descriptor = new OpenIddictScopeDescriptor { Name = name, DisplayName = name };
            if (Config.ScopeResources.TryGetValue(name, out var resources))
                foreach (var resource in resources)
                    descriptor.Resources.Add(resource);

            var existing = await scopes.FindByNameAsync(name, ct);
            if (existing is null)
                await scopes.CreateAsync(descriptor, ct);
            else
                await scopes.UpdateAsync(existing, descriptor, ct);
        }

        // İstemciler.
        foreach (var client in Config.Clients)
        {
            var descriptor = BuildDescriptor(client);
            var existing = await apps.FindByClientIdAsync(client.ClientId, ct);
            if (existing is null)
                await apps.CreateAsync(descriptor, ct);
            else
                await apps.UpdateAsync(existing, descriptor, ct);
        }

        // 030 RBAC: roller + rol→scope map + bootstrap admin + backfill (idempotent).
        await SeedRbacAsync(scope.ServiceProvider, ct);
    }

    // 030 RBAC seed. Roller ve rol→scope map yalnız YOKken doldurulur; admin'in ekrandan
    // yaptığı scope düzenlemeleri yeniden başlatmada EZİLMEZ.
    private static async Task SeedRbacAsync(IServiceProvider sp, CancellationToken ct)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var bootstrapAdmin = sp.GetRequiredService<Identity.Server.Options.BootstrapAdmin>();

        // Roller (admin + customer).
        foreach (var roleName in Config.RoleScopeSeed.Keys)
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));

        // Rol→scope map — eksik bundle scope'larını EKLE (additive merge, silme yok).
        // Eski "hiç satır yoksa doldur" guard'ı, bundle'a sonradan eklenen scope'u mevcut DB'ye
        // uygulamıyordu (reset gerekiyordu → 062/077 customer.write drift'i). Additive merge self-heal
        // eder; admin'in elle eklediği scope'lar korunur (yalnız ekleme, hiçbir satır silinmez).
        foreach (var (roleName, bundle) in Config.RoleScopeSeed)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            var existing = await db.RoleScopes.Where(rs => rs.RoleId == role.Id)
                .Select(rs => rs.Scope).ToListAsync(ct);
            foreach (var s in bundle.Distinct().Except(existing))
                db.RoleScopes.Add(new RoleScope { Id = Guid.NewGuid(), RoleId = role.Id, Scope = s });
        }
        await db.SaveChangesAsync(ct);

        // Bootstrap admin — email+parola config/secret'tan (kodda değil, DEĞERİ LOGLANMAZ — İlke VIII).
        // 005: rol admin (super-admin kırpıldı); secret eksikse crash YOK, uyarı logla (FR-004).
        var adminEmail = bootstrapAdmin.Email;
        var adminPassword = bootstrapAdmin.Password;
        var logger = sp.GetRequiredService<ILogger<SeedHostedService>>();
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("BootstrapAdmin config'i eksik — admin seed atlandı (yalnız customer kayıtları mümkün).");
        }
        else if (await userManager.FindByNameAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var created = await userManager.CreateAsync(admin, adminPassword);
            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, RoleAssignmentService.AdminRole);
                await userManager.AddClaimsAsync(admin,
                    [new Claim(Claims.Name, adminEmail), new Claim(Claims.Email, adminEmail)]);
                logger.LogInformation("Bootstrap admin seed edildi: {Email}", adminEmail);
            }
            else
            {
                logger.LogWarning("Bootstrap admin oluşturulamadı: {Errors}",
                    string.Join("; ", created.Errors.Select(e => e.Code)));
            }
        }

        // Backfill: rolsüz mevcut kullanıcılar → customer (feature öncesi kayıtlılar).
        foreach (var user in await userManager.Users.ToListAsync(ct))
            if ((await userManager.GetRolesAsync(user)).Count == 0)
                await userManager.AddToRoleAsync(user, RoleAssignmentService.CustomerRole);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor BuildDescriptor(ClientSeed client)
    {
        // 070: public seed istemci (external-admin-agent) secret'sız + PKCE; diğerleri confidential.
        var d = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.ClientSecret,
            DisplayName = client.DisplayName,
            ClientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
        };

        if (client.AllowAuthorizationCode)
        {
            d.Permissions.Add(Permissions.Endpoints.Authorization);
            d.Permissions.Add(Permissions.Endpoints.EndSession);
            d.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            d.Permissions.Add(Permissions.ResponseTypes.Code);
            d.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        if (client.AllowClientCredentials)
            d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        if (client.AllowRefreshToken)
            d.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        // Token ucu tüm grant'lar için gerekli.
        d.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var uri in client.RedirectUris)
            d.RedirectUris.Add(new Uri(uri));

        foreach (var uri in client.PostLogoutRedirectUris)
            d.PostLogoutRedirectUris.Add(new Uri(uri));

        // Scope izinleri (scp: prefix'li). openid dahil — OpenIddict openid'i zaten serbest sayar.
        foreach (var s in client.Scopes)
            d.Permissions.Add(Permissions.Prefixes.Scope + s);

        return d;
    }
}
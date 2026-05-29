using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PetalPal.Sample.Api.Data;
using PetalPal.Sample.Api.Models;
using SqlOS.AuthServer.Contracts;
using SqlOS.AuthServer.Extensions;
using SqlOS.AuthServer.Services;
using SqlOS.Extensions;
using SqlOS.Fga.Interfaces;
using SqlOS.Fga.Models;
using SqlOS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? "Server=localhost;Database=PetalPalSample;User Id=sa;Password=LocalDevPassword123!;TrustServerCertificate=True;";

builder.Services.AddDbContext<PetalPalDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PetalPal API",
        Version = "v1",
        Description = "A tiny OAuth-protected garden API shaped for ChatGPT through MCP Stack."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer token minted by the PetalPal SqlOS authorization server.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});
builder.Services.AddHealthChecks();

var publicOrigin = (builder.Configuration["PetalPal:PublicOrigin"] ?? "http://localhost:5098").TrimEnd('/');
var resource = builder.Configuration["PetalPal:Resource"] ?? $"{publicOrigin}/api";
var mcpStackRedirectUri = builder.Configuration["PetalPal:McpStackRedirectUri"] ?? "https://mcpstack.com/oauth/callback";
var localRedirectUri = builder.Configuration["PetalPal:LocalRedirectUri"] ?? $"{publicOrigin}/oauth/callback";
var allowedScopes = new List<string> { "openid", "profile", "email", "gardens.read", "gardens.write" };

builder.AddSqlOS<PetalPalDbContext>(options =>
{
    options.DashboardBasePath = "/sqlos";
    options.Fga.RootResourceId = PetalPalFga.RootResourceId;
    options.Fga.RootResourceName = "PetalPal Root";

    var auth = options.AuthServer;
    auth.Issuer = builder.Configuration["PetalPal:Issuer"] ?? $"{publicOrigin}/sqlos/auth";
    auth.PublicOrigin = publicOrigin;
    auth.DefaultAudience = resource;
    auth.EnableLocalPasswordAuth = true;

    auth.SeedAuthPage(page =>
    {
        page.PageTitle = "PetalPal";
        page.PageSubtitle = "A tiny garden that lets ChatGPT water plants as the signed-in user.";
        page.PrimaryColor = "#7cc950";
        page.AccentColor = "#38bdf8";
        page.BackgroundColor = "#f7fbf2";
        page.Layout = "split";
        page.EnablePasswordSignup = true;
        page.EnabledCredentialTypes = ["password"];
    });

    auth.SeedClient(client =>
    {
        client.ClientId = "petalpal-local";
        client.Name = "PetalPal local web";
        client.Description = "Local public PKCE client for trying PetalPal in a browser.";
        client.Audience = resource;
        client.RedirectUris = [localRedirectUri];
        client.AllowedScopes = allowedScopes;
        client.ClientType = "public_pkce";
        client.RequirePkce = true;
        client.IsFirstParty = true;
    });

    auth.SeedClient(client =>
    {
        client.ClientId = "petalpal-mcpstack-gateway";
        client.Name = "PetalPal MCP Stack Gateway";
        client.Description = "Downstream OAuth client used when MCP Stack Gateway fronts PetalPal for ChatGPT.";
        client.Audience = resource;
        client.RedirectUris = [mcpStackRedirectUri];
        client.AllowedScopes = allowedScopes;
        client.ClientType = "public_pkce";
        client.RequirePkce = true;
        client.IsFirstParty = false;
    });

    options.Fga.Seed(seed =>
    {
        seed.ResourceType(PetalPalFga.GardenResourceType, "PetalPal Garden", "Per-user garden root.");
        seed.ResourceType(PetalPalFga.PlantResourceType, "PetalPal Plant", "A plant inside a garden.");

        seed.Permission("perm_petalpal_garden_read", PetalPalFga.GardenReadPermission, "Read garden", PetalPalFga.GardenResourceType);
        seed.Permission("perm_petalpal_garden_write", PetalPalFga.GardenWritePermission, "Write garden", PetalPalFga.GardenResourceType);
        seed.Permission("perm_petalpal_plant_water", PetalPalFga.PlantWaterPermission, "Water plant", PetalPalFga.PlantResourceType);

        seed.Role("role_petalpal_owner", PetalPalFga.OwnerRole, "Garden owner", "Full control of a PetalPal garden.");
        seed.Role("role_petalpal_viewer", PetalPalFga.ViewerRole, "Garden viewer", "Read-only PetalPal garden access.");

        seed.RolePermission(PetalPalFga.OwnerRole, PetalPalFga.GardenReadPermission);
        seed.RolePermission(PetalPalFga.OwnerRole, PetalPalFga.GardenWritePermission);
        seed.RolePermission(PetalPalFga.OwnerRole, PetalPalFga.PlantWaterPermission);
        seed.RolePermission(PetalPalFga.ViewerRole, PetalPalFga.GardenReadPermission);
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");
app.MapSqlOS();

app.UseSqlOSAccessTokenValidation(options =>
{
    options.ExpectedAudience = resource;
    options.Realm = "PetalPal API";
    options.ResourceMetadataUrl = $"{publicOrigin}/.well-known/oauth-protected-resource";
    options.ShouldValidate = context => context.Request.Path.StartsWithSegments("/api/gardens");
});

app.MapGet("/sample/config", () => Results.Ok(new
{
    publicOrigin,
    issuer = $"{publicOrigin}/sqlos/auth",
    resource,
    protectedResourceMetadata = $"{publicOrigin}/.well-known/oauth-protected-resource",
    localClient = new { clientId = "petalpal-local", redirectUri = localRedirectUri },
    mcpStackGatewayClient = new { clientId = "petalpal-mcpstack-gateway", redirectUri = mcpStackRedirectUri },
    allowedScopes
}))
.WithName("get_petalpal_config")
.ExcludeFromDescription();

app.MapGet("/.well-known/oauth-protected-resource", () =>
{
    var payload = new Dictionary<string, object?>
    {
        ["resource"] = resource,
        ["authorization_servers"] = new[] { $"{publicOrigin}/sqlos/auth" },
        ["scopes_supported"] = allowedScopes,
        ["bearer_methods_supported"] = new[] { "header" },
        ["resource_documentation"] = $"{publicOrigin}/swagger"
    };

    return Results.Text(JsonSerializer.Serialize(payload), "application/json");
})
.ExcludeFromDescription();

var gardenApi = app.MapGroup("/api/gardens")
    .WithTags("Gardens");

gardenApi.MapGet("/me", async (
    HttpContext httpContext,
    PetalPalDbContext dbContext,
    ISqlOSFgaAuthService fgaAuthService,
    CancellationToken cancellationToken) =>
{
    var context = await PetalPalFga.EnsureGardenAsync(httpContext, dbContext, cancellationToken);
    if (context is null) return Results.Unauthorized();

    var access = await fgaAuthService.CheckAccessAsync(
        context.SubjectId,
        PetalPalFga.GardenReadPermission,
        context.Garden.ResourceId);
    if (!access.Allowed) return Results.Forbid();

    var plants = await dbContext.Plants
        .AsNoTracking()
        .Where(x => x.GardenId == context.Garden.Id)
        .OrderByDescending(x => x.CreatedAt)
        .Select(PetalPalDtos.ToPlantResponse)
        .ToListAsync(cancellationToken);

    return Results.Ok(new GardenResponse(
        context.Garden.Id,
        context.Garden.Name,
        context.Garden.ResourceId,
        context.SubjectId,
        plants));
})
.WithName("get_my_petalpal_garden")
.Produces<GardenResponse>(StatusCodes.Status200OK)
.WithOpenApi(operation =>
{
    operation.Summary = "Get the signed-in user's PetalPal garden";
    operation.Description = "Returns the OAuth user's garden and plants. MCP Stack Gateway can expose this as a read-only ChatGPT tool.";
    return operation;
});

gardenApi.MapGet("/plants", async (
    HttpContext httpContext,
    PetalPalDbContext dbContext,
    ISqlOSFgaAuthService fgaAuthService,
    CancellationToken cancellationToken) =>
{
    var context = await PetalPalFga.EnsureGardenAsync(httpContext, dbContext, cancellationToken);
    if (context is null) return Results.Unauthorized();

    var access = await fgaAuthService.CheckAccessAsync(
        context.SubjectId,
        PetalPalFga.GardenReadPermission,
        context.Garden.ResourceId);
    if (!access.Allowed) return Results.Forbid();

    var plants = await dbContext.Plants
        .AsNoTracking()
        .Where(x => x.GardenId == context.Garden.Id)
        .OrderByDescending(x => x.CreatedAt)
        .Select(PetalPalDtos.ToPlantResponse)
        .ToListAsync(cancellationToken);

    return Results.Ok(plants);
})
.WithName("list_petalpal_plants")
.Produces<IReadOnlyList<PlantResponse>>(StatusCodes.Status200OK)
.WithOpenApi(operation =>
{
    operation.Summary = "List plants in the signed-in user's garden";
    operation.Description = "Use when ChatGPT needs a compact inventory of the user's plants, moods, water counts, and notes.";
    return operation;
});

gardenApi.MapPost("/plants", async (
    CreatePlantRequest request,
    HttpContext httpContext,
    PetalPalDbContext dbContext,
    ISqlOSFgaAuthService fgaAuthService,
    CancellationToken cancellationToken) =>
{
    var context = await PetalPalFga.EnsureGardenAsync(httpContext, dbContext, cancellationToken);
    if (context is null) return Results.Unauthorized();

    var access = await fgaAuthService.CheckAccessAsync(
        context.SubjectId,
        PetalPalFga.GardenWritePermission,
        context.Garden.ResourceId);
    if (!access.Allowed) return Results.Forbid();

    var plantName = string.IsNullOrWhiteSpace(request.Name) ? "Tiny sprout" : request.Name.Trim();
    var plant = new Plant
    {
        Id = Guid.NewGuid(),
        GardenId = context.Garden.Id,
        Name = plantName,
        Mood = string.IsNullOrWhiteSpace(request.Mood) ? "cozy" : request.Mood.Trim(),
        Note = string.IsNullOrWhiteSpace(request.Note) ? "Newly planted by an agent." : request.Note.Trim(),
        CreatedAt = DateTime.UtcNow,
        ResourceId = $"petalpal:plant:{Guid.NewGuid():N}"
    };

    dbContext.Plants.Add(plant);
    dbContext.Set<SqlOSFgaResource>().Add(new SqlOSFgaResource
    {
        Id = plant.ResourceId,
        ParentId = context.Garden.ResourceId,
        Name = plant.Name,
        Description = "Plant resource created through the PetalPal API.",
        ResourceTypeId = PetalPalFga.PlantResourceType,
        IsActive = true
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/gardens/plants/{plant.Id}", PetalPalDtos.ToPlantResponse.Compile().Invoke(plant));
})
.WithName("create_petalpal_plant")
.Accepts<CreatePlantRequest>("application/json")
.Produces<PlantResponse>(StatusCodes.Status201Created)
.WithOpenApi(operation =>
{
    operation.Summary = "Create a plant in the signed-in user's garden";
    operation.Description = "Use after the user asks ChatGPT to add a small plant, reminder, or mood note to their PetalPal garden.";
    return operation;
});

gardenApi.MapPost("/plants/{plantId:guid}/water", async (
    Guid plantId,
    HttpContext httpContext,
    PetalPalDbContext dbContext,
    ISqlOSFgaAuthService fgaAuthService,
    CancellationToken cancellationToken) =>
{
    var context = await PetalPalFga.EnsureGardenAsync(httpContext, dbContext, cancellationToken);
    if (context is null) return Results.Unauthorized();

    var plant = await dbContext.Plants
        .FirstOrDefaultAsync(x => x.Id == plantId && x.GardenId == context.Garden.Id, cancellationToken);
    if (plant is null) return Results.NotFound();

    var access = await fgaAuthService.CheckAccessAsync(
        context.SubjectId,
        PetalPalFga.PlantWaterPermission,
        plant.ResourceId);
    if (!access.Allowed) return Results.Forbid();

    plant.WaterCount += 1;
    plant.LastWateredAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(PetalPalDtos.ToPlantResponse.Compile().Invoke(plant));
})
.WithName("water_petalpal_plant")
.Produces<PlantResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi(operation =>
{
    operation.Summary = "Water one PetalPal plant";
    operation.Description = "Use when ChatGPT needs to perform a tiny write action for a specific plant after user confirmation.";
    return operation;
});

gardenApi.MapGet("/plants/{plantId:guid}/fga-trace", async (
    Guid plantId,
    HttpContext httpContext,
    PetalPalDbContext dbContext,
    ISqlOSFgaAuthService fgaAuthService,
    CancellationToken cancellationToken) =>
{
    var context = await PetalPalFga.EnsureGardenAsync(httpContext, dbContext, cancellationToken);
    if (context is null) return Results.Unauthorized();

    var plant = await dbContext.Plants
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == plantId && x.GardenId == context.Garden.Id, cancellationToken);
    if (plant is null) return Results.NotFound();

    var trace = await fgaAuthService.TraceResourceAccessAsync(
        context.SubjectId,
        plant.ResourceId,
        PetalPalFga.PlantWaterPermission);

    return Results.Ok(trace);
})
.WithName("trace_petalpal_plant_access")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi(operation =>
{
    operation.Summary = "Trace the FGA decision for watering a plant";
    operation.Description = "Debug helper for the blog walkthrough: shows why SqlOS FGA allowed or denied the plant write.";
    return operation;
});

await EnsureDatabaseAsync(app);
app.Run();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PetalPalDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var bootstrapper = scope.ServiceProvider.GetRequiredService<SqlOSBootstrapper>();
    await bootstrapper.InitializeAsync();
}

internal static class PetalPalFga
{
    public const string RootResourceId = "petalpal_root";
    public const string GardenResourceType = "petalpal_garden";
    public const string PlantResourceType = "petalpal_plant";
    public const string GardenReadPermission = "PETALPAL_GARDEN_READ";
    public const string GardenWritePermission = "PETALPAL_GARDEN_WRITE";
    public const string PlantWaterPermission = "PETALPAL_PLANT_WATER";
    public const string OwnerRole = "petalpal_owner";
    public const string ViewerRole = "petalpal_viewer";
    private const string OwnerRoleId = "role_petalpal_owner";

    public static async Task<PetalPalRequestContext?> EnsureGardenAsync(
        HttpContext httpContext,
        PetalPalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = httpContext.GetSqlOSValidatedToken();
        if (token is null || string.IsNullOrWhiteSpace(token.UserId))
        {
            return null;
        }

        var subjectId = token.UserId.Trim();
        var displayName = DisplayNameFrom(token.Principal, subjectId);
        var email = token.Principal.FindFirst("email")?.Value;
        var now = DateTime.UtcNow;

        var subject = await dbContext.Set<SqlOSFgaSubject>().FindAsync([subjectId], cancellationToken);
        if (subject is null)
        {
            dbContext.Set<SqlOSFgaSubject>().Add(new SqlOSFgaSubject
            {
                Id = subjectId,
                SubjectTypeId = "user",
                OrganizationId = token.OrganizationId,
                DisplayName = displayName,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            subject.DisplayName = displayName;
            subject.UpdatedAt = now;
        }

        var fgaUser = await dbContext.Set<SqlOSFgaUser>().FindAsync([subjectId], cancellationToken);
        if (fgaUser is null)
        {
            dbContext.Set<SqlOSFgaUser>().Add(new SqlOSFgaUser
            {
                Id = subjectId,
                SubjectId = subjectId,
                Email = email,
                LastLoginAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            fgaUser.Email = email;
            fgaUser.LastLoginAt = now;
            fgaUser.UpdatedAt = now;
        }

        var garden = await dbContext.Gardens
            .Include(x => x.Plants)
            .FirstOrDefaultAsync(x => x.OwnerSubjectId == subjectId, cancellationToken);

        if (garden is null)
        {
            garden = new Garden
            {
                Id = Guid.NewGuid(),
                OwnerSubjectId = subjectId,
                Name = $"{displayName}'s Pocket Garden",
                ResourceId = $"petalpal:garden:{subjectId}",
                CreatedAt = now
            };
            dbContext.Gardens.Add(garden);
            dbContext.Set<SqlOSFgaResource>().Add(new SqlOSFgaResource
            {
                Id = garden.ResourceId,
                ParentId = RootResourceId,
                Name = garden.Name,
                Description = "Per-user PetalPal garden.",
                ResourceTypeId = GardenResourceType,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var existingGrant = await dbContext.Set<SqlOSFgaGrant>()
            .AnyAsync(x => x.SubjectId == subjectId && x.ResourceId == garden.ResourceId && x.RoleId == OwnerRoleId, cancellationToken);
        if (!existingGrant)
        {
            dbContext.Set<SqlOSFgaGrant>().Add(new SqlOSFgaGrant
            {
                Id = $"grant_petalpal_owner_{Guid.NewGuid():N}",
                SubjectId = subjectId,
                ResourceId = garden.ResourceId,
                RoleId = OwnerRoleId,
                Description = "Auto-provisioned owner grant for the signed-in user's garden.",
                EffectiveFrom = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PetalPalRequestContext(subjectId, garden);
    }

    private static string DisplayNameFrom(ClaimsPrincipal principal, string fallback)
        => principal.FindFirst("name")?.Value
            ?? principal.FindFirst("email")?.Value
            ?? fallback;
}

internal static class PetalPalDtos
{
    public static readonly System.Linq.Expressions.Expression<Func<Plant, PlantResponse>> ToPlantResponse =
        plant => new PlantResponse(
            plant.Id,
            plant.Name,
            plant.Mood,
            plant.Note,
            plant.WaterCount,
            plant.LastWateredAt,
            plant.ResourceId);
}

public sealed record CreatePlantRequest(string Name, string? Mood, string? Note);
public sealed record PlantResponse(Guid Id, string Name, string Mood, string Note, int WaterCount, DateTime? LastWateredAt, string ResourceId);
public sealed record GardenResponse(Guid Id, string Name, string ResourceId, string SubjectId, IReadOnlyList<PlantResponse> Plants);
public sealed record PetalPalRequestContext(string SubjectId, Garden Garden);

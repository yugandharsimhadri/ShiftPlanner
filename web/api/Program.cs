using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "DevCors";

// --- Services -----------------------------------------------------------

builder.Services.AddOpenApi();

// Entities have navigation properties that form cycles (Track <-> Subtrack <-> Employee, etc.).
// Ignore cycles rather than modeling separate read DTOs for every entity, given the MVP scope.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=shiftplanner.db"));

builder.Services
    .AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// Bearer tokens (not cookies) so both the React SPA and a future mobile client can use them.
builder.Services.Configure<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions>(
    Microsoft.AspNetCore.Identity.IdentityConstants.BearerScheme,
    options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromHours(12);
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Startup: create DB + seed ------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(db, userManager, logger);
}

// --- Pipeline -------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api").MapIdentityApi<IdentityUser>();

app.MapTeamsEndpoints();
app.MapAuthEndpoints();
app.MapTracksEndpoints();
app.MapSubtracksEndpoints();
app.MapLocationsEndpoints();
app.MapJobRolesEndpoints();
app.MapShiftTypesEndpoints();
app.MapHolidaysEndpoints();
app.MapRosterEndpoints();
app.MapExportEndpoints();
app.MapImportEndpoints();
app.MapCompOffsEndpoints();
app.MapReportsEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }

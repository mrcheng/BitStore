using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=bitstore;User=bitstore;Password=change-me;";
var serverVersion = GetConfiguredServerVersion(builder.Configuration);

// Add services to the container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddControllersWithViews();
builder.Services.AddCors(options =>
{
    options.AddPolicy("BitStoreApi", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(
        connectionString,
        serverVersion,
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
});
builder.Services.AddScoped<IPasswordHasher<BitStoreWeb.Net9.Models.AppUser>, PasswordHasher<BitStoreWeb.Net9.Models.AppUser>>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddHttpClient<IUserRegistrationNotifier, SlackUserRegistrationNotifier>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BitStore API",
        Version = "v1",
        Description = "External bucket access API. Use X-BitStore-Key for write operations."
    });

    options.AddSecurityDefinition("BitStoreApiKey", new OpenApiSecurityScheme
    {
        Description = "Bucket write API key. Send as header: X-BitStore-Key: {key}",
        Name = "X-BitStore-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var userExists = await db.Users.AnyAsync(x => x.Id == userId);
            if (!userExists)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    EnsureRecordFilterIndex(db, "IX_BucketRecords_BucketId_UpdatedUtc", "`BucketId`, `UpdatedUtc`");
    EnsureRecordFilterIndex(db, "IX_BucketRecords_BucketId_Value", "`BucketId`, `Value`");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("BitStoreApi");

app.UseAuthentication();
app.UseAuthorization();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/swagger"),
    branch =>
    {
        branch.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await next();
                return;
            }

            await context.ChallengeAsync();
        });
    });
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "BitStore API v1");
    options.RoutePrefix = "swagger";
});

app.MapStaticAssets();
app.MapControllers();
app.MapGet("/api", () => Results.Redirect("/swagger", permanent: false))
    .RequireAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

static ServerVersion GetConfiguredServerVersion(IConfiguration configuration)
{
    var configuredVersion = configuration["MySql:ServerVersion"];
    var version = Version.TryParse(configuredVersion, out var parsedVersion)
        ? parsedVersion
        : new Version(8, 0, 0);
    var serverType = configuration["MySql:ServerType"];
    return string.Equals(serverType, "MariaDb", StringComparison.OrdinalIgnoreCase)
        ? new MariaDbServerVersion(version)
        : new MySqlServerVersion(version);
}

static void EnsureRecordFilterIndex(AppDbContext db, string indexName, string columns)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State == System.Data.ConnectionState.Closed;
    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'BucketRecords'
              AND INDEX_NAME = @indexName;
            """;
        var indexNameParameter = existsCommand.CreateParameter();
        indexNameParameter.ParameterName = "@indexName";
        indexNameParameter.Value = indexName;
        existsCommand.Parameters.Add(indexNameParameter);

        var exists = Convert.ToInt32(existsCommand.ExecuteScalar()) > 0;
        if (exists)
        {
            return;
        }

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE INDEX `{indexName}` ON `BucketRecords` ({columns});";
        createCommand.ExecuteNonQuery();
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

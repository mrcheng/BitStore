using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=bitstore.db";
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

// Add services to the container.
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
    if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(
            connectionString,
            sqlServerOptions =>
            {
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});
builder.Services.AddScoped<IPasswordHasher<BitStoreWeb.Net9.Models.AppUser>, PasswordHasher<BitStoreWeb.Net9.Models.AppUser>>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
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
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
app.MapGet("/demo", () => Results.Redirect("/demo/index.html", permanent: false));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

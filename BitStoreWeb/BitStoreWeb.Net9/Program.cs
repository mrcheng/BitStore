using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=bitstore.db"));
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
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS "Buckets" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Buckets" PRIMARY KEY AUTOINCREMENT,
            "OwnerUserId" INTEGER NOT NULL,
            "Name" TEXT NOT NULL,
            "Description" TEXT NULL,
            "Slug" TEXT NOT NULL,
            "WriteApiKey" TEXT NOT NULL,
            "CreatedUtc" TEXT NOT NULL,
            "UpdatedUtc" TEXT NOT NULL,
            CONSTRAINT "FK_Buckets_Users_OwnerUserId" FOREIGN KEY ("OwnerUserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS "BucketRecords" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_BucketRecords" PRIMARY KEY AUTOINCREMENT,
            "BucketId" INTEGER NOT NULL,
            "Value" TEXT NULL,
            "CreatedUtc" TEXT NOT NULL,
            "UpdatedUtc" TEXT NOT NULL,
            CONSTRAINT "FK_BucketRecords_Buckets_BucketId" FOREIGN KEY ("BucketId") REFERENCES "Buckets" ("Id") ON DELETE CASCADE
        );
        """);
    db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Buckets_OwnerUserId_Name" ON "Buckets" ("OwnerUserId", "Name");""");
    db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Buckets_Slug" ON "Buckets" ("Slug");""");
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_BucketRecords_BucketId_CreatedUtc" ON "BucketRecords" ("BucketId", "CreatedUtc");""");
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
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "BitStore API v1");
    options.RoutePrefix = "swagger";
});

app.MapStaticAssets();
app.MapControllers();
app.MapGet("/api", () => Results.Redirect("/swagger", permanent: false));
app.MapGet("/api/", () => Results.Redirect("/swagger", permanent: false));
app.MapGet("/demo", (IWebHostEnvironment env) =>
{
    var demoPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "testbucket-1-view.html"));
    return File.Exists(demoPath)
        ? Results.File(demoPath, "text/html; charset=utf-8")
        : Results.NotFound("Demo page not found.");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

using Capableza.Web.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<AuthService>();

try
{
    string projectId = builder.Configuration["Firebase:ProjectId"] ?? "skills-audit-db";
    builder.Services.AddSingleton<FirestoreDb>(provider => FirestoreDb.Create(projectId));
    builder.Services.AddScoped<FirestoreService>();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: Failed to initialize Firestore: {ex.Message}");
    throw;
}

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole("employee", "admin"));
});

var app = builder.Build();

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

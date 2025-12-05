using Microsoft.EntityFrameworkCore;
using LMS.Models;
using OfficeOpenXml;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure EPPlus License - Force set for EPPlus 8+
try 
{
    // Method 1: Try direct assignment
    var licenseField = typeof(ExcelPackage).GetProperty("License", 
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
    
    if (licenseField != null)
    {
        // For newer versions, try to set via reflection
        var licenseObj = licenseField.GetValue(null);
        var setMethod = licenseObj?.GetType().GetMethod("SetLicense");
        if (setMethod != null)
        {
            // Try to find NonCommercial enum value
            var assembly = typeof(ExcelPackage).Assembly;
            var licenseType = assembly.GetTypes().FirstOrDefault(t => t.Name == "LicenseType");
            if (licenseType != null)
            {
                var nonCommercial = Enum.Parse(licenseType, "NonCommercial");
                setMethod.Invoke(licenseObj, new[] { nonCommercial });
            }
        }
    }
}
catch 
{
    // Fallback: Use environment variable approach
    Environment.SetEnvironmentVariable("EPPlus:License", "NonCommercial");
}

// Add services to the container.
builder.Services.AddDbContext<LmsSystemContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout 30 phút
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add session middleware
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

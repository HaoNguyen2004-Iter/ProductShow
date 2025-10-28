using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services;
using SPMH.Services.Executes;
using SPMH.Services.Executes.Accounts;
using SPMH.Services.Executes.Brands;
using SPMH.Services.Executes.Products;
using SPMH.Services.Executes.Storage;
using SPMH.Services.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<ImageStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var physicalRoot = Path.Combine(env.WebRootPath, "uploads", "products");
    var publicBase = "/uploads/products";
    return new ImageStorage(physicalRoot, publicBase);
});

builder.Services.AddScoped<ProductOne>(sp =>
{
    var db = sp.GetRequiredService<AppDbContext>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new ProductOne(db, env.WebRootPath);
});


builder.Services.AddScoped<ProductCommand>();
builder.Services.AddScoped<ProductMany>();
builder.Services.AddScoped<ProductModel>();

builder.Services.AddScoped<BrandMany>();
builder.Services.AddScoped<BrandModel>();
builder.Services.AddScoped<AccountModel>();
builder.Services.AddScoped<AccountOne>();
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Account/Login";
    });
builder.Services.AddAuthorization();


var app = builder.Build();

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
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
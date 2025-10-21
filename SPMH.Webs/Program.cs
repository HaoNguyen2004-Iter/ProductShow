using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services;
using SPMH.Services.Executes;
using SPMH.Services.Executes.Brands;
using SPMH.Services.Executes.Products;
using SPMH.Services.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//Đăng ký DI cho ProductService
builder.Services.AddScoped<ProductCommand>();
builder.Services.AddScoped<ProductMany>();
builder.Services.AddScoped<ProductModel>();
builder.Services.AddScoped<ProductOne>();
builder.Services.AddScoped<BrandMany>();
builder.Services.AddScoped<BrandModel>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Product}/{action=Index}/{id?}");

app.Run();

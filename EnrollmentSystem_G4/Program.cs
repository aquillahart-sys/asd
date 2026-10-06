using EnrollmentSystem_G4.Data;
using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
        
// Register MySqlConnection using the connection string from appsettings.json
builder.Services.AddTransient<MySqlConnection>(_ =>
    new MySqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<DatabaseHelper>();

var app = builder.Build();

    
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Register DatabaseHelper as a Scoped service

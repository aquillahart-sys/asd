using EnrollmentSystem_G4.Data;
using Microsoft.AspNetCore.Authorization;
using EnrollmentSystem_G4.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DatabaseHelper>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<UserStatusCookieEvents>();
builder.Services
    .AddAuthentication("EnrollmentCookie")
    .AddCookie("EnrollmentCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "EnrollmentSystem.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.EventsType = typeof(UserStatusCookieEvents);
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (args.Length == 2 && args[0] == "--reset-password")
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
    var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
    var auditLogger = scope.ServiceProvider.GetRequiredService<AuditLogger>();
    string usernameToReset = args[1].Trim();
    object? userIdValue = database.ExecuteScalar(
        "SELECT user_id FROM users WHERE username = @Username",
        new Dictionary<string, object> { { "@Username", usernameToReset } });
    if (userIdValue == null || userIdValue == DBNull.Value)
    {
        throw new InvalidOperationException("The specified user account was not found.");
    }

    int userIdToReset = Convert.ToInt32(userIdValue);
    Console.Write("New password (minimum 12 characters): ");
    string replacementPassword = ReadPassword();
    if (replacementPassword.Length < 12)
    {
        throw new InvalidOperationException("The password must contain at least 12 characters.");
    }

    var account = new User { UserId = userIdToReset, Username = usernameToReset };
    passwordService.HashPassword(account, replacementPassword);
    database.ExecuteInTransaction((connection, transaction) =>
    {
        database.ExecuteNonQuery(
            connection,
            transaction,
            @"UPDATE users
              SET password_hash = @PasswordHash, password_salt = @PasswordSalt
              WHERE user_id = @UserId",
            new Dictionary<string, object>
            {
                { "@PasswordHash", account.PasswordHash },
                { "@PasswordSalt", account.PasswordSalt },
                { "@UserId", account.UserId }
            });
        auditLogger.Log(
            connection,
            transaction,
            null,
            "USER_PASSWORD_RESET",
            "User",
            account.UserId.ToString(),
            "Password reset through the local administrative setup command.");
        return account.UserId;
    });
    Console.WriteLine($"Password reset for '{usernameToReset}'.");
    return;
}

if (args.Contains("--bootstrap-admin", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
    var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
    var auditLogger = scope.ServiceProvider.GetRequiredService<AuditLogger>();

    if (Convert.ToInt32(database.ExecuteScalar("SELECT COUNT(*) FROM users") ?? 0) != 0)
    {
        throw new InvalidOperationException("Administrator bootstrap is available only when no user accounts exist.");
    }

    Console.Write("Administrator username: ");
    string username = (Console.ReadLine() ?? string.Empty).Trim();
    if (username.Length < 3 || username.Length > 50)
    {
        throw new InvalidOperationException("The username must contain between 3 and 50 characters.");
    }

    Console.Write("Administrator first name: ");
    string firstName = (Console.ReadLine() ?? string.Empty).Trim();
    Console.Write("Administrator last name: ");
    string lastName = (Console.ReadLine() ?? string.Empty).Trim();
    Console.Write("Administrator email: ");
    string email = (Console.ReadLine() ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 50 ||
        string.IsNullOrWhiteSpace(lastName) || lastName.Length > 50 ||
        string.IsNullOrWhiteSpace(email) || email.Length > 100)
    {
        throw new InvalidOperationException("Enter a valid first name, last name, and email.");
    }

    Console.Write("Administrator password (minimum 12 characters): ");
    string password = ReadPassword();
    if (password.Length < 12)
    {
        throw new InvalidOperationException("The password must contain at least 12 characters.");
    }

    var administrator = new User
    {
        Username = username,
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Role = "Administrator"
    };
    passwordService.HashPassword(administrator, password);
    database.ExecuteInTransaction((connection, transaction) =>
    {
        database.ExecuteNonQuery(
            connection,
            transaction,
            @"INSERT INTO users
                (username, password_hash, password_salt, first_name, last_name, email, role, status, is_active)
              VALUES
                (@Username, @PasswordHash, @PasswordSalt, @FirstName, @LastName, @Email, @Role, 'Active', 1)",
            new Dictionary<string, object>
            {
                { "@Username", administrator.Username },
                { "@PasswordHash", administrator.PasswordHash },
                { "@PasswordSalt", administrator.PasswordSalt },
                { "@FirstName", administrator.FirstName },
                { "@LastName", administrator.LastName },
                { "@Email", administrator.Email },
                { "@Role", administrator.Role }
            });
        int userId = Convert.ToInt32(database.ExecuteScalar(
            connection,
            transaction,
            "SELECT LAST_INSERT_ID()"));
        auditLogger.Log(
            connection,
            transaction,
            userId,
            "USER_CREATE",
            "User",
            userId.ToString(),
            $"Bootstrapped initial Administrator account '{username}'.");
        return userId;
    });

    Console.WriteLine($"Administrator account '{username}' created. Sign in to create Registrar and Cashier accounts.");
    return;
}

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

static string ReadPassword()
{
    var password = new System.Text.StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return password.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}

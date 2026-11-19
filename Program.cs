using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".EmployeePortal.Session";
    options.Cookie.HttpOnly = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddSingleton<SqlStore>();
var app = builder.Build();
if (args.Contains("--init-db"))
{
    var db = app.Services.GetRequiredService<SqlStore>();
    await db.Initialize();
    var password = builder.Configuration["Demo:Password"];
    if (password is { Length: >= 12 })
    {
        var accounts = new[] { ("manager","Manager"), ("employee","Employee"), ("hr","HR") };
        var hasher = new PasswordHasher<string>();
        foreach (var (login, role) in accounts)
            await db.Run(@"IF NOT EXISTS(SELECT 1 FROM Staff WHERE Login=@login)
INSERT Staff(Login,PasswordHash,Role,ManagerId) VALUES(@login,@hash,@role,(SELECT StaffId FROM Staff WHERE Login='manager' AND @role='Employee'))",
                ("@login",login),("@hash",hasher.HashPassword(login,password)),("@role",role));
    }
    return;
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
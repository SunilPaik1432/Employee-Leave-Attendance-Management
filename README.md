# Employee Leave & Attendance Management System

Attendance, leave approvals and HR reporting. Built with C#, ASP.NET Core MVC, Razor and SQL Server, with custom session authentication.

## Run locally

Use the .NET 7 SDK and SQL Server. The default is SQL Server LocalDB with database `EmployeePortal`. Set `ConnectionStrings__Default` to use another instance. Initialization requires database creation privileges.

```powershell
$env:Demo__Password = Read-Host 'Set a demo password with at least 12 characters'
dotnet run -- --init-db
dotnet run -- --urls http://localhost:5203
```

Open [http://localhost:5203](http://localhost:5203).

## Demo workflow

Sign in as `employee` to mark attendance and submit leave. Approve the request as `manager`, then as `hr`. The HR dashboard includes a 30-day attendance report.

The seeded accounts use your local demo password.

This portfolio demo targets the installed .NET 7 SDK. Upgrade to a supported runtime before public hosting.

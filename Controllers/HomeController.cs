using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Data.SqlClient;

public class HomeController : Controller
{
    private SqlStore Db { get; }
    public HomeController(SqlStore db) => Db = db;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.RouteValues["action"] != nameof(Login) && HttpContext.Session.GetInt32("userId") is null)
        {
            context.Result = RedirectToAction(nameof(Login));
            return;
        }
        var result = await next();
        if (result.Exception is SqlException error)
        {
            result.ExceptionHandled = true;
            if (error.Number is >= 50000 and < 50100)
                result.Result = Feedback(error.Message);
            else if (error.Number is 2601 or 2627)
                result.Result = Feedback("A record with these values already exists.");
            else
            {
                Response.StatusCode = 503;
                result.Result = View("Unavailable");
            }
        }
    }

    private IActionResult Feedback(string message)
    {
        TempData["Feedback"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = HttpContext.Session.GetInt32("userId")!.Value;
var role = HttpContext.Session.GetString("role");
var leaves = await Db.Read(@"SELECT l.LeaveId AS ID,u.Login AS Employee,l.StartDate,l.EndDate,l.Reason,l.State
FROM LeaveRequest l JOIN Staff u ON u.StaffId=l.StaffId
WHERE l.StaffId=@user OR @role='HR' OR (@role='Manager' AND u.ManagerId=@user) ORDER BY l.LeaveId DESC",("@user",user),("@role",role));
var attendance = await Db.Read("SELECT TOP (60) WorkDate,CheckInUtc,CheckOutUtc FROM Attendance WHERE StaffId=@user ORDER BY WorkDate DESC",("@user",user));
var grids = new List<Grid> { new("Leave requests", leaves), new("My attendance", attendance) };
if (role == "HR")
    grids.Add(new("HR summary · last 30 days (UTC)", await Db.Read(@"SELECT s.Login AS Employee, COUNT(a.WorkDate) AS DaysPresent,
COALESCE(SUM(DATEDIFF(minute,a.CheckInUtc,a.CheckOutUtc)),0) AS CompletedMinutes FROM Staff s LEFT JOIN Attendance a
ON s.StaffId=a.StaffId AND a.WorkDate >= DATEADD(day,-29,CONVERT(date,SYSUTCDATETIME())) GROUP BY s.Login ORDER BY s.Login")));
return View(new Dashboard(grids));
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login([Required,StringLength(100)] string login,[Required,StringLength(128)] string password)
    {
        if (ModelState.IsValid)
        {
            var rows = await Db.Read("SELECT StaffId,Login,PasswordHash,Role FROM Staff WHERE Login=@login",("@login",login.Trim()));
            if (rows.SingleOrDefault() is { } account &&
                new PasswordHasher<string>().VerifyHashedPassword((string)account["Login"]!, (string)account["PasswordHash"]!,password) != PasswordVerificationResult.Failed)
            {
                HttpContext.Session.Clear();
                HttpContext.Session.SetInt32("userId",(int)account["StaffId"]!);
                HttpContext.Session.SetString("role",(string)account["Role"]!);
                return RedirectToAction(nameof(Index));
            }
        }
        ViewData["Error"] = "Invalid login or password.";
        return View();
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
public async Task<IActionResult> Attendance(string actionType)
{
    var user = HttpContext.Session.GetInt32("userId")!.Value;
    if (actionType != "in" && actionType != "out") return BadRequest();
    await Db.Procedure("dbo.MarkAttendance",("@StaffId",user),("@CheckIn",actionType=="in"));
    return Feedback("Attendance saved.");
}
[HttpPost]
public async Task<IActionResult> RequestLeave(DateTime start, DateTime end, [Required,StringLength(250)] string reason)
{
    var user = HttpContext.Session.GetInt32("userId")!.Value;
    if (!ModelState.IsValid || start.Date < DateTime.Today || end.Date < start.Date || (end.Date-start.Date).Days > 90)
        return Feedback("Choose valid leave dates and enter a reason.");
    await Db.Procedure("dbo.ApplyLeave",("@StaffId",user),("@Start",start.Date),("@End",end.Date),("@Reason",reason.Trim()));
    return Feedback("Leave request submitted.");
}
[HttpPost]
public async Task<IActionResult> Decision(int id, string decision)
{
    var user = HttpContext.Session.GetInt32("userId")!.Value;
    var role = HttpContext.Session.GetString("role");
    if ((role != "Manager" && role != "HR") || (decision != "approve" && decision != "reject")) return StatusCode(403);
    await Db.Procedure("dbo.DecideLeave",("@StaffId",user),("@Id",id),("@Approve",decision=="approve"));
    return Feedback("Decision recorded.");
}
}
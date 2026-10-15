using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Data;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.ViewModels;
namespace TrafficFineManager.Web.Controllers;
[Authorize]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var query = db.TrafficFines.AsNoTracking();
        var model = new DashboardVm(await db.Vehicles.CountAsync(ct),
            await query.CountAsync(x => x.Status == FineStatus.ManagerApproval, ct),
            await query.CountAsync(x => x.Status == FineStatus.FinanceApproval, ct),
            await query.CountAsync(x => x.Status == FineStatus.Completed, ct),
            await query.Where(x => x.Status == FineStatus.ManagerApproval || x.Status == FineStatus.FinanceApproval).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            await query.Include(x => x.Vehicle).OrderByDescending(x => x.CreatedAt).Take(6).ToListAsync(ct));
        return View(model);
    }
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }
}

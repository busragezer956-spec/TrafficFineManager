using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Data;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.ViewModels;
namespace TrafficFineManager.Web.Controllers;
[Authorize]
public class VehiclesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        ViewBag.Search = search;
        var query = db.Vehicles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.Plate.Contains(term) || x.Brand.Contains(term) || x.Model.Contains(term)); }
        return View(await query.OrderBy(x => x.Plate).Take(200).ToListAsync(ct));
    }
    [Authorize(Roles = Roles.Operator), HttpGet]
    public IActionResult Create() => View(new VehicleVm());
    [Authorize(Roles = Roles.Operator), HttpPost]
    public async Task<IActionResult> Create(VehicleVm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(input);
        var plate = string.Concat(input.Plate.Where(c => c != ' ' && c != '-')).ToUpperInvariant();
        if (plate.Length < 3) ModelState.AddModelError(nameof(input.Plate), "Geçerli bir plaka girin.");
        if (await db.Vehicles.AnyAsync(x => x.Plate == plate, ct)) ModelState.AddModelError(nameof(input.Plate), "Bu plaka zaten kayıtlı.");
        if (!ModelState.IsValid) return View(input);
        db.Vehicles.Add(new Vehicle { Plate = plate, Type = input.Type, Brand = input.Brand.Trim(), Model = input.Model.Trim() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { ModelState.AddModelError(nameof(input.Plate), "Bu plaka zaten kayıtlı."); return View(input); }
        TempData["Success"] = "Araç kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
}

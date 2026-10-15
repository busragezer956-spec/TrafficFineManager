using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Data;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.Services;
using TrafficFineManager.Web.ViewModels;
namespace TrafficFineManager.Web.Controllers;
[Authorize]
public class FinesController(AppDbContext db, FineWorkflowService workflow) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, FineStatus? status, bool mine = false, int page = 1, CancellationToken ct = default)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest();
        if (mine)
        {
            if (User.IsInRole(Roles.Manager)) status = FineStatus.ManagerApproval;
            else if (User.IsInRole(Roles.Finance)) status = FineStatus.FinanceApproval;
            else return Forbid();
        }
        var query = db.TrafficFines.AsNoTracking().Include(x => x.Vehicle).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.ReferenceNumber.Contains(term) || x.Vehicle.Plate.Contains(term)); }
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 15d)));
        return View(new FineListVm { Search = search, Status = status, Mine = mine, Page = page, Total = total,
            Items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * 15).Take(15).ToListAsync(ct) });
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var fine = await db.TrafficFines.AsNoTracking().Include(x => x.Vehicle).Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, ct);
        return fine is null ? NotFound() : View(fine);
    }
    [Authorize(Roles = Roles.Operator), HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) => View("Form", await PopulateAsync(new FineVm(), ct));
    [Authorize(Roles = Roles.Operator), HttpPost]
    public async Task<IActionResult> Create(FineVm input, CancellationToken ct)
    {
        input.Id = 0;
        if (ModelState.IsValid)
        {
            try { var id = await workflow.CreateAsync(input, User, ct); TempData["Success"] = "Ceza kaydı oluşturuldu. Hazır olduğunda onaya gönderebilirsiniz."; return RedirectToAction(nameof(Details), new { id }); }
            catch (WorkflowException ex) { ModelState.AddModelError("", ex.Message); }
        }
        return View("Form", await PopulateAsync(input, ct));
    }
    [Authorize(Roles = Roles.Operator), HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var fine = await db.TrafficFines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (fine is null) return NotFound();
        if (!FineWorkflowService.CanEdit(fine, User)) return Forbid();
        return View("Form", await PopulateAsync(new FineVm {
            Id = fine.Id, VehicleId = fine.VehicleId, ReferenceNumber = fine.ReferenceNumber, ViolationDate = fine.ViolationDate,
            Amount = fine.Amount, Description = fine.Description, Version = fine.Version }, ct));
    }
    [Authorize(Roles = Roles.Operator), HttpPost]
    public async Task<IActionResult> Edit(int id, FineVm input, CancellationToken ct)
    {
        if (id != input.Id) return BadRequest();
        if (ModelState.IsValid)
        {
            try { await workflow.EditAsync(input, User, ct); TempData["Success"] = "Ceza bilgileri güncellendi."; return RedirectToAction(nameof(Details), new { id }); }
            catch (WorkflowException ex) { ModelState.AddModelError("", ex.Message); }
        }
        return View("Form", await PopulateAsync(input, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Decide(DecisionVm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Geçersiz işlem bilgileri. Açıklama en fazla 1000 karakter olabilir."; }
        else
        {
            try { await workflow.ExecuteAsync(input, User, ct); TempData["Success"] = "İşlem kaydedildi ve geçmiş güncellendi."; }
            catch (WorkflowException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Details), new { id = input.Id });
    }
    private async Task<FineVm> PopulateAsync(FineVm vm, CancellationToken ct)
    {
        vm.Vehicles = await db.Vehicles.AsNoTracking().OrderBy(x => x.Plate)
            .Select(x => new SelectListItem(x.Plate + " · " + x.Brand + " " + x.Model, x.Id.ToString())).ToListAsync(ct);
        return vm;
    }
}

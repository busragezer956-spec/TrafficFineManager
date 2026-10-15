using System.Security.Claims;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Data;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.ViewModels;

namespace TrafficFineManager.Web.Services;
public class WorkflowException(string message) : Exception(message);

// Bütün durum değişiklikleri tek noktadan geçer; kayıt ve geçmiş tek SaveChanges ile atomiktir.
public class FineWorkflowService(AppDbContext db, TimeProvider clock)
{
    public static bool CanEdit(TrafficFine fine, ClaimsPrincipal user) =>
        user.IsInRole(Roles.Operator) && fine.CreatedById == user.FindFirstValue(ClaimTypes.NameIdentifier)
        && fine.Status is FineStatus.New or FineStatus.Rejected;

    public static bool CanDecide(TrafficFine fine, ClaimsPrincipal user) =>
        (fine.Status == FineStatus.ManagerApproval && user.IsInRole(Roles.Manager)) ||
        (fine.Status == FineStatus.FinanceApproval && user.IsInRole(Roles.Finance));

    public async Task<int> CreateAsync(FineVm input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Require(user.IsInRole(Roles.Operator), "Ceza oluşturma yetkiniz yok.");
        await ValidateAsync(input, ct);
        var fine = new TrafficFine {
            VehicleId = input.VehicleId, ReferenceNumber = NormalizeReference(input.ReferenceNumber),
            ViolationDate = input.ViolationDate, Amount = input.Amount, Description = input.Description.Trim(),
            CreatedById = UserId(user), CreatedAt = clock.GetUtcNow()
        };
        AddHistory(fine, user, WorkflowAction.Created, null, "Ceza kaydı oluşturuldu.");
        db.TrafficFines.Add(fine);
        await SaveAsync(ct);
        return fine.Id;
    }
    public async Task EditAsync(FineVm input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var fine = await GetAsync(input.Id, ct);
        Require(CanEdit(fine, user), "Yalnızca size ait yeni veya reddedilmiş kayıtları düzenleyebilirsiniz.");
        CheckVersion(fine, input.Version);
        await ValidateAsync(input, ct);
        var changes = new List<string>();
        if (fine.VehicleId != input.VehicleId)
        {
            var plates = await db.Vehicles.Where(x => x.Id == fine.VehicleId || x.Id == input.VehicleId)
                .ToDictionaryAsync(x => x.Id, x => x.Plate, ct);
            changes.Add($"Araç: {plates[fine.VehicleId]} → {plates[input.VehicleId]}");
        }
        var reference = NormalizeReference(input.ReferenceNumber);
        if (fine.ReferenceNumber != reference) changes.Add($"Tutanak: {fine.ReferenceNumber} → {reference}");
        if (fine.ViolationDate != input.ViolationDate) changes.Add($"İhlal tarihi: {fine.ViolationDate:dd.MM.yyyy} → {input.ViolationDate:dd.MM.yyyy}");
        if (fine.Amount != input.Amount)
        {
            var culture = CultureInfo.GetCultureInfo("tr-TR");
            changes.Add($"Tutar: {fine.Amount.ToString("N2", culture)} ₺ → {input.Amount.ToString("N2", culture)} ₺");
        }
        if (fine.Description != input.Description.Trim()) changes.Add("Açıklama değiştirildi.");
        Require(changes.Count > 0, "Kaydedilecek bir değişiklik bulunamadı.");
        fine.VehicleId = input.VehicleId;
        fine.ReferenceNumber = NormalizeReference(input.ReferenceNumber);
        fine.ViolationDate = input.ViolationDate;
        fine.Amount = input.Amount;
        fine.Description = input.Description.Trim();
        AddHistory(fine, user, WorkflowAction.Edited, fine.Status, string.Join("\n", changes));
        fine.Version = Guid.NewGuid();
        await SaveAsync(ct);
    }
    public async Task ExecuteAsync(DecisionVm input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var fine = await GetAsync(input.Id, ct);
        CheckVersion(fine, input.Version);
        Require(input.Note?.Length is not > 1000, "Açıklama en fazla 1000 karakter olabilir.");
        var previous = fine.Status;
        switch (input.Action)
        {
            case WorkflowAction.Submitted:
                Require(CanEdit(fine, user), "Bu kayıt onaya gönderilemez.");
                fine.Status = FineStatus.ManagerApproval;
                break;
            case WorkflowAction.Approved:
                Require(CanDecide(fine, user), "Bu aşamada onay yetkiniz yok.");
                fine.Status = previous == FineStatus.ManagerApproval ? FineStatus.FinanceApproval : FineStatus.Completed;
                break;
            case WorkflowAction.Rejected:
                Require(CanDecide(fine, user), "Bu aşamada ret yetkiniz yok.");
                Require(!string.IsNullOrWhiteSpace(input.Note), "Ret nedeni zorunludur.");
                fine.Status = FineStatus.Rejected;
                break;
            default: throw new WorkflowException("Geçersiz işlem.");
        }
        AddHistory(fine, user, input.Action, previous, input.Note?.Trim());
        fine.Version = Guid.NewGuid();
        await SaveAsync(ct);
    }
    private async Task ValidateAsync(FineVm input, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(input.ReferenceNumber) && input.ReferenceNumber.Trim().Length <= 50, "Geçerli bir tutanak numarası girin.");
        Require(!string.IsNullOrWhiteSpace(input.Description) && input.Description.Trim().Length <= 1000, "Açıklama zorunludur (en fazla 1000 karakter).");
        Require(input.Amount > 0 && input.Amount <= 999999999.99m && decimal.Round(input.Amount, 2) == input.Amount, "Pozitif ve en fazla iki ondalıklı tutar girin.");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Europe/Istanbul").DateTime);
        Require(input.ViolationDate != default && input.ViolationDate <= today, "İhlal tarihi gelecekte olamaz.");
        Require(await db.Vehicles.AnyAsync(x => x.Id == input.VehicleId, ct), "Seçilen araç bulunamadı.");
        var reference = NormalizeReference(input.ReferenceNumber);
        Require(!await db.TrafficFines.AnyAsync(x => x.Id != input.Id && x.ReferenceNumber == reference, ct), "Bu tutanak numarası zaten kayıtlı.");
    }
    private async Task<TrafficFine> GetAsync(int id, CancellationToken ct) =>
        await db.TrafficFines.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new WorkflowException("Ceza kaydı bulunamadı.");
    private static string NormalizeReference(string reference) => reference.Trim().ToUpperInvariant();
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new WorkflowException("Oturum bulunamadı.");
    private void AddHistory(TrafficFine fine, ClaimsPrincipal user, WorkflowAction action, FineStatus? previous, string? note) =>
        fine.History.Add(new ApprovalHistory {
            ActorId = UserId(user), ActorName = user.Identity?.Name ?? "Kullanıcı", OccurredAt = clock.GetUtcNow(),
            Action = action, PreviousStatus = previous, NewStatus = fine.Status, Note = note
        });
    private static void Require(bool condition, string message) { if (!condition) throw new WorkflowException(message); }
    private static void CheckVersion(TrafficFine fine, Guid version) => Require(fine.Version == version, "Kayıt başka bir işlemle değişti. Sayfayı yenileyip tekrar deneyin.");
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowException("Kayıt başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyin."); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new WorkflowException("Aynı tutanak numarasıyla kayıt bulunuyor."); }
    }
}

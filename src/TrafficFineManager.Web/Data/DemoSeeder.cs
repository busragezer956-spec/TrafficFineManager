using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.Services;
using TrafficFineManager.Web.ViewModels;
namespace TrafficFineManager.Web.Data;
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var password = configuration["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Demo:Password eksik. README'deki user-secrets komutunu çalıştırın.");
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Operator, Roles.Manager, Roles.Finance })
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
        foreach (var pair in new[] { ("operasyon@demo.local", Roles.Operator), ("yonetici@demo.local", Roles.Manager), ("finans@demo.local", Roles.Finance) })
        {
            var user = await users.FindByEmailAsync(pair.Item1);
            if (user is null) { user = new IdentityUser { UserName = pair.Item1, Email = pair.Item1, EmailConfirmed = true }; Check(await users.CreateAsync(user, password)); }
            if (!await users.IsInRoleAsync(user, pair.Item2)) Check(await users.AddToRoleAsync(user, pair.Item2));
        }
        if (!await db.Vehicles.AnyAsync())
        {
            db.Vehicles.AddRange(
                new Vehicle { Plate = "34ABC123", Type = VehicleType.Passenger, Brand = "Renault", Model = "Clio" },
                new Vehicle { Plate = "76AAR246", Type = VehicleType.Tractor, Brand = "Mercedes-Benz", Model = "Actros" },
                new Vehicle { Plate = "76AAD580", Type = VehicleType.Trailer, Brand = "Krone", Model = "Cool Liner" },
                new Vehicle { Plate = "34KRL456", Type = VehicleType.Rental, Brand = "Fiat", Model = "Egea" });
            await db.SaveChangesAsync();
        }
        if (await db.TrafficFines.AnyAsync()) return;
        var workflow = services.GetRequiredService<FineWorkflowService>();
        async Task<ClaimsPrincipal> Principal(string email, string role)
        {
            var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException();
            return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, email), new Claim(ClaimTypes.Role, role) }, "seed"));
        }
        var op = await Principal("operasyon@demo.local", Roles.Operator);
        var manager = await Principal("yonetici@demo.local", Roles.Manager);
        var finance = await Principal("finans@demo.local", Roles.Finance);
        var vehicleId = await db.Vehicles.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        for (var i = 0; i < 5; i++)
        {
            var id = await workflow.CreateAsync(new FineVm { VehicleId = vehicleId, ReferenceNumber = $"DEMO-2026-{i + 1:000}", Amount = 1500 + i * 750,
                ViolationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-i - 1)), Description = new[] { "Hız sınırı ihlali", "Hatalı park", "Şerit ihlali", "Trafik işaretine uymama", "Eksik evrak kontrolü" }[i] }, op);
            async Task Act(WorkflowAction action, ClaimsPrincipal actor, string? note = null)
            {
                var fine = await db.TrafficFines.FindAsync(id) ?? throw new InvalidOperationException();
                await workflow.ExecuteAsync(new DecisionVm { Id = id, Version = fine.Version, Action = action, Note = note }, actor);
            }
            if (i > 0) await Act(WorkflowAction.Submitted, op);
            if (i is 2 or 3) await Act(WorkflowAction.Approved, manager);
            if (i == 3) await Act(WorkflowAction.Approved, finance, "Ödeme sürecine aktarılmak üzere tamamlandı.");
            if (i == 4) await Act(WorkflowAction.Rejected, manager, "Tutanak numarası ve evrak bilgileri kontrol edilmeli.");
        }
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}

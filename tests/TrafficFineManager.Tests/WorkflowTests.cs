using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Data;
using TrafficFineManager.Web.Models;
using TrafficFineManager.Web.Services;
using TrafficFineManager.Web.ViewModels;
using Xunit;
namespace TrafficFineManager.Tests;
public class WorkflowTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private FineWorkflowService service = null!;
    private readonly ClaimsPrincipal op = User("operator-1", Roles.Operator);
    private readonly ClaimsPrincipal manager = User("manager-1", Roles.Manager);
    private readonly ClaimsPrincipal finance = User("finance-1", Roles.Finance);
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = NewContext();
        await db.Database.EnsureCreatedAsync();
        db.Vehicles.Add(new Vehicle { Plate = "34ABC123", Type = VehicleType.Tractor, Brand = "Test", Model = "Test" });
        await db.SaveChangesAsync();
        service = new FineWorkflowService(db, TimeProvider.System);
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    private static ClaimsPrincipal User(string id, string role) => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, id + "@test.local"), new Claim(ClaimTypes.Role, role) }, "test"));
    private FineVm Input(string reference = "TEST-001") => new() {
        VehicleId = 1, ReferenceNumber = reference, Amount = 1500.25m,
        ViolationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), Description = "Hız ihlali" };
    private async Task<int> Create() => await service.CreateAsync(Input(), op);
    private async Task Act(int id, WorkflowAction action, ClaimsPrincipal user, string? note = null)
    {
        var fine = await db.TrafficFines.FindAsync(id);
        await service.ExecuteAsync(new DecisionVm { Id = id, Version = fine!.Version, Action = action, Note = note }, user);
    }
    [Fact] public async Task Full_approval_flow_preserves_every_transition()
    {
        var id = await Create();
        await Act(id, WorkflowAction.Submitted, op);
        await Act(id, WorkflowAction.Approved, manager);
        await Act(id, WorkflowAction.Approved, finance);
        var fine = await db.TrafficFines.Include(x => x.History).SingleAsync();
        Assert.Equal(FineStatus.Completed, fine.Status);
        var history = fine.History.OrderBy(x => x.Id).ToArray();
        Assert.Equal(4, history.Length);
        Assert.Null(history[0].PreviousStatus);
        Assert.Equal(FineStatus.New, history[1].PreviousStatus);
        Assert.Equal(FineStatus.ManagerApproval, history[1].NewStatus);
        Assert.Equal("manager-1", history[2].ActorId);
        Assert.Equal(FineStatus.FinanceApproval, history[2].NewStatus);
        Assert.Equal(FineStatus.Completed, history[3].NewStatus);
        Assert.All(history, item => { Assert.NotEmpty(item.ActorName); Assert.NotEqual(default, item.OccurredAt); });
    }
    [Theory] [InlineData("Operator")] [InlineData("Finance")]
    public async Task Wrong_role_cannot_approve_manager_stage(string role)
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op);
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Approved, User("wrong", role)));
        Assert.Equal(FineStatus.ManagerApproval, (await db.TrafficFines.FindAsync(id))!.Status);
        Assert.Equal(2, await db.ApprovalHistories.CountAsync());
    }
    [Fact] public async Task Manager_cannot_approve_finance_stage()
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op); await Act(id, WorkflowAction.Approved, manager);
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Approved, manager));
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("  ")]
    public async Task Reject_requires_nonblank_reason(string? note)
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op);
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Rejected, manager, note));
        Assert.Equal(2, await db.ApprovalHistories.CountAsync());
    }
    [Fact] public async Task Rejected_fine_can_be_edited_and_restarts_manager_approval()
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op); await Act(id, WorkflowAction.Approved, manager);
        await Act(id, WorkflowAction.Rejected, finance, "Tutarı doğrulayın");
        var input = Input(); input.Id = id; input.Amount = 999; input.Version = (await db.TrafficFines.FindAsync(id))!.Version;
        await service.EditAsync(input, op); await Act(id, WorkflowAction.Submitted, op);
        var fine = (await db.TrafficFines.FindAsync(id))!;
        Assert.Equal(FineStatus.ManagerApproval, fine.Status); Assert.Equal(999m, fine.Amount);
        Assert.Contains(await db.ApprovalHistories.ToListAsync(), h => h.Note == "Tutarı doğrulayın");
    }
    [Fact] public async Task Another_operator_cannot_edit_or_submit_someone_elses_record()
    {
        var id = await Create(); var stranger = User("operator-2", Roles.Operator);
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Submitted, stranger));
        var input = Input(); input.Id = id; input.Version = (await db.TrafficFines.FindAsync(id))!.Version;
        await Assert.ThrowsAsync<WorkflowException>(() => service.EditAsync(input, stranger));
    }
    [Fact] public async Task Completed_record_is_immutable()
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op); await Act(id, WorkflowAction.Approved, manager); await Act(id, WorkflowAction.Approved, finance);
        var input = Input(); input.Id = id; input.Version = (await db.TrafficFines.FindAsync(id))!.Version;
        await Assert.ThrowsAsync<WorkflowException>(() => service.EditAsync(input, op));
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Approved, finance));
        await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Rejected, finance, "Ret"));
    }
    [Fact] public async Task Stale_version_does_not_duplicate_history()
    {
        var id = await Create(); var version = (await db.TrafficFines.FindAsync(id))!.Version;
        var command = new DecisionVm { Id = id, Version = version, Action = WorkflowAction.Submitted };
        await service.ExecuteAsync(command, op);
        await Assert.ThrowsAsync<WorkflowException>(() => service.ExecuteAsync(command, op));
        Assert.Equal(2, await db.ApprovalHistories.CountAsync());
    }
    [Fact] public async Task Two_database_contexts_cannot_overwrite_the_same_approval()
    {
        var id = await Create(); await Act(id, WorkflowAction.Submitted, op);
        await using var second = NewContext();
        var stale = await second.TrafficFines.SingleAsync();
        await Act(id, WorkflowAction.Approved, manager);
        var secondService = new FineWorkflowService(second, TimeProvider.System);
        await Assert.ThrowsAsync<WorkflowException>(() => secondService.ExecuteAsync(new DecisionVm { Id = id, Version = stale.Version, Action = WorkflowAction.Rejected, Note = "Ret" }, manager));
        await using var verify = NewContext();
        Assert.Equal(FineStatus.FinanceApproval, (await verify.TrafficFines.SingleAsync()).Status);
        Assert.Equal(3, await verify.ApprovalHistories.CountAsync());
    }
    [Fact] public async Task Duplicate_reference_is_rejected_after_normalization()
    {
        await Create(); await Assert.ThrowsAsync<WorkflowException>(() => service.CreateAsync(Input(" test-001 "), op));
        Assert.Equal(1, await db.TrafficFines.CountAsync());
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1.123)]
    public async Task Invalid_amount_is_rejected(decimal amount)
    {
        var input = Input(); input.Amount = amount;
        await Assert.ThrowsAsync<WorkflowException>(() => service.CreateAsync(input, op));
    }
    [Fact] public async Task Future_date_is_rejected()
    {
        var input = Input(); input.ViolationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        await Assert.ThrowsAsync<WorkflowException>(() => service.CreateAsync(input, op));
    }
    [Fact] public async Task Missing_vehicle_is_rejected()
    {
        var input = Input(); input.VehicleId = 999;
        await Assert.ThrowsAsync<WorkflowException>(() => service.CreateAsync(input, op));
    }
    [Fact] public async Task Manager_cannot_create_fine() => await Assert.ThrowsAsync<WorkflowException>(() => service.CreateAsync(Input(), manager));
    [Fact] public async Task New_fine_cannot_skip_submission()
    {
        var id = await Create(); await Assert.ThrowsAsync<WorkflowException>(() => Act(id, WorkflowAction.Approved, manager));
    }
    [Fact] public async Task Unknown_action_is_rejected()
    {
        var id = await Create(); await Assert.ThrowsAsync<WorkflowException>(() => Act(id, (WorkflowAction)999, op));
    }

    [Fact] public async Task Editing_records_field_changes_with_old_and_new_values()
    {
        var id = await Create();
        var input = Input("EDIT-001"); input.Id = id; input.Amount = 2000.50m;
        input.Description = "Düzeltilen açıklama";
        input.Version = (await db.TrafficFines.FindAsync(id))!.Version;
        await service.EditAsync(input, op);
        var history = await db.ApprovalHistories.SingleAsync(x => x.Action == WorkflowAction.Edited);
        Assert.Contains("1.500,25 ₺ → 2.000,50 ₺", history.Note);
        Assert.Contains("TEST-001 → EDIT-001", history.Note);
        Assert.Contains("Açıklama değiştirildi", history.Note);
        Assert.Equal(FineStatus.New, history.PreviousStatus);
        Assert.Equal(FineStatus.New, history.NewStatus);
    }
    [Fact] public async Task Saving_unchanged_record_does_not_add_misleading_history()
    {
        var id = await Create(); var input = Input(); input.Id = id;
        input.Version = (await db.TrafficFines.FindAsync(id))!.Version;
        await Assert.ThrowsAsync<WorkflowException>(() => service.EditAsync(input, op));
        Assert.Equal(1, await db.ApprovalHistories.CountAsync());
    }
}

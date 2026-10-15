using System.ComponentModel.DataAnnotations;

namespace TrafficFineManager.Web.Models;

public static class Roles
{
    public const string Operator = "Operator";
    public const string Manager = "Manager";
    public const string Finance = "Finance";
}
public enum VehicleType
{
    [Display(Name = "Binek")] Passenger = 1,
    [Display(Name = "Çekici")] Tractor = 2,
    [Display(Name = "Dorse")] Trailer = 3,
    [Display(Name = "Kiralık Araç")] Rental = 4
}
public enum FineStatus
{
    [Display(Name = "Yeni")] New = 0,
    [Display(Name = "Yönetici Onayı")] ManagerApproval = 1,
    [Display(Name = "Finans Onayı")] FinanceApproval = 2,
    [Display(Name = "Tamamlandı")] Completed = 3,
    [Display(Name = "Reddedildi")] Rejected = 4
}
public enum WorkflowAction
{
    [Display(Name = "Oluşturuldu")] Created,
    [Display(Name = "Güncellendi")] Edited,
    [Display(Name = "Onaya gönderildi")] Submitted,
    [Display(Name = "Onaylandı")] Approved,
    [Display(Name = "Reddedildi")] Rejected
}
public class Vehicle
{
    public int Id { get; set; }
    public string Plate { get; set; } = "";
    public VehicleType Type { get; set; }
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
}
public class TrafficFine
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string ReferenceNumber { get; set; } = "";
    public DateOnly ViolationDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public FineStatus Status { get; set; } = FineStatus.New;
    public string CreatedById { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<ApprovalHistory> History { get; set; } = new List<ApprovalHistory>();
}
public class ApprovalHistory
{
    public int Id { get; set; }
    public int TrafficFineId { get; set; }
    public TrafficFine TrafficFine { get; set; } = null!;
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public WorkflowAction Action { get; set; }
    public string? Note { get; set; }
    public FineStatus? PreviousStatus { get; set; }
    public FineStatus NewStatus { get; set; }
}
public static class EnumLabels
{
    public static string Label(this Enum value) => value.GetType().GetField(value.ToString())?
        .GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>()
        .FirstOrDefault()?.GetName() ?? value.ToString();
}

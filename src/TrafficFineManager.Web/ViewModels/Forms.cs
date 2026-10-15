using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TrafficFineManager.Web.Models;

namespace TrafficFineManager.Web.ViewModels;
public class LoginVm
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress, Display(Name = "E-posta")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Şifre zorunludur."), DataType(DataType.Password), Display(Name = "Şifre")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class VehicleVm
{
    [Required, StringLength(20, MinimumLength = 3), Display(Name = "Plaka")]
    [RegularExpression(@"^[a-zA-Z0-9ÇĞİÖŞÜçğıöşü -]+$", ErrorMessage = "Plakada yalnızca harf, sayı, boşluk ve tire kullanın.")]
    public string Plate { get; set; } = "";
    [EnumDataType(typeof(VehicleType)), Display(Name = "Araç tipi")] public VehicleType Type { get; set; } = VehicleType.Passenger;
    [Required, StringLength(60), Display(Name = "Marka")] public string Brand { get; set; } = "";
    [Required, StringLength(60), Display(Name = "Model")] public string Model { get; set; } = "";
}
public class FineVm
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Araç seçin."), Display(Name = "Araç")] public int VehicleId { get; set; }
    [Required, StringLength(50), Display(Name = "Tutanak / ceza numarası")] public string ReferenceNumber { get; set; } = "";
    [Required, DataType(DataType.Date), Display(Name = "İhlal tarihi")] public DateOnly ViolationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "Tutar 0,01 ile 999.999.999,99 arasında olmalıdır."), Display(Name = "Tutar (₺)")] public decimal Amount { get; set; }
    [Required, StringLength(1000), Display(Name = "Açıklama")] public string Description { get; set; } = "";
    public Guid Version { get; set; }
    public List<SelectListItem> Vehicles { get; set; } = [];
}
public class DecisionVm
{
    public int Id { get; set; }
    public Guid Version { get; set; }
    public WorkflowAction Action { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}
public class FineListVm
{
    public List<TrafficFine> Items { get; set; } = [];
    public string? Search { get; set; }
    public FineStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / 15d));
}
public record DashboardVm(int Vehicles, int PendingManager, int PendingFinance, int Completed, decimal PendingAmount, List<TrafficFine> Recent);

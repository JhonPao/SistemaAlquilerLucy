using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Cabecera del contrato de alquiler (RF-04).
/// </summary>
public class Alquiler
{
    [Key]
    public int IdAlquiler { get; set; }

    [Display(Name = "Cliente")]
    public int IdCliente { get; set; }
    public Cliente? Cliente { get; set; }

    [Display(Name = "Fecha de alquiler")]
    public DateTime FechaAlquiler { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "La fecha de devolución pactada es obligatoria.")]
    [Display(Name = "Fecha de devolución pactada")]
    public DateTime FechaDevolucionPactada { get; set; } = DateTime.Today.AddDays(2);

    [Display(Name = "Subtotal")]
    public decimal Subtotal { get; set; }

    [Display(Name = "Total de garantía")]
    public decimal TotalGarantia { get; set; }

    [Display(Name = "Monto total")]
    public decimal MontoTotal { get; set; }

    [Display(Name = "Estado del contrato")]
    public EstadoContrato EstadoContrato { get; set; } = EstadoContrato.Activo;

    [StringLength(500)]
    [Display(Name = "Observaciones de entrega")]
    public string? Observaciones { get; set; }

    [Required(ErrorMessage = "El medio de pago es obligatorio.")]
    [StringLength(30)]
    [Display(Name = "Medio de pago")]
    public string MedioPago { get; set; } = "Transferencia";

    [StringLength(50)]
    [Display(Name = "Referencia de pago")]
    public string? ReferenciaPago { get; set; }

    [Display(Name = "Vendedor")]
    public string? UserId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    // Navegación
    public ICollection<DetalleAlquiler> Detalles { get; set; } = new List<DetalleAlquiler>();
    public Devolucion? Devolucion { get; set; }

    public string CodigoContrato => $"LV-{IdAlquiler:D4}";
}

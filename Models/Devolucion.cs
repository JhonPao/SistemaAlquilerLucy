using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Registro de la devolución física y de la inspección de calidad (RF-05).
/// </summary>
public class Devolucion
{
    [Key]
    public int IdDevolucion { get; set; }

    public int IdAlquiler { get; set; }
    public Alquiler? Alquiler { get; set; }

    [Display(Name = "Fecha de devolución real")]
    public DateTime FechaDevolucionReal { get; set; } = DateTime.Now;

    [Display(Name = "Estado físico de recepción")]
    public EstadoFisicoRecepcion EstadoFisicoRecepcion { get; set; } = EstadoFisicoRecepcion.Optimo;

    [StringLength(500)]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    [StringLength(30)]
    [Display(Name = "Medio de reembolso")]
    public string? MedioReembolso { get; set; }

    [Range(0, 99999)]
    [Display(Name = "Reembolso al cliente")]
    public decimal MontoReembolso { get; set; }

    [Range(0, 99999)]
    [Display(Name = "Saldo pendiente")]
    public decimal SaldoPendiente { get; set; }

    [Display(Name = "Cliente informado del desglose")]
    public bool ClienteInformado { get; set; }

    [Display(Name = "Responsable de recepción")]
    public string? UserId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    // Navegación
    public Penalidad? Penalidad { get; set; }
    public ICollection<DetalleDevolucion> Detalles { get; set; } = new List<DetalleDevolucion>();
}

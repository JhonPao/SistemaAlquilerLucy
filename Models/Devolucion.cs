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

    [Display(Name = "Responsable de recepción")]
    public string? UserId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    // Navegación
    public Penalidad? Penalidad { get; set; }
}

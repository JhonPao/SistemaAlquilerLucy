using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Resultado de la inspección de una prenda concreta dentro de una devolución
/// (RF-05 / RF-06). Permite aplicar RNC02 por unidad y derivar solo las prendas
/// dañadas a mantenimiento.
/// </summary>
public class DetalleDevolucion
{
    [Key]
    public int IdDetalleDevolucion { get; set; }

    public int IdDevolucion { get; set; }
    public Devolucion? Devolucion { get; set; }

    public int IdDetalleAlquiler { get; set; }
    public DetalleAlquiler? DetalleAlquiler { get; set; }

    [Display(Name = "Estado físico de recepción")]
    public EstadoFisicoRecepcion EstadoFisicoRecepcion { get; set; } = EstadoFisicoRecepcion.Optimo;

    [Range(0, 99999)]
    [Display(Name = "Cargo por daño")]
    public decimal CargoDanio { get; set; }

    [StringLength(300)]
    [Display(Name = "Observaciones de la prenda")]
    public string? Observaciones { get; set; }
}

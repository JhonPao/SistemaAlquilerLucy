using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Orden de mantenimiento (lavandería, costura o desinfección) de una prenda (RF-07).
/// </summary>
public class Mantenimiento
{
    [Key]
    public int IdMantenimiento { get; set; }

    public int IdPrenda { get; set; }
    public Prenda? Prenda { get; set; }

    [Display(Name = "Fecha de ingreso")]
    public DateTime FechaIngreso { get; set; } = DateTime.Now;

    [Display(Name = "Fecha de salida estimada")]
    public DateTime FechaSalidaEstimada { get; set; } = DateTime.Today.AddDays(2);

    [Display(Name = "Fecha de salida real")]
    public DateTime? FechaSalidaReal { get; set; }

    [Display(Name = "Tipo de servicio")]
    public TipoServicio TipoServicio { get; set; } = TipoServicio.Lavanderia;

    [Range(0, 99999)]
    [Display(Name = "Costo del servicio")]
    public decimal CostoServicio { get; set; }

    [StringLength(300)]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    public bool EnCurso => FechaSalidaReal is null;
}

using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Tarifas configurables por la Administradora (RF-06 / HU-10).
/// Es una fila única de configuración para no incrustar montos en el código.
/// </summary>
public class ConfiguracionPolitica
{
    [Key]
    public int IdConfiguracion { get; set; }

    [Range(0, 9999)]
    [Display(Name = "Tarifa diaria de mora (RNC01)")]
    public decimal TarifaMoraDiaria { get; set; } = 15.00m;

    [Range(0, 1)]
    [Display(Name = "Porcentaje de daño leve (RNC02)")]
    public decimal PorcentajeDanioLeve { get; set; } = 0.50m;

    [Range(0, 1)]
    [Display(Name = "Porcentaje de daño grave (RNC02)")]
    public decimal PorcentajeDanioGrave { get; set; } = 1.00m;

    [Range(0, 99999)]
    [Display(Name = "Costo base de reparación")]
    public decimal CostoBaseReparacion { get; set; } = 40.00m;

    [Display(Name = "Última actualización")]
    public DateTime FechaActualizacion { get; set; } = DateTime.Now;
}

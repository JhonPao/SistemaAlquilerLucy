using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Liquidación de las reglas de negocio RNC01 (mora) y RNC02 (deterioro).
/// Se guardan las tarifas aplicadas para que el cálculo sea auditable.
/// </summary>
public class Penalidad
{
    [Key]
    public int IdPenalidad { get; set; }

    public int IdDevolucion { get; set; }
    public Devolucion? Devolucion { get; set; }

    [Display(Name = "Días de retraso")]
    public int DiasRetraso { get; set; }

    [Display(Name = "Tarifa diaria aplicada")]
    public decimal TarifaMoraAplicada { get; set; }

    [Display(Name = "Monto por mora (RNC01)")]
    public decimal MontoMora { get; set; }

    [Display(Name = "Costo base de reparación aplicado")]
    public decimal CostoBaseAplicado { get; set; }

    [Display(Name = "Monto por deterioro (RNC02)")]
    public decimal MontoDanio { get; set; }

    [Display(Name = "Total de penalidad")]
    public decimal TotalPenalidad { get; set; }

    [Display(Name = "Liquidado")]
    public bool Liquidado { get; set; }
}

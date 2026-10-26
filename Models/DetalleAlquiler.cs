using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Detalle de prendas incluidas en un contrato de alquiler.
/// </summary>
public class DetalleAlquiler
{
    [Key]
    public int IdDetalle { get; set; }

    public int IdAlquiler { get; set; }
    public Alquiler? Alquiler { get; set; }

    public int IdPrenda { get; set; }
    public Prenda? Prenda { get; set; }

    [Display(Name = "Precio unitario de alquiler")]
    public decimal PrecioUnitarioAlquiler { get; set; }

    [Display(Name = "Garantía unitaria")]
    public decimal GarantiaUnitaria { get; set; }
}

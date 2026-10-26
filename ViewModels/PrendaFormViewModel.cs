using System.ComponentModel.DataAnnotations;
using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.ViewModels;

/// <summary>
/// Formulario de alta y edición de prendas. Se separa de la entidad para
/// evitar sobre-publicación de campos y permitir montos vacíos al registrar.
/// </summary>
public class PrendaFormViewModel
{
    public int IdPrenda { get; set; }

    [Required(ErrorMessage = "El código único es obligatorio.")]
    [StringLength(20)]
    [RegularExpression(@"^(?i)[A-Z0-9][A-Z0-9-]{2,19}$",
        ErrorMessage = "El código debe ser alfanumérico, en mayúsculas y de 3 a 20 caracteres.")]
    [Display(Name = "Código único")]
    public string CodigoUnico { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120)]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(60)]
    [Display(Name = "Categoría")]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "La talla es obligatoria.")]
    [StringLength(10)]
    [Display(Name = "Talla")]
    public string Talla { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el precio de alquiler.")]
    [Range(0.01, 99999, ErrorMessage = "El precio de alquiler debe ser mayor a cero.")]
    [Display(Name = "Precio de alquiler")]
    public decimal? PrecioAlquiler { get; set; }

    [Required(ErrorMessage = "Ingrese el costo de garantía.")]
    [Range(0, 99999, ErrorMessage = "El costo de garantía no puede ser negativo.")]
    [Display(Name = "Costo de garantía")]
    public decimal? CostoGarantia { get; set; }

    public EstadoPrenda Estado { get; set; } = EstadoPrenda.Disponible;
    public DateTime FechaRegistro { get; set; }

    public Prenda AEntidad()
    {
        return new Prenda
        {
            IdPrenda = IdPrenda,
            CodigoUnico = CodigoUnico,
            Nombre = Nombre,
            Categoria = Categoria,
            Talla = Talla,
            PrecioAlquiler = PrecioAlquiler ?? 0m,
            CostoGarantia = CostoGarantia ?? 0m
        };
    }

    public static PrendaFormViewModel DesdeEntidad(Prenda prenda)
    {
        return new PrendaFormViewModel
        {
            IdPrenda = prenda.IdPrenda,
            CodigoUnico = prenda.CodigoUnico,
            Nombre = prenda.Nombre,
            Categoria = prenda.Categoria,
            Talla = prenda.Talla,
            PrecioAlquiler = prenda.PrecioAlquiler,
            CostoGarantia = prenda.CostoGarantia,
            Estado = prenda.Estado,
            FechaRegistro = prenda.FechaRegistro
        };
    }
}

using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Vestimenta física del inventario. El código alfanumérico es único por prenda
/// y permite su trazabilidad individual (RF-02).
/// </summary>
public class Prenda
{
    [Key]
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

    [Range(0.01, 99999, ErrorMessage = "El precio de alquiler debe ser mayor a cero.")]
    [Display(Name = "Precio de alquiler")]
    public decimal PrecioAlquiler { get; set; }

    [Range(0, 99999, ErrorMessage = "El costo de garantía no puede ser negativo.")]
    [Display(Name = "Costo de garantía")]
    public decimal CostoGarantia { get; set; }

    [Display(Name = "Estado")]
    public EstadoPrenda Estado { get; set; } = EstadoPrenda.Disponible;

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;

    [Display(Name = "Fecha de registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // Navegación
    public ICollection<DetalleAlquiler> Detalles { get; set; } = new List<DetalleAlquiler>();
    public ICollection<Mantenimiento> Mantenimientos { get; set; } = new List<Mantenimiento>();
}

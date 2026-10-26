using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Arrendatario. El DNI se valida a 8 dígitos y el consentimiento informado
/// es obligatorio conforme a la Ley N° 29733.
/// </summary>
public class Cliente
{
    [Key]
    public int IdCliente { get; set; }

    [Required(ErrorMessage = "El DNI es obligatorio.")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "El DNI debe tener exactamente 8 dígitos.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener exactamente 8 dígitos.")]
    [Display(Name = "DNI")]
    public string Dni { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100)]
    [Display(Name = "Nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100)]
    [Display(Name = "Apellidos")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(15)]
    [RegularExpression(@"^\d{6,15}$", ErrorMessage = "El teléfono debe contener entre 6 y 15 dígitos.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [Required(ErrorMessage = "Debe registrar el consentimiento para el tratamiento de datos.")]
    [Range(typeof(bool), "true", "true",
        ErrorMessage = "El cliente debe autorizar el tratamiento de sus datos personales (Ley N° 29733).")]
    [Display(Name = "Consentimiento de datos (Ley N° 29733)")]
    public bool ConsentimientoDatos { get; set; }

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;

    [Display(Name = "Fecha de registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // Navegación
    public ICollection<Alquiler> Alquileres { get; set; } = new List<Alquiler>();

    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
}

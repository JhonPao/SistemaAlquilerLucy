using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.ViewModels;

public class AdministracionIndexViewModel
{
    public string? Q { get; set; }
    public string? Rol { get; set; }
    public string Estado { get; set; } = "todos";
    public int TotalUsuarios { get; set; }
    public int UsuariosActivos { get; set; }
    public int UsuariosInactivos { get; set; }
    public int Administradores { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<UsuarioAdminFilaViewModel> Usuarios { get; set; } = new();
    public PoliticaViewModel Politica { get; set; } = new();
}

public class UsuarioAdminFilaViewModel
{
    public string Id { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool EsUsuarioActual { get; set; }
}

public class UsuarioFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [StringLength(150)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Selecciona un rol.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Usuario activo")]
    public bool Activo { get; set; } = true;

    public bool EsEdicion => !string.IsNullOrWhiteSpace(Id);
    public List<string> Roles { get; set; } = new();
}

public class RestablecerPasswordViewModel
{
    public string Id { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la nueva contraseña.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class PoliticaViewModel
{
    [Range(0, 99999, ErrorMessage = "La tarifa debe ser mayor o igual a cero.")]
    [Display(Name = "Tarifa de mora diaria")]
    public decimal TarifaMoraDiaria { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Daño leve")]
    public decimal PorcentajeDanioLeve { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Daño grave")]
    public decimal PorcentajeDanioGrave { get; set; }

    [Range(0, 99999, ErrorMessage = "El costo base debe ser mayor o igual a cero.")]
    [Display(Name = "Costo base de reparación")]
    public decimal CostoBaseReparacion { get; set; }

    public DateTime FechaActualizacion { get; set; }
}

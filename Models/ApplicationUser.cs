using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Usuario extendido de ASP.NET Core Identity con los datos propios del negocio.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;

    [Display(Name = "Fecha de creación")]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}

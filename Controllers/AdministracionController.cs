using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Administración de usuarios, roles y políticas de negocio (RF-09).
/// Solo la cuenta con rol Administradora puede acceder al módulo.
/// </summary>
[Authorize(Roles = DbInitializer.RolAdministradora)]
public class AdministracionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AdministracionController> _logger;

    public AdministracionController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<AdministracionController> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, string? rol, string? estado)
    {
        ViewData["ActiveNav"] = "administracion";

        var estadoTab = estado?.Trim().ToLowerInvariant() switch
        {
            "activos" => "activos",
            "inactivos" => "inactivos",
            _ => "todos"
        };

        var usuarios = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();

        var filas = new List<UsuarioAdminFilaViewModel>();
        foreach (var usuario in usuarios)
        {
            var roles = await _userManager.GetRolesAsync(usuario);
            filas.Add(new UsuarioAdminFilaViewModel
            {
                Id = usuario.Id,
                NombreCompleto = usuario.NombreCompleto,
                Email = usuario.Email ?? usuario.UserName ?? string.Empty,
                Rol = roles.FirstOrDefault() ?? "Sin rol",
                Activo = usuario.Activo,
                FechaCreacion = usuario.FechaCreacion,
                EsUsuarioActual = usuario.Id == _userManager.GetUserId(User)
            });
        }

        var todos = filas.ToList();
        var conteoAdministradores = todos.Count(u => u.Rol == DbInitializer.RolAdministradora);

        var consulta = filas.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var termino = q.Trim();
            consulta = consulta.Where(u =>
                u.NombreCompleto.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(rol))
        {
            consulta = consulta.Where(u => u.Rol == rol);
        }

        consulta = estadoTab switch
        {
            "activos" => consulta.Where(u => u.Activo),
            "inactivos" => consulta.Where(u => !u.Activo),
            _ => consulta
        };

        var politica = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

        return View(new AdministracionIndexViewModel
        {
            Q = q,
            Rol = rol,
            Estado = estadoTab,
            TotalUsuarios = todos.Count,
            UsuariosActivos = todos.Count(u => u.Activo),
            UsuariosInactivos = todos.Count(u => !u.Activo),
            Administradores = conteoAdministradores,
            Roles = await CargarRolesAsync(),
            Usuarios = consulta.OrderBy(u => u.NombreCompleto).ToList(),
            Politica = politica is null
                ? new PoliticaViewModel()
                : new PoliticaViewModel
                {
                    TarifaMoraDiaria = politica.TarifaMoraDiaria,
                    PorcentajeDanioLeve = politica.PorcentajeDanioLeve * 100m,
                    PorcentajeDanioGrave = politica.PorcentajeDanioGrave * 100m,
                    CostoBaseReparacion = politica.CostoBaseReparacion,
                    FechaActualizacion = politica.FechaActualizacion
                }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["ActiveNav"] = "administracion";
        return View(new UsuarioFormViewModel
        {
            Rol = DbInitializer.RolVendedor,
            Activo = true,
            Roles = await CargarRolesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioFormViewModel modelo)
    {
        ViewData["ActiveNav"] = "administracion";
        modelo.Roles = await CargarRolesAsync();

        if (string.IsNullOrWhiteSpace(modelo.Password))
        {
            ModelState.AddModelError(nameof(modelo.Password), "La contraseña es obligatoria para un usuario nuevo.");
        }

        if (!modelo.Roles.Contains(modelo.Rol))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Selecciona un rol válido.");
        }

        if (await _userManager.FindByEmailAsync(modelo.Email.Trim()) is not null)
        {
            ModelState.AddModelError(nameof(modelo.Email), "Ya existe un usuario registrado con ese correo.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario = new ApplicationUser
        {
            UserName = modelo.Email.Trim(),
            Email = modelo.Email.Trim(),
            EmailConfirmed = true,
            NombreCompleto = modelo.NombreCompleto.Trim(),
            Activo = modelo.Activo,
            FechaCreacion = DateTime.Now
        };

        var resultado = await _userManager.CreateAsync(usuario, modelo.Password!);
        if (!resultado.Succeeded)
        {
            AgregarErrores(resultado);
            return View(modelo);
        }

        var resultadoRol = await _userManager.AddToRoleAsync(usuario, modelo.Rol);
        if (!resultadoRol.Succeeded)
        {
            await _userManager.DeleteAsync(usuario);
            AgregarErrores(resultadoRol);
            return View(modelo);
        }

        _logger.LogInformation("Usuario {Email} creado con rol {Rol}.", usuario.Email, modelo.Rol);
        TempData["Exito"] = $"El usuario {usuario.Email} fue creado con el rol {modelo.Rol}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        ViewData["ActiveNav"] = "administracion";

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(usuario);

        return View(new UsuarioFormViewModel
        {
            Id = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email ?? usuario.UserName ?? string.Empty,
            Rol = roles.FirstOrDefault() ?? DbInitializer.RolVendedor,
            Activo = usuario.Activo,
            Roles = await CargarRolesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UsuarioFormViewModel modelo)
    {
        ViewData["ActiveNav"] = "administracion";
        modelo.Roles = await CargarRolesAsync();

        if (id != modelo.Id)
        {
            return BadRequest();
        }

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        if (!modelo.Roles.Contains(modelo.Rol))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Selecciona un rol válido.");
        }

        var existente = await _userManager.FindByEmailAsync(modelo.Email.Trim());
        if (existente is not null && existente.Id != usuario.Id)
        {
            ModelState.AddModelError(nameof(modelo.Email), "Ya existe otro usuario registrado con ese correo.");
        }

        var esUsuarioActual = usuario.Id == _userManager.GetUserId(User);
        if (esUsuarioActual && !modelo.Activo)
        {
            ModelState.AddModelError(nameof(modelo.Activo), "No puedes desactivar tu propia cuenta.");
        }

        if (esUsuarioActual && modelo.Rol != DbInitializer.RolAdministradora)
        {
            ModelState.AddModelError(nameof(modelo.Rol), "No puedes retirar tu propio rol de Administradora.");
        }

        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        var eraAdministradora = rolesActuales.Contains(DbInitializer.RolAdministradora);
        var seguiraSiendoAdministradora = modelo.Rol == DbInitializer.RolAdministradora && modelo.Activo;

        if (eraAdministradora && !seguiraSiendoAdministradora)
        {
            var administradoras = await _userManager.GetUsersInRoleAsync(DbInitializer.RolAdministradora);
            if (administradoras.Count(u => u.Activo) <= 1)
            {
                ModelState.AddModelError(nameof(modelo.Rol),
                    "Debe existir al menos una Administradora activa en el sistema.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        usuario.NombreCompleto = modelo.NombreCompleto.Trim();
        usuario.Activo = modelo.Activo;

        var emailAnterior = usuario.Email;
        if (!string.Equals(emailAnterior, modelo.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var resultadoEmail = await _userManager.SetEmailAsync(usuario, modelo.Email.Trim());
            if (!resultadoEmail.Succeeded)
            {
                AgregarErrores(resultadoEmail);
                return View(modelo);
            }

            var resultadoNombre = await _userManager.SetUserNameAsync(usuario, modelo.Email.Trim());
            if (!resultadoNombre.Succeeded)
            {
                AgregarErrores(resultadoNombre);
                return View(modelo);
            }
        }

        var actualizacion = await _userManager.UpdateAsync(usuario);
        if (!actualizacion.Succeeded)
        {
            AgregarErrores(actualizacion);
            return View(modelo);
        }

        var rolesDiferentes = rolesActuales.Where(r => r != modelo.Rol).ToList();
        if (rolesDiferentes.Count > 0)
        {
            var quitar = await _userManager.RemoveFromRolesAsync(usuario, rolesDiferentes);
            if (!quitar.Succeeded)
            {
                AgregarErrores(quitar);
                return View(modelo);
            }
        }

        if (!rolesActuales.Contains(modelo.Rol))
        {
            var agregar = await _userManager.AddToRoleAsync(usuario, modelo.Rol);
            if (!agregar.Succeeded)
            {
                AgregarErrores(agregar);
                return View(modelo);
            }
        }

        if (!esUsuarioActual)
        {
            // Invalida las sesiones abiertas del usuario cuando cambian sus permisos o estado.
            await _userManager.UpdateSecurityStampAsync(usuario);
        }

        _logger.LogInformation("Usuario {Email} actualizado por administración.", usuario.Email);
        TempData["Exito"] = $"El usuario {usuario.Email} fue actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActivo(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        if (usuario.Activo)
        {
            var roles = await _userManager.GetRolesAsync(usuario);
            if (roles.Contains(DbInitializer.RolAdministradora))
            {
                var administradoras = await _userManager.GetUsersInRoleAsync(DbInitializer.RolAdministradora);
                if (administradoras.Count(u => u.Activo) <= 1)
                {
                    TempData["Error"] = "Debe existir al menos una Administradora activa en el sistema.";
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        usuario.Activo = !usuario.Activo;
        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            TempData["Error"] = string.Join(" ", resultado.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await _userManager.UpdateSecurityStampAsync(usuario);
        _logger.LogInformation("Usuario {Email} {Estado}.", usuario.Email, usuario.Activo ? "activado" : "desactivado");
        TempData["Exito"] = usuario.Activo
            ? $"El usuario {usuario.Email} fue activado."
            : $"El usuario {usuario.Email} fue desactivado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        ViewData["ActiveNav"] = "administracion";
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        return View(new RestablecerPasswordViewModel
        {
            Id = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email ?? usuario.UserName ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(RestablecerPasswordViewModel modelo)
    {
        ViewData["ActiveNav"] = "administracion";

        var usuario = await _userManager.FindByIdAsync(modelo.Id);
        if (usuario is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            modelo.NombreCompleto = usuario.NombreCompleto;
            modelo.Email = usuario.Email ?? usuario.UserName ?? string.Empty;
            return View(modelo);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, modelo.Password);
        if (!resultado.Succeeded)
        {
            AgregarErrores(resultado);
            modelo.NombreCompleto = usuario.NombreCompleto;
            modelo.Email = usuario.Email ?? usuario.UserName ?? string.Empty;
            return View(modelo);
        }

        await _userManager.UpdateSecurityStampAsync(usuario);
        _logger.LogInformation("Contraseña restablecida para {Email}.", usuario.Email);
        TempData["Exito"] = $"La contraseña de {usuario.Email} fue restablecida.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Politica()
    {
        ViewData["ActiveNav"] = "administracion";
        var politica = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

        if (politica is null)
        {
            return View(new PoliticaViewModel());
        }

        return View(new PoliticaViewModel
        {
            TarifaMoraDiaria = politica.TarifaMoraDiaria,
            PorcentajeDanioLeve = politica.PorcentajeDanioLeve * 100m,
            PorcentajeDanioGrave = politica.PorcentajeDanioGrave * 100m,
            CostoBaseReparacion = politica.CostoBaseReparacion,
            FechaActualizacion = politica.FechaActualizacion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Politica(PoliticaViewModel modelo)
    {
        ViewData["ActiveNav"] = "administracion";

        if (modelo.PorcentajeDanioGrave < modelo.PorcentajeDanioLeve)
        {
            ModelState.AddModelError(nameof(modelo.PorcentajeDanioGrave),
                "El porcentaje de daño grave no puede ser menor que el de daño leve.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var politica = await _db.ConfiguracionesPolitica
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

        if (politica is null)
        {
            politica = new ConfiguracionPolitica();
            _db.ConfiguracionesPolitica.Add(politica);
        }

        politica.TarifaMoraDiaria = modelo.TarifaMoraDiaria;
        politica.PorcentajeDanioLeve = modelo.PorcentajeDanioLeve / 100m;
        politica.PorcentajeDanioGrave = modelo.PorcentajeDanioGrave / 100m;
        politica.CostoBaseReparacion = modelo.CostoBaseReparacion;
        politica.FechaActualizacion = DateTime.Now;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Políticas de negocio actualizadas.");
        TempData["Exito"] = "Las tarifas y porcentajes de liquidación fueron actualizados.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> CargarRolesAsync()
    {
        return await _roleManager.Roles
            .Where(r => r.Name != null)
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();
    }

    private void AgregarErrores(IdentityResult resultado)
    {
        foreach (var error in resultado.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}

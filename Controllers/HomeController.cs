using System.Diagnostics;
using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Panel general del taller (dashboard) y páginas de módulos aún no habilitados.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var fechaActual = DateTime.Today;
        var usuarioActual = await _userManager.GetUserAsync(User);
        var nombreDelUsuario = usuarioActual?.NombreCompleto ?? User.Identity?.Name ?? string.Empty;
        var nombrePila = nombreDelUsuario.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nombreDelUsuario;

        var configuracionVigente = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

        var contratosActivos = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Detalles)
            .Where(a => a.EstadoContrato == EstadoContrato.Activo)
            .OrderBy(a => a.FechaDevolucionPactada)
            .ToListAsync();

        var resumenPrendas = await _db.Prendas
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        int ContarPorEstado(EstadoPrenda estado) => resumenPrendas.FirstOrDefault(x => x.Estado == estado)?.Total ?? 0;

        var devolucionesDelDia = await _db.Devoluciones
            .AsNoTracking()
            .CountAsync(d => d.FechaDevolucionReal >= fechaActual && d.FechaDevolucionReal < fechaActual.AddDays(1));

        var panel = new DashboardViewModel
        {
            Nombre = nombrePila,
            ContratosVigentes = contratosActivos.Count,
            DevolucionesHoy = devolucionesDelDia,
            VencidosConMora = contratosActivos.Count(a => a.FechaDevolucionPactada.Date < fechaActual),
            PrendasDisponibles = ContarPorEstado(EstadoPrenda.Disponible),
            TotalPrendas = resumenPrendas.Sum(x => x.Total),
            Alquiladas = ContarPorEstado(EstadoPrenda.Alquilada),
            EnMantenimiento = ContarPorEstado(EstadoPrenda.EnMantenimiento),
            DeBaja = ContarPorEstado(EstadoPrenda.DeBaja),
            TarifaMoraDiaria = configuracionVigente?.TarifaMoraDiaria ?? 0m,
            ProximasDevoluciones = contratosActivos
                .Take(3)
                .Select(a => new ProximaDevolucionViewModel
                {
                    IdAlquiler = a.IdAlquiler,
                    Contrato = $"LV-{a.IdAlquiler:D4}",
                    Cliente = a.Cliente?.NombreCompleto ?? "Sin cliente",
                    FechaPactada = a.FechaDevolucionPactada,
                    CantidadPrendas = a.Detalles.Count,
                    Vencido = a.FechaDevolucionPactada.Date < fechaActual
                })
                .ToList()
        };

        panel.ActividadReciente = await ObtenerActividadRecienteAsync();

        return View(panel);
    }

    private async Task<List<ActividadViewModel>> ObtenerActividadRecienteAsync()
    {
        var registrosActividad = new List<(DateTime Fecha, string Movimiento, string Responsable)>();

        var alquileresRecientes = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaAlquiler)
            .Take(3)
            .ToListAsync();

        registrosActividad.AddRange(alquileresRecientes.Select(a => (
            a.FechaAlquiler,
            $"Contrato LV-{a.IdAlquiler:D4} confirmado",
            a.Usuario?.NombreCompleto ?? "Sistema")));

        var mantenimientosRecientes = await _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .OrderByDescending(m => m.FechaIngreso)
            .Take(2)
            .ToListAsync();

        registrosActividad.AddRange(mantenimientosRecientes.Select(m => (
            m.FechaIngreso,
            $"Ingreso de {m.Prenda?.CodigoUnico ?? "prenda"} a mantenimiento",
            "Asistente")));

        var devolucionesRecientes = await _db.Devoluciones
            .AsNoTracking()
            .OrderByDescending(d => d.FechaDevolucionReal)
            .Take(2)
            .ToListAsync();

        registrosActividad.AddRange(devolucionesRecientes.Select(d => (
            d.FechaDevolucionReal,
            $"Devolución del contrato LV-{d.IdAlquiler:D4}",
            "Recepción")));

        return registrosActividad
            .OrderByDescending(m => m.Fecha)
            .Take(3)
            .Select(m => new ActividadViewModel
            {
                Hora = FormatearMomento(m.Fecha),
                Movimiento = m.Movimiento,
                Responsable = m.Responsable
            })
            .ToList();
    }

    private static string FormatearMomento(DateTime momento)
    {
        var diasTranscurridos = (DateTime.Today - momento.Date).Days;

        return diasTranscurridos switch
        {
            0 => momento.ToString("HH:mm"),
            1 => $"Ayer {momento:HH:mm}",
            _ => momento.ToString("dd MMM · HH:mm")
        };
    }

    [HttpGet]
    public IActionResult Pendiente(string? modulo, string? clave)
    {
        ViewData["Title"] = string.IsNullOrWhiteSpace(modulo) ? "Módulo" : modulo;
        ViewData["ActiveNav"] = clave;
        ViewData["Modulo"] = ViewData["Title"];
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

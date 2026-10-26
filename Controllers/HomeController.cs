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
        var hoy = DateTime.Today;
        var usuario = await _userManager.GetUserAsync(User);
        var nombreCompleto = usuario?.NombreCompleto ?? User.Identity?.Name ?? string.Empty;
        var primerNombre = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nombreCompleto;

        var politica = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

        var alquileresActivos = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Detalles)
            .Where(a => a.EstadoContrato == EstadoContrato.Activo)
            .OrderBy(a => a.FechaDevolucionPactada)
            .ToListAsync();

        var prendasPorEstado = await _db.Prendas
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        int Contar(EstadoPrenda estado) => prendasPorEstado.FirstOrDefault(x => x.Estado == estado)?.Total ?? 0;

        var devolucionesHoy = await _db.Devoluciones
            .AsNoTracking()
            .CountAsync(d => d.FechaDevolucionReal >= hoy && d.FechaDevolucionReal < hoy.AddDays(1));

        var modelo = new DashboardViewModel
        {
            Nombre = primerNombre,
            ContratosVigentes = alquileresActivos.Count,
            DevolucionesHoy = devolucionesHoy,
            VencidosConMora = alquileresActivos.Count(a => a.FechaDevolucionPactada.Date < hoy),
            PrendasDisponibles = Contar(EstadoPrenda.Disponible),
            TotalPrendas = prendasPorEstado.Sum(x => x.Total),
            Alquiladas = Contar(EstadoPrenda.Alquilada),
            EnMantenimiento = Contar(EstadoPrenda.EnMantenimiento),
            DeBaja = Contar(EstadoPrenda.DeBaja),
            TarifaMoraDiaria = politica?.TarifaMoraDiaria ?? 0m,
            ProximasDevoluciones = alquileresActivos
                .Take(3)
                .Select(a => new ProximaDevolucionViewModel
                {
                    IdAlquiler = a.IdAlquiler,
                    Contrato = $"LV-{a.IdAlquiler:D4}",
                    Cliente = a.Cliente?.NombreCompleto ?? "Sin cliente",
                    FechaPactada = a.FechaDevolucionPactada,
                    CantidadPrendas = a.Detalles.Count,
                    Vencido = a.FechaDevolucionPactada.Date < hoy
                })
                .ToList()
        };

        modelo.ActividadReciente = await ObtenerActividadRecienteAsync();

        return View(modelo);
    }

    private async Task<List<ActividadViewModel>> ObtenerActividadRecienteAsync()
    {
        var movimientos = new List<(DateTime Fecha, string Movimiento, string Responsable)>();

        var ultimosAlquileres = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaAlquiler)
            .Take(3)
            .ToListAsync();

        movimientos.AddRange(ultimosAlquileres.Select(a => (
            a.FechaAlquiler,
            $"Contrato LV-{a.IdAlquiler:D4} confirmado",
            a.Usuario?.NombreCompleto ?? "Sistema")));

        var ultimosMantenimientos = await _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .OrderByDescending(m => m.FechaIngreso)
            .Take(2)
            .ToListAsync();

        movimientos.AddRange(ultimosMantenimientos.Select(m => (
            m.FechaIngreso,
            $"Ingreso de {m.Prenda?.CodigoUnico ?? "prenda"} a mantenimiento",
            "Asistente")));

        var ultimasDevoluciones = await _db.Devoluciones
            .AsNoTracking()
            .OrderByDescending(d => d.FechaDevolucionReal)
            .Take(2)
            .ToListAsync();

        movimientos.AddRange(ultimasDevoluciones.Select(d => (
            d.FechaDevolucionReal,
            $"Devolución del contrato LV-{d.IdAlquiler:D4}",
            "Recepción")));

        return movimientos
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

    private static string FormatearMomento(DateTime fecha)
    {
        var dias = (DateTime.Today - fecha.Date).Days;

        return dias switch
        {
            0 => fecha.ToString("HH:mm"),
            1 => $"Ayer {fecha:HH:mm}",
            _ => fecha.ToString("dd MMM · HH:mm")
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

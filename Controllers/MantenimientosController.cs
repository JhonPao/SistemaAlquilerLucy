using System.Globalization;
using System.Text;
using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Órdenes de lavandería, costura y desinfección (RF-07).
/// El estado de la prenda se mantiene sincronizado con la orden activa.
/// </summary>
[Authorize]
public class MantenimientosController : Controller
{
    private const int PageSize = 8;
    private const string RolesGestion = DbInitializer.RolAdministradora + "," + DbInitializer.RolAsistente;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<MantenimientosController> _logger;

    public MantenimientosController(ApplicationDbContext db, ILogger<MantenimientosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, string? estado, int page = 1)
    {
        ViewData["ActiveNav"] = "mantenimiento";

        var ahora = DateTime.Now;
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes = inicioMes.AddMonths(1);
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var estadoTab = termino is not null && string.IsNullOrWhiteSpace(estado)
            ? "todos"
            : NormalizarEstado(estado);

        var consulta = _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .AsQueryable();

        if (termino is not null)
        {
            var tokens = termino.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var filtro = token;
                consulta = consulta.Where(m =>
                    (m.Prenda != null && m.Prenda.CodigoUnico.Contains(filtro)) ||
                    (m.Prenda != null && m.Prenda.Nombre.Contains(filtro)) ||
                    (m.Prenda != null && m.Prenda.Categoria.Contains(filtro)) ||
                    (m.Observaciones != null && m.Observaciones.Contains(filtro)));
            }
        }

        var conteoTodos = await consulta.CountAsync();
        var conteoEnCurso = await consulta.CountAsync(m => m.FechaSalidaReal == null);
        var conteoVencidos = await consulta.CountAsync(m =>
            m.FechaSalidaReal == null && m.FechaSalidaEstimada < ahora);
        var conteoFinalizados = await consulta.CountAsync(m => m.FechaSalidaReal != null);

        var filtrada = estadoTab switch
        {
            "vencidos" => consulta.Where(m => m.FechaSalidaReal == null && m.FechaSalidaEstimada < ahora),
            "finalizados" => consulta.Where(m => m.FechaSalidaReal != null),
            "todos" => consulta,
            _ => consulta.Where(m => m.FechaSalidaReal == null)
        };

        var totalFiltrado = await filtrada.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltrado / (double)PageSize));
        var paginaActual = Math.Clamp(page, 1, totalPages);

        var filas = await filtrada
            .OrderBy(m => m.FechaSalidaReal == null ? 0 : 1)
            .ThenBy(m => m.FechaSalidaEstimada)
            .ThenByDescending(m => m.IdMantenimiento)
            .Skip((paginaActual - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var filasVm = filas.Select(m => MapearFila(m, ahora)).ToList();
        var activos = consulta.Where(m => m.FechaSalidaReal == null);

        var modelo = new MantenimientosIndexViewModel
        {
            Q = termino,
            EstadoTab = estadoTab,
            Page = paginaActual,
            TotalPages = totalPages,
            TotalFiltrado = totalFiltrado,
            ConteoTodos = conteoTodos,
            ConteoEnCurso = conteoEnCurso,
            ConteoVencidos = conteoVencidos,
            ConteoFinalizados = conteoFinalizados,
            KpiEnTaller = conteoEnCurso,
            KpiVencidos = conteoVencidos,
            KpiFinalizadosMes = await consulta.CountAsync(m =>
                m.FechaSalidaReal != null && m.FechaSalidaReal >= inicioMes && m.FechaSalidaReal < finMes),
            KpiCostoMes = await consulta
                .Where(m => m.FechaIngreso >= inicioMes && m.FechaIngreso < finMes)
                .SumAsync(m => (decimal?)m.CostoServicio) ?? 0m,
            PrendasEnTaller = await activos.Select(m => m.IdPrenda).Distinct().CountAsync(),
            FechaCorte = ahora,
            Filas = filasVm,
            Alerta = await consulta
                .Where(m => m.FechaSalidaReal == null && m.FechaSalidaEstimada < ahora)
                .OrderBy(m => m.FechaSalidaEstimada)
                .ThenBy(m => m.IdMantenimiento)
                .FirstOrDefaultAsync() is { } alerta
                ? MapearFila(alerta, ahora)
                : null
        };

        return View(modelo);
    }

    [Authorize(Roles = RolesGestion)]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Create()
    {
        ViewData["ActiveNav"] = "mantenimiento";

        var ahora = DateTime.Now;
        var modelo = new MantenimientoFormViewModel
        {
            FechaIngreso = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0),
            FechaSalidaEstimada = DateTime.Today.AddDays(2).AddHours(18),
            Prendas = await ObtenerPrendasDisponiblesAsync()
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Create(MantenimientoFormViewModel modelo)
    {
        ViewData["ActiveNav"] = "mantenimiento";

        ValidarFechas(modelo);

        var prenda = await _db.Prendas.FirstOrDefaultAsync(p => p.IdPrenda == modelo.IdPrenda && p.Activo);
        if (prenda is null)
        {
            ModelState.AddModelError(nameof(modelo.IdPrenda), "La prenda seleccionada no existe o está inactiva.");
        }
        else if (prenda.Estado == EstadoPrenda.Alquilada)
        {
            ModelState.AddModelError(nameof(modelo.IdPrenda), "La prenda está alquilada. Primero registra su devolución.");
        }
        else if (prenda.Estado == EstadoPrenda.DeBaja)
        {
            ModelState.AddModelError(nameof(modelo.IdPrenda), "La prenda está de baja y no puede recibir mantenimiento.");
        }
        else if (prenda.Estado == EstadoPrenda.EnMantenimiento ||
                 await _db.Mantenimientos.AnyAsync(m => m.IdPrenda == modelo.IdPrenda && m.FechaSalidaReal == null))
        {
            ModelState.AddModelError(nameof(modelo.IdPrenda), "La prenda ya tiene una orden de mantenimiento activa.");
        }

        if (!ModelState.IsValid)
        {
            modelo.Prendas = await ObtenerPrendasDisponiblesAsync();
            return View(modelo);
        }

        var orden = new Mantenimiento
        {
            IdPrenda = prenda!.IdPrenda,
            FechaIngreso = modelo.FechaIngreso,
            FechaSalidaEstimada = modelo.FechaSalidaEstimada,
            TipoServicio = modelo.TipoServicio,
            CostoServicio = modelo.CostoServicio,
            Observaciones = LimpiarOpcional(modelo.Observaciones)
        };

        prenda.Estado = EstadoPrenda.EnMantenimiento;
        _db.Mantenimientos.Add(orden);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Orden de mantenimiento {IdMantenimiento} creada para {Codigo}.", orden.IdMantenimiento, prenda.CodigoUnico);
        TempData["Exito"] = $"La orden #{orden.IdMantenimiento:D4} para {prenda.CodigoUnico} quedó registrada.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["ActiveNav"] = "mantenimiento";

        var orden = await _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .FirstOrDefaultAsync(m => m.IdMantenimiento == id);

        if (orden is null)
        {
            return NotFound();
        }

        if (orden.FechaSalidaReal is not null)
        {
            TempData["Info"] = $"La orden #{orden.IdMantenimiento:D4} ya fue finalizada y no puede editarse.";
            return RedirectToAction(nameof(Index), new { estado = "finalizados" });
        }

        var modelo = new MantenimientoFormViewModel
        {
            IdMantenimiento = orden.IdMantenimiento,
            IdPrenda = orden.IdPrenda,
            Codigo = orden.Prenda?.CodigoUnico,
            Nombre = orden.Prenda?.Nombre,
            Categoria = orden.Prenda?.Categoria,
            Talla = orden.Prenda?.Talla,
            EstadoPrenda = orden.Prenda?.Estado ?? EstadoPrenda.EnMantenimiento,
            TipoServicio = orden.TipoServicio,
            FechaIngreso = orden.FechaIngreso,
            FechaSalidaEstimada = orden.FechaSalidaEstimada,
            CostoServicio = orden.CostoServicio,
            Observaciones = orden.Observaciones,
            Prendas = await ObtenerPrendasDisponiblesAsync(orden.IdPrenda)
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Edit(int id, MantenimientoFormViewModel modelo)
    {
        ViewData["ActiveNav"] = "mantenimiento";

        if (id != modelo.IdMantenimiento)
        {
            return BadRequest();
        }

        ValidarFechas(modelo);

        var orden = await _db.Mantenimientos
            .Include(m => m.Prenda)
            .FirstOrDefaultAsync(m => m.IdMantenimiento == id);

        if (orden is null)
        {
            return NotFound();
        }

        if (orden.FechaSalidaReal is not null)
        {
            TempData["Info"] = $"La orden #{orden.IdMantenimiento:D4} ya fue finalizada.";
            return RedirectToAction(nameof(Index), new { estado = "finalizados" });
        }

        if (modelo.IdPrenda != orden.IdPrenda)
        {
            ModelState.AddModelError(nameof(modelo.IdPrenda), "La prenda de una orden activa no puede cambiarse.");
        }

        if (!ModelState.IsValid)
        {
            modelo.Codigo = orden.Prenda?.CodigoUnico;
            modelo.Nombre = orden.Prenda?.Nombre;
            modelo.Categoria = orden.Prenda?.Categoria;
            modelo.Talla = orden.Prenda?.Talla;
            modelo.EstadoPrenda = orden.Prenda?.Estado ?? EstadoPrenda.EnMantenimiento;
            modelo.Prendas = await ObtenerPrendasDisponiblesAsync(orden.IdPrenda);
            return View(modelo);
        }

        orden.FechaIngreso = modelo.FechaIngreso;
        orden.FechaSalidaEstimada = modelo.FechaSalidaEstimada;
        orden.TipoServicio = modelo.TipoServicio;
        orden.CostoServicio = modelo.CostoServicio;
        orden.Observaciones = LimpiarOpcional(modelo.Observaciones);

        await _db.SaveChangesAsync();

        TempData["Exito"] = $"La orden #{orden.IdMantenimiento:D4} fue actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Finalizar(int id)
    {
        var orden = await _db.Mantenimientos
            .Include(m => m.Prenda)
            .FirstOrDefaultAsync(m => m.IdMantenimiento == id);

        if (orden is null)
        {
            return NotFound();
        }

        if (orden.FechaSalidaReal is not null)
        {
            TempData["Info"] = $"La orden #{orden.IdMantenimiento:D4} ya estaba finalizada.";
            return RedirectToAction(nameof(Index), new { estado = "finalizados" });
        }

        var ahora = DateTime.Now;
        orden.FechaSalidaReal = ahora;
        _db.Mantenimientos.Update(orden);

        var prendaDisponible = false;
        if (orden.Prenda is not null && orden.Prenda.Estado == EstadoPrenda.EnMantenimiento)
        {
            var otraOrdenActiva = await _db.Mantenimientos
                .AnyAsync(m => m.IdPrenda == orden.IdPrenda &&
                               m.IdMantenimiento != orden.IdMantenimiento &&
                               m.FechaSalidaReal == null);

            if (!otraOrdenActiva)
            {
                orden.Prenda.Estado = EstadoPrenda.Disponible;
                prendaDisponible = true;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Orden de mantenimiento {IdMantenimiento} finalizada.", orden.IdMantenimiento);
        TempData["Exito"] = prendaDisponible
            ? $"La orden #{orden.IdMantenimiento:D4} de {orden.Prenda?.CodigoUnico} fue finalizada. La prenda vuelve a estar disponible."
            : $"La orden #{orden.IdMantenimiento:D4} de {orden.Prenda?.CodigoUnico} fue finalizada.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Exportar(string? q, string? estado)
    {
        var ahora = DateTime.Now;
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var estadoTab = termino is not null && string.IsNullOrWhiteSpace(estado)
            ? "todos"
            : NormalizarEstado(estado);

        var consulta = _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .AsQueryable();

        if (termino is not null)
        {
            var tokens = termino.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var filtro = token;
                consulta = consulta.Where(m =>
                    (m.Prenda != null && m.Prenda.CodigoUnico.Contains(filtro)) ||
                    (m.Prenda != null && m.Prenda.Nombre.Contains(filtro)) ||
                    (m.Prenda != null && m.Prenda.Categoria.Contains(filtro)) ||
                    (m.Observaciones != null && m.Observaciones.Contains(filtro)));
            }
        }

        consulta = estadoTab switch
        {
            "vencidos" => consulta.Where(m => m.FechaSalidaReal == null && m.FechaSalidaEstimada < ahora),
            "finalizados" => consulta.Where(m => m.FechaSalidaReal != null),
            "todos" => consulta,
            _ => consulta.Where(m => m.FechaSalidaReal == null)
        };

        var filas = await consulta
            .OrderBy(m => m.FechaSalidaReal == null ? 0 : 1)
            .ThenBy(m => m.FechaSalidaEstimada)
            .ToListAsync();

        var contenido = new StringBuilder();
        contenido.AppendLine("Orden;Codigo;Prenda;Servicio;Ingreso;Salida estimada;Salida real;Estado;Costo");

        foreach (var fila in filas.Select(m => MapearFila(m, ahora)))
        {
            contenido.AppendLine(string.Join(';',
                fila.IdMantenimiento.ToString("D4", CultureInfo.InvariantCulture),
                fila.Codigo,
                Limpiar(fila.Nombre),
                fila.TipoServicioTexto,
                fila.FechaIngreso.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                fila.FechaSalidaEstimada.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                fila.FechaSalidaReal?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                fila.Estado,
                fila.CostoServicio.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"mantenimientos-lucyvest-{DateTime.Now:yyyyMMdd}.csv");
    }

    private async Task<List<PrendaMantenimientoOpcionViewModel>> ObtenerPrendasDisponiblesAsync(int? incluirId = null)
    {
        var idsConOrdenActiva = _db.Mantenimientos
            .Where(m => m.FechaSalidaReal == null)
            .Select(m => m.IdPrenda);

        return await _db.Prendas
            .AsNoTracking()
            .Where(p => p.Activo &&
                        (p.IdPrenda == incluirId ||
                         (p.Estado == EstadoPrenda.Disponible && !idsConOrdenActiva.Contains(p.IdPrenda))))
            .OrderBy(p => p.CodigoUnico)
            .Select(p => new PrendaMantenimientoOpcionViewModel
            {
                IdPrenda = p.IdPrenda,
                Codigo = p.CodigoUnico,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Talla = p.Talla,
                Estado = p.Estado,
                TieneOrdenActiva = false
            })
            .ToListAsync();
    }

    private void ValidarFechas(MantenimientoFormViewModel modelo)
    {
        if (modelo.FechaSalidaEstimada < modelo.FechaIngreso)
        {
            ModelState.AddModelError(nameof(modelo.FechaSalidaEstimada),
                "La salida estimada no puede ser anterior al ingreso.");
        }

        if (modelo.FechaIngreso > DateTime.Now.AddMinutes(10))
        {
            // Se permite un pequeño margen por la precisión de los campos datetime-local.
            ModelState.AddModelError(nameof(modelo.FechaIngreso),
                "La fecha de ingreso no puede estar en el futuro.");
        }
    }

    private static MantenimientoFilaViewModel MapearFila(Mantenimiento orden, DateTime ahora)
    {
        var prenda = orden.Prenda;
        var salida = orden.FechaSalidaReal ?? ahora;
        var dias = Math.Max(0, (int)Math.Floor((salida - orden.FechaIngreso).TotalDays));
        var enCurso = orden.FechaSalidaReal is null;
        var vencido = enCurso && orden.FechaSalidaEstimada < ahora;

        return new MantenimientoFilaViewModel
        {
            IdMantenimiento = orden.IdMantenimiento,
            IdPrenda = orden.IdPrenda,
            Codigo = prenda?.CodigoUnico ?? "—",
            Nombre = prenda?.Nombre ?? "Prenda eliminada",
            Categoria = prenda?.Categoria ?? string.Empty,
            Talla = prenda?.Talla ?? string.Empty,
            TipoServicio = orden.TipoServicio,
            FechaIngreso = orden.FechaIngreso,
            FechaSalidaEstimada = orden.FechaSalidaEstimada,
            FechaSalidaReal = orden.FechaSalidaReal,
            CostoServicio = orden.CostoServicio,
            Observaciones = orden.Observaciones,
            DiasEnTaller = dias,
            EnCurso = enCurso,
            Vencido = vencido,
            Estado = !enCurso ? "Finalizado" : vencido ? "Vencido" : "En curso",
            PillClass = !enCurso ? "pill-completado" : vencido ? "pill-vencido" : "pill-atencion"
        };
    }

    private static string NormalizarEstado(string? estado) => estado?.Trim().ToLowerInvariant() switch
    {
        "vencidos" => "vencidos",
        "finalizados" => "finalizados",
        "todos" => "todos",
        _ => "encurso"
    };

    private static string? LimpiarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string Limpiar(string valor) =>
        valor.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
}

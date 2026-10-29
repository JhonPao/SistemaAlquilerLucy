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
/// Supervisión financiera, reporte de inventario y trazabilidad operativa (RF-08).
/// Los importes se leen de los contratos y liquidaciones ya registrados.
/// </summary>
[Authorize(Roles = RolesReportes)]
public class ReportesController : Controller
{
    private const string RolesReportes = DbInitializer.RolAdministradora + "," + DbInitializer.RolVendedor;

    private static readonly string[] Meses =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "set", "oct", "nov", "dic"];

    private readonly ApplicationDbContext _db;

    public ReportesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta)
    {
        ViewData["ActiveNav"] = "reportes";

        var (inicio, fin, invalido) = NormalizarRango(desde, hasta);
        var finExclusivo = fin.AddDays(1);
        var ahora = DateTime.Now;

        var alquileres = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Devolucion).ThenInclude(d => d!.Penalidad)
            .Where(a => a.FechaAlquiler >= inicio && a.FechaAlquiler < finExclusivo)
            .OrderByDescending(a => a.FechaAlquiler)
            .ToListAsync();

        var mantenimientos = await _db.Mantenimientos
            .AsNoTracking()
            .Include(m => m.Prenda)
            .Where(m => m.FechaIngreso >= inicio && m.FechaIngreso < finExclusivo)
            .OrderByDescending(m => m.FechaIngreso)
            .ToListAsync();

        var devolucionesPeriodo = await _db.Devoluciones
            .AsNoTracking()
            .Include(d => d.Alquiler).ThenInclude(a => a!.Cliente)
            .Include(d => d.Penalidad)
            .Where(d => d.FechaDevolucionReal >= inicio && d.FechaDevolucionReal < finExclusivo)
            .OrderByDescending(d => d.FechaDevolucionReal)
            .ToListAsync();

        var garantiasCustodia = await _db.Alquileres
            .Where(a => a.Devolucion == null && a.EstadoContrato != EstadoContrato.Cerrado)
            .SumAsync(a => (decimal?)a.TotalGarantia) ?? 0m;

        var abiertos = await _db.Alquileres
            .CountAsync(a => a.Devolucion == null && a.EstadoContrato != EstadoContrato.Cerrado);
        var vencidos = await _db.Alquileres
            .CountAsync(a => a.Devolucion == null &&
                             a.EstadoContrato != EstadoContrato.Cerrado &&
                             a.FechaDevolucionPactada < ahora);

        var estados = await _db.Prendas
            .AsNoTracking()
            .GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync();

        int Cantidad(EstadoPrenda estado) => estados.FirstOrDefault(e => e.Estado == estado)?.Cantidad ?? 0;

        var modelo = new ReportesIndexViewModel
        {
            Desde = inicio,
            Hasta = fin,
            RangoInvalido = invalido,
            Contratos = alquileres.Count,
            Devoluciones = devolucionesPeriodo.Count,
            ContratosAbiertos = abiertos,
            ContratosVencidos = vencidos,
            IngresosAlquiler = alquileres.Sum(a => a.Subtotal),
            GarantiasCustodia = garantiasCustodia,
            Penalidades = devolucionesPeriodo.Sum(d => d.Penalidad?.TotalPenalidad ?? 0m),
            Reembolsos = devolucionesPeriodo.Sum(d => d.MontoReembolso),
            CostosMantenimiento = mantenimientos.Sum(m => m.CostoServicio),
            PrendasDisponibles = Cantidad(EstadoPrenda.Disponible),
            PrendasAlquiladas = Cantidad(EstadoPrenda.Alquilada),
            PrendasEnMantenimiento = Cantidad(EstadoPrenda.EnMantenimiento),
            PrendasDeBaja = Cantidad(EstadoPrenda.DeBaja),
            TotalPrendas = estados.Sum(e => e.Cantidad)
        };

        modelo.TopClientes = alquileres
            .Where(a => a.Cliente is not null)
            .GroupBy(a => new { a.IdCliente, a.Cliente!.Nombres, a.Cliente.Apellidos, a.Cliente.Dni })
            .Select(g => new ReporteClienteViewModel
            {
                IdCliente = g.Key.IdCliente,
                Cliente = $"{g.Key.Nombres} {g.Key.Apellidos}".Trim(),
                Dni = g.Key.Dni,
                Contratos = g.Count(),
                Ingresos = g.Sum(a => a.Subtotal)
            })
            .OrderByDescending(c => c.Ingresos)
            .ThenBy(c => c.Cliente)
            .Take(5)
            .ToList();

        var maxCliente = modelo.TopClientes.Select(c => c.Ingresos).DefaultIfEmpty(0m).Max();
        foreach (var cliente in modelo.TopClientes)
        {
            cliente.Altura = maxCliente == 0 ? 0 : Math.Round(cliente.Ingresos * 100m / maxCliente, 1);
        }

        var series = await ConstruirSerieAsync(ahora);
        var maxSerie = series.Select(s => s.Ingresos).DefaultIfEmpty(0m).Max();
        foreach (var punto in series)
        {
            punto.Altura = maxSerie == 0 ? 0 : Math.Round(punto.Ingresos * 100m / maxSerie, 1);
        }
        modelo.Serie = series;

        var actividad = new List<ReporteActividadViewModel>();
        foreach (var alquiler in alquileres.Take(4))
        {
            var vencido = alquiler.Devolucion is null &&
                          alquiler.EstadoContrato != EstadoContrato.Cerrado &&
                          alquiler.FechaDevolucionPactada < ahora;
            var devuelto = alquiler.Devolucion is not null || alquiler.EstadoContrato == EstadoContrato.Cerrado;

            actividad.Add(new ReporteActividadViewModel
            {
                Fecha = alquiler.FechaAlquiler,
                Tipo = "Alquiler",
                Titulo = $"{alquiler.CodigoContrato} · {alquiler.Cliente?.NombreCompleto}",
                Detalle = devuelto
                    ? "Contrato cerrado con devolución registrada."
                    : vencido
                        ? "Contrato vencido pendiente de recepción."
                        : "Contrato activo dentro del plazo.",
                Monto = alquiler.Subtotal,
                PillClass = devuelto ? "pill-completado" : vencido ? "pill-vencido" : "pill-vigente",
                Controller = "Alquileres",
                Action = "ContratoDigital",
                RouteId = alquiler.IdAlquiler
            });
        }

        foreach (var devolucion in devolucionesPeriodo.Take(4))
        {
            var contrato = devolucion.Alquiler;
            actividad.Add(new ReporteActividadViewModel
            {
                Fecha = devolucion.FechaDevolucionReal,
                Tipo = "Devolución",
                Titulo = $"{contrato?.CodigoContrato ?? "—"} · {contrato?.Cliente?.NombreCompleto}",
                Detalle = devolucion.Penalidad is { TotalPenalidad: > 0 }
                    ? "Liquidación con mora o cargos por inspección."
                    : "Prenda recibida sin cargos adicionales.",
                Monto = devolucion.Penalidad?.TotalPenalidad ?? 0m,
                PillClass = devolucion.Penalidad is { TotalPenalidad: > 0 } ? "pill-atencion" : "pill-completado",
                Controller = "Devoluciones",
                Action = "Liquidacion",
                RouteId = devolucion.IdDevolucion
            });
        }

        foreach (var orden in mantenimientos.Take(4))
        {
            actividad.Add(new ReporteActividadViewModel
            {
                Fecha = orden.FechaIngreso,
                Tipo = "Mantenimiento",
                Titulo = $"Orden #{orden.IdMantenimiento:D4} · {orden.Prenda?.CodigoUnico}",
                Detalle = $"{orden.TipoServicio.Etiqueta()} · {orden.Prenda?.Nombre}",
                Monto = orden.CostoServicio,
                PillClass = orden.FechaSalidaReal is null ? "pill-atencion" : "pill-completado",
                Controller = "Mantenimientos",
                Action = "Index",
                RouteId = null
            });
        }

        modelo.Actividad = actividad
            .OrderByDescending(a => a.Fecha)
            .Take(8)
            .ToList();

        return View(modelo);
    }

    public async Task<IActionResult> Ingresos(DateTime? desde, DateTime? hasta)
    {
        ViewData["ActiveNav"] = "reportes";
        var (inicio, fin, invalido) = NormalizarRango(desde, hasta);
        var modelo = await ConstruirIngresosAsync(inicio, fin);
        modelo.RangoInvalido = invalido;
        return View(modelo);
    }

    public async Task<IActionResult> Stock(string? q, EstadoPrenda? estado, string? categoria)
    {
        ViewData["ActiveNav"] = "reportes";
        var modelo = await ConstruirStockAsync(q, estado, categoria);
        return View(modelo);
    }

    public async Task<IActionResult> ExportarIngresos(DateTime? desde, DateTime? hasta)
    {
        var (inicio, fin, _) = NormalizarRango(desde, hasta);
        var modelo = await ConstruirIngresosAsync(inicio, fin);

        var contenido = new StringBuilder();
        contenido.AppendLine("Contrato;Fecha;Cliente;DNI;Alquiler;Garantia;Total recibido;Penalidad;Reembolso;Estado");

        foreach (var fila in modelo.Filas)
        {
            contenido.AppendLine(string.Join(';',
                fila.Contrato,
                fila.Fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Limpiar(fila.Cliente),
                fila.Dni,
                fila.Subtotal.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Garantia.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Total.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Penalidad.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Reembolso.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Estado));
        }

        contenido.AppendLine();
        contenido.AppendLine(string.Join(';',
            "TOTALES", "", "", "",
            modelo.TotalAlquiler.ToString("0.00", CultureInfo.InvariantCulture),
            modelo.TotalGarantia.ToString("0.00", CultureInfo.InvariantCulture),
            modelo.TotalRecibido.ToString("0.00", CultureInfo.InvariantCulture),
            modelo.TotalPenalidades.ToString("0.00", CultureInfo.InvariantCulture),
            modelo.TotalReembolsos.ToString("0.00", CultureInfo.InvariantCulture),
            ""));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"reporte-ingresos-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> ExportarStock(string? q, EstadoPrenda? estado, string? categoria)
    {
        var modelo = await ConstruirStockAsync(q, estado, categoria);

        var contenido = new StringBuilder();
        contenido.AppendLine("Codigo;Prenda;Categoria;Talla;Estado;Precio alquiler;Garantia");

        foreach (var fila in modelo.Filas)
        {
            contenido.AppendLine(string.Join(';',
                fila.Codigo,
                Limpiar(fila.Nombre),
                Limpiar(fila.Categoria),
                Limpiar(fila.Talla),
                fila.Estado.Etiqueta(),
                fila.PrecioAlquiler.ToString("0.00", CultureInfo.InvariantCulture),
                fila.CostoGarantia.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"reporte-inventario-lucyvest-{DateTime.Now:yyyyMMdd}.csv");
    }

    private async Task<ReportesIngresosViewModel> ConstruirIngresosAsync(DateTime inicio, DateTime fin)
    {
        var finExclusivo = fin.AddDays(1);
        var ahora = DateTime.Now;

        var alquileres = await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Devolucion).ThenInclude(d => d!.Penalidad)
            .Where(a => a.FechaAlquiler >= inicio && a.FechaAlquiler < finExclusivo)
            .OrderByDescending(a => a.FechaAlquiler)
            .ToListAsync();

        var filas = alquileres.Select(a =>
        {
            var devuelto = a.Devolucion is not null || a.EstadoContrato == EstadoContrato.Cerrado;
            var vencido = !devuelto && a.FechaDevolucionPactada < ahora;

            return new ReporteIngresoFilaViewModel
            {
                IdAlquiler = a.IdAlquiler,
                Contrato = a.CodigoContrato,
                Cliente = a.Cliente?.NombreCompleto ?? "Cliente eliminado",
                Dni = a.Cliente?.Dni ?? string.Empty,
                Fecha = a.FechaAlquiler,
                Subtotal = a.Subtotal,
                Garantia = a.TotalGarantia,
                Total = a.MontoTotal,
                Penalidad = a.Devolucion?.Penalidad?.TotalPenalidad ?? 0m,
                Reembolso = a.Devolucion?.MontoReembolso ?? 0m,
                Estado = devuelto ? "Devuelto" : vencido ? "Vencido" : "Vigente",
                PillClass = devuelto ? "pill-completado" : vencido ? "pill-vencido" : "pill-vigente",
                TieneDevolucion = a.Devolucion is not null
            };
        }).ToList();

        return new ReportesIngresosViewModel
        {
            Desde = inicio,
            Hasta = fin,
            Filas = filas,
            TotalAlquiler = filas.Sum(f => f.Subtotal),
            TotalGarantia = filas.Sum(f => f.Garantia),
            TotalRecibido = filas.Sum(f => f.Total),
            TotalPenalidades = filas.Sum(f => f.Penalidad),
            TotalReembolsos = filas.Sum(f => f.Reembolso)
        };
    }

    private async Task<ReportesStockViewModel> ConstruirStockAsync(string? q, EstadoPrenda? estado, string? categoria)
    {
        var consulta = _db.Prendas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termino = q.Trim();
            consulta = consulta.Where(p =>
                p.CodigoUnico.Contains(termino) ||
                p.Nombre.Contains(termino) ||
                p.Categoria.Contains(termino) ||
                p.Talla.Contains(termino));
        }

        if (estado is not null)
        {
            consulta = consulta.Where(p => p.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            consulta = consulta.Where(p => p.Categoria == categoria);
        }

        var filas = await consulta
            .OrderBy(p => p.CodigoUnico)
            .Select(p => new ReporteStockFilaViewModel
            {
                IdPrenda = p.IdPrenda,
                Codigo = p.CodigoUnico,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Talla = p.Talla,
                Estado = p.Estado,
                PrecioAlquiler = p.PrecioAlquiler,
                CostoGarantia = p.CostoGarantia
            })
            .ToListAsync();

        var todos = await _db.Prendas.AsNoTracking().ToListAsync();

        return new ReportesStockViewModel
        {
            Q = q,
            Estado = estado,
            Categoria = categoria,
            Filas = filas,
            Total = todos.Count,
            Disponibles = todos.Count(p => p.Estado == EstadoPrenda.Disponible),
            Alquiladas = todos.Count(p => p.Estado == EstadoPrenda.Alquilada),
            EnMantenimiento = todos.Count(p => p.Estado == EstadoPrenda.EnMantenimiento),
            DeBaja = todos.Count(p => p.Estado == EstadoPrenda.DeBaja),
            Categorias = todos.Select(p => p.Categoria).Distinct().OrderBy(c => c).ToList()
        };
    }

    private async Task<List<ReporteSerieViewModel>> ConstruirSerieAsync(DateTime ahora)
    {
        var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
        var inicioSerie = inicioMes.AddMonths(-5);
        var finSerie = inicioMes.AddMonths(1);

        var agrupado = await _db.Alquileres
            .AsNoTracking()
            .Where(a => a.FechaAlquiler >= inicioSerie && a.FechaAlquiler < finSerie)
            .GroupBy(a => new { a.FechaAlquiler.Year, a.FechaAlquiler.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Ingresos = g.Sum(a => a.Subtotal),
                Contratos = g.Count()
            })
            .ToListAsync();

        var serie = new List<ReporteSerieViewModel>();
        for (var i = 0; i < 6; i++)
        {
            var mes = inicioSerie.AddMonths(i);
            var dato = agrupado.FirstOrDefault(a => a.Year == mes.Year && a.Month == mes.Month);
            serie.Add(new ReporteSerieViewModel
            {
                Etiqueta = $"{Meses[mes.Month - 1]} {mes:yy}",
                Ingresos = dato?.Ingresos ?? 0m,
                Contratos = dato?.Contratos ?? 0
            });
        }

        return serie;
    }

    private static (DateTime Inicio, DateTime Fin, bool Invalido) NormalizarRango(DateTime? desde, DateTime? hasta)
    {
        var hoy = DateTime.Today;
        var inicio = desde?.Date ?? new DateTime(hoy.Year, hoy.Month, 1);
        var fin = hasta?.Date ?? hoy;
        var invalido = fin < inicio;

        if (invalido)
        {
            fin = inicio;
        }

        if ((fin - inicio).TotalDays > 3660)
        {
            inicio = fin.AddYears(-10);
        }

        return (inicio, fin, invalido);
    }

    private static string Limpiar(string valor) =>
        valor.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
}

using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.Services;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Gestión de arrendatarios (RF-03). La ficha combina la consulta por DNI o
/// teléfono con el historial de contratos y el consentimiento de la Ley N° 29733.
/// </summary>
[Authorize]
public class ClientesController : Controller
{
    private const string RolesGestion = DbInitializer.RolAdministradora + "," + DbInitializer.RolVendedor;

    private readonly ApplicationDbContext _db;
    private readonly IReglasNegocioService _reglas;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(
        ApplicationDbContext db,
        IReglasNegocioService reglas,
        ILogger<ClientesController> logger)
    {
        _db = db;
        _reglas = reglas;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int? id)
    {
        var hoy = DateTime.Today;
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var totalClientes = await _db.Clientes.CountAsync();

        var consulta = _db.Clientes.AsNoTracking();
        if (termino is not null)
        {
            consulta = consulta.Where(c =>
                c.Dni.Contains(termino) ||
                c.Telefono.Contains(termino) ||
                c.Nombres.Contains(termino) ||
                c.Apellidos.Contains(termino));
        }

        var resumenes = await consulta
            .OrderBy(c => !c.Activo ? 3
                : c.Alquileres.Any(a => a.EstadoContrato != EstadoContrato.Cerrado && a.FechaDevolucionPactada < hoy) ? 0
                : c.Alquileres.Any(a => a.EstadoContrato != EstadoContrato.Cerrado) ? 1
                : 2)
            .ThenBy(c => c.Apellidos)
            .ThenBy(c => c.Nombres)
            .Select(c => new
            {
                c.IdCliente,
                c.Dni,
                c.Nombres,
                c.Apellidos,
                c.Telefono,
                c.Activo,
                Activos = c.Alquileres.Count(a => a.EstadoContrato != EstadoContrato.Cerrado),
                Vencidos = c.Alquileres.Count(a =>
                    a.EstadoContrato != EstadoContrato.Cerrado && a.FechaDevolucionPactada < hoy)
            })
            .ToListAsync();

        var seleccionadoId = id.HasValue && resumenes.Any(r => r.IdCliente == id.Value)
            ? id.Value
            : resumenes.FirstOrDefault()?.IdCliente;

        ClienteDetalleViewModel? detalle = null;
        if (seleccionadoId.HasValue)
        {
            var cliente = await _db.Clientes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.Alquileres).ThenInclude(a => a.Detalles).ThenInclude(d => d.Prenda)
                .Include(c => c.Alquileres).ThenInclude(a => a.Devolucion)
                .FirstOrDefaultAsync(c => c.IdCliente == seleccionadoId.Value);

            if (cliente is not null)
            {
                var politica = await _db.ConfiguracionesPolitica
                    .AsNoTracking()
                    .OrderBy(p => p.IdConfiguracion)
                    .FirstOrDefaultAsync();

                detalle = ConstruirDetalle(cliente, politica, hoy);
            }
        }

        var modelo = new ClientesIndexViewModel
        {
            Q = termino,
            TotalClientes = totalClientes,
            Clientes = resumenes.Select(r => new ClienteResumenViewModel
            {
                IdCliente = r.IdCliente,
                NombreCompleto = $"{r.Nombres} {r.Apellidos}".Trim(),
                Dni = r.Dni,
                Telefono = FormatearTelefono(r.Telefono),
                Iniciales = Iniciales(r.Nombres, r.Apellidos),
                Activo = r.Activo,
                EstadoResumen = ResumenEstado(r.Activo, r.Vencidos, r.Activos),
                ClaseResumen = !r.Activo ? string.Empty : r.Vencidos > 0 ? "is-overdue" : string.Empty
            }).ToList(),
            Detalle = detalle
        };

        return View(modelo);
    }

    /// <summary>
    /// La ficha completa vive en Index (maestro-detalle). Mantenemos la ruta
    /// Details para enlaces externos y compatibilidad con la estructura del proyecto.
    /// </summary>
    [HttpGet]
    public IActionResult Details(int id) => RedirectToAction(nameof(Index), new { id });

    [HttpGet]
    [Authorize(Roles = RolesGestion)]
    public IActionResult Create() => View(new ClienteFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Create(ClienteFormViewModel modelo)
    {
        Normalizar(modelo);
        await ValidarDniUnicoAsync(modelo.Dni, null);

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var cliente = modelo.AEntidad();
        cliente.Activo = true;
        cliente.FechaRegistro = DateTime.Now;

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cliente {Dni} registrado por {Usuario}.", cliente.Dni, User.Identity?.Name);
        TempData["Exito"] = $"El cliente {cliente.NombreCompleto} se registró correctamente.";
        return RedirectToAction(nameof(Index), new { id = cliente.IdCliente });
    }

    [HttpGet]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Edit(int id)
    {
        var cliente = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == id);
        if (cliente is null)
        {
            return NotFound();
        }

        var modelo = ClienteFormViewModel.DesdeEntidad(cliente);
        modelo.Activo = cliente.Activo;
        modelo.TieneContratosActivos = await _db.Alquileres
            .AnyAsync(a => a.IdCliente == id && a.EstadoContrato != EstadoContrato.Cerrado);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Edit(int id, ClienteFormViewModel modelo)
    {
        if (modelo.IdCliente != 0 && modelo.IdCliente != id)
        {
            return BadRequest();
        }

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id);
        if (cliente is null)
        {
            return NotFound();
        }

        Normalizar(modelo);
        await ValidarDniUnicoAsync(modelo.Dni, id);

        modelo.Activo = cliente.Activo;
        modelo.TieneContratosActivos = await _db.Alquileres
            .AnyAsync(a => a.IdCliente == id && a.EstadoContrato != EstadoContrato.Cerrado);

        if (!ModelState.IsValid)
        {
            modelo.IdCliente = id;
            return View(modelo);
        }

        cliente.Dni = modelo.Dni;
        cliente.Nombres = modelo.Nombres;
        cliente.Apellidos = modelo.Apellidos;
        cliente.Telefono = modelo.Telefono;
        cliente.Direccion = modelo.Direccion;
        cliente.ConsentimientoDatos = modelo.ConsentimientoDatos;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Ficha del cliente {Dni} actualizada por {Usuario}.", cliente.Dni, User.Identity?.Name);
        TempData["Exito"] = "La ficha del cliente se actualizó correctamente.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> DarDeBaja(int id)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id);
        if (cliente is null)
        {
            return NotFound();
        }

        if (!cliente.Activo)
        {
            TempData["Error"] = "El cliente ya se encuentra inactivo.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var tieneContratosActivos = await _db.Alquileres
            .AnyAsync(a => a.IdCliente == id && a.EstadoContrato != EstadoContrato.Cerrado);

        if (tieneContratosActivos)
        {
            TempData["Error"] = "No se puede dar de baja al cliente porque tiene contratos activos.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        cliente.Activo = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cliente {Dni} dado de baja por {Usuario}.", cliente.Dni, User.Identity?.Name);
        TempData["Exito"] = "El cliente se marcó como inactivo. Su historial se conserva.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> Reactivar(int id)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id);
        if (cliente is null)
        {
            return NotFound();
        }

        cliente.Activo = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cliente {Dni} reactivado por {Usuario}.", cliente.Dni, User.Identity?.Name);
        TempData["Exito"] = "El cliente volvió a quedar activo.";
        return RedirectToAction(nameof(Index), new { id });
    }

    private ClienteDetalleViewModel ConstruirDetalle(
        Cliente cliente,
        ConfiguracionPolitica? politica,
        DateTime hoy)
    {
        var ahora = DateTime.Now;
        var alquileres = cliente.Alquileres
            .OrderByDescending(a => a.FechaAlquiler)
            .ThenByDescending(a => a.IdAlquiler)
            .ToList();

        var devoluciones = alquileres.Where(a => a.Devolucion is not null).ToList();
        var puntuales = devoluciones.Count(a =>
            a.Devolucion!.FechaDevolucionReal.Date <= a.FechaDevolucionPactada.Date);

        var vencidos = alquileres
            .Where(a => a.Devolucion is null &&
                        a.EstadoContrato != EstadoContrato.Cerrado &&
                        a.FechaDevolucionPactada.Date < hoy)
            .ToList();

        var mora = 0m;
        var diasMora = 0;
        if (politica is not null)
        {
            foreach (var alquiler in vencidos)
            {
                var liquidacion = _reglas.CalcularLiquidacion(
                    alquiler.FechaDevolucionPactada,
                    ahora,
                    EstadoFisicoRecepcion.Optimo,
                    politica);

                mora += liquidacion.MontoMora;
                diasMora += liquidacion.DiasRetraso;
            }
        }

        var historial = alquileres.Select(a =>
        {
            var devuelto = a.Devolucion is not null;
            var vencido = !devuelto &&
                          (a.EstadoContrato == EstadoContrato.Vencido ||
                           (a.EstadoContrato == EstadoContrato.Activo &&
                            a.FechaDevolucionPactada.Date < hoy));

            var estado = devuelto ? "Devuelto"
                : vencido ? "Vencido"
                : a.EstadoContrato == EstadoContrato.Activo ? "Vigente"
                : "Cerrado";

            var pill = devuelto ? "pill-completado"
                : vencido ? "pill-vencido"
                : a.EstadoContrato == EstadoContrato.Activo ? "pill-vigente"
                : "pill-neutral";

            string? puntualidad = null;
            if (a.Devolucion is not null)
            {
                puntualidad = a.Devolucion.FechaDevolucionReal.Date <= a.FechaDevolucionPactada.Date
                    ? "A tiempo"
                    : "Con retraso";
            }

            return new ClienteAlquilerViewModel
            {
                IdAlquiler = a.IdAlquiler,
                Contrato = $"LV-{a.IdAlquiler:D4}",
                Prendas = a.Detalles.Count == 0
                    ? "Sin detalle"
                    : string.Join(" + ", a.Detalles.Select(d => d.Prenda?.CodigoUnico ?? "—")),
                Importe = a.Subtotal,
                FechaRetorno = a.FechaDevolucionPactada,
                Estado = estado,
                PillClass = pill,
                Puntualidad = puntualidad
            };
        }).ToList();

        var nota = vencidos.Count > 0
            ? $"La puntualidad considera contratos devueltos. LV-{vencidos[0].IdAlquiler:D4} sigue pendiente y no forma parte del indicador."
            : devoluciones.Count == 0
                ? "Aún no hay devoluciones registradas; la puntualidad se calculará con el primer retorno."
                : "La puntualidad considera únicamente contratos con devolución registrada.";

        return new ClienteDetalleViewModel
        {
            IdCliente = cliente.IdCliente,
            NombreCompleto = cliente.NombreCompleto,
            Dni = cliente.Dni,
            Telefono = FormatearTelefono(cliente.Telefono),
            Direccion = cliente.Direccion,
            FechaRegistro = cliente.FechaRegistro,
            Activo = cliente.Activo,
            ConsentimientoDatos = cliente.ConsentimientoDatos,
            ContratosTotal = alquileres.Count,
            DevolucionesPuntuales = puntuales,
            DevolucionesTotal = devoluciones.Count,
            MoraActiva = vencidos.Count,
            MoraEstimada = mora,
            DiasMora = diasMora,
            TarifaMora = politica?.TarifaMoraDiaria ?? 0m,
            FechaCorte = ahora,
            NotaPuntualidad = nota,
            Historial = historial
        };
    }

    private async Task ValidarDniUnicoAsync(string dni, int? idActual)
    {
        var existe = await _db.Clientes.AnyAsync(c =>
            c.Dni == dni && (!idActual.HasValue || c.IdCliente != idActual.Value));

        if (existe)
        {
            ModelState.AddModelError(
                nameof(ClienteFormViewModel.Dni),
                $"Ya existe un cliente registrado con el DNI {dni}.");
        }
    }

    private static void Normalizar(ClienteFormViewModel modelo)
    {
        modelo.Dni = (modelo.Dni ?? string.Empty).Trim();
        modelo.Nombres = (modelo.Nombres ?? string.Empty).Trim();
        modelo.Apellidos = (modelo.Apellidos ?? string.Empty).Trim();
        modelo.Direccion = string.IsNullOrWhiteSpace(modelo.Direccion) ? null : modelo.Direccion.Trim();

        var digitos = new string((modelo.Telefono ?? string.Empty).Where(char.IsDigit).ToArray());
        modelo.Telefono = digitos;
    }

    private static string ResumenEstado(bool activo, int vencidos, int activos)
    {
        if (!activo)
        {
            return "Cliente inactivo";
        }

        if (vencidos > 0)
        {
            return vencidos == 1 ? "1 alquiler vencido" : $"{vencidos} alquileres vencidos";
        }

        if (activos > 0)
        {
            return activos == 1 ? "1 alquiler vigente" : $"{activos} alquileres vigentes";
        }

        return "Sin contratos activos";
    }

    private static string Iniciales(string nombres, string apellidos)
    {
        var inicialNombre = nombres.Trim().FirstOrDefault();
        var inicialApellido = apellidos.Trim().FirstOrDefault();

        var texto = $"{inicialNombre}{inicialApellido}".Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(texto) ? "?" : texto;
    }

    public static string FormatearTelefono(string? telefono)
    {
        var digitos = new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length switch
        {
            9 => $"{digitos[..3]} {digitos[3..6]} {digitos[6..]}",
            7 => $"{digitos[..3]} {digitos[3..]}",
            _ => telefono ?? string.Empty
        };
    }
}

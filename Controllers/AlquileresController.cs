using System.Data;
using System.Text;
using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.Services;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Alquileres y contratos digitales (RF-04). El listado prioriza los contratos
/// abiertos y el alta valida la disponibilidad física antes de emitir el contrato.
/// </summary>
[Authorize]
public class AlquileresController : Controller
{
    private const int PageSize = 8;
    private const string RolesGestion = DbInitializer.RolAdministradora + "," + DbInitializer.RolVendedor;

    private static readonly string[] MesesCortos =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "set", "oct", "nov", "dic"];

    private readonly ApplicationDbContext _db;
    private readonly IReglasNegocioService _reglas;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AlquileresController> _logger;

    public AlquileresController(
        ApplicationDbContext db,
        IReglasNegocioService reglas,
        UserManager<ApplicationUser> userManager,
        ILogger<AlquileresController> logger)
    {
        _db = db;
        _reglas = reglas;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        string? estado,
        DateTime? desde,
        DateTime? hasta,
        int page = 1,
        int? id = null)
    {
        var hoy = DateTime.Today;
        var desdeFecha = (desde ?? hoy.AddDays(-14)).Date;
        var hastaFecha = (hasta ?? hoy).Date;
        if (desdeFecha > hastaFecha)
        {
            (desdeFecha, hastaFecha) = (hastaFecha, desdeFecha);
        }

        var estadoTab = NormalizarEstado(estado);
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var baseQuery = _db.Alquileres
            .AsNoTracking()
            .Where(a => a.FechaAlquiler >= desdeFecha && a.FechaAlquiler < hastaFecha.AddDays(1));
        baseQuery = AplicarBusqueda(baseQuery, termino);

        var conteoTodos = await baseQuery.CountAsync();
        var conteoAbiertos = await baseQuery.CountAsync(a => a.EstadoContrato != EstadoContrato.Cerrado);
        var conteoVencidos = await baseQuery.CountAsync(a =>
            a.EstadoContrato != EstadoContrato.Cerrado &&
            a.FechaDevolucionPactada.Date < hoy);
        var conteoDevueltos = await baseQuery.CountAsync(a =>
            a.EstadoContrato == EstadoContrato.Cerrado || a.Devolucion != null);

        var filtrada = AplicarEstado(baseQuery, estadoTab, hoy);
        var totalFiltrado = await filtrada.CountAsync();
        var totalPages = totalFiltrado == 0 ? 1 : (int)Math.Ceiling(totalFiltrado / (double)PageSize);
        page = Math.Clamp(page, 1, totalPages);

        var entidades = await filtrada
            .Include(a => a.Cliente)
            .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
            .OrderBy(a => a.EstadoContrato == EstadoContrato.Cerrado ? 1 : 0)
            .ThenBy(a => a.FechaDevolucionPactada)
            .ThenBy(a => a.IdAlquiler)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var filas = entidades.Select(a => CrearFila(a, hoy)).ToList();

        var seleccionadoId = id ?? filas.FirstOrDefault()?.IdAlquiler;
        AlquilerSeleccionViewModel? seleccionado = null;
        if (seleccionadoId.HasValue)
        {
            var detalle = await _db.Alquileres
                .AsNoTracking()
                .Include(a => a.Cliente)
                .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
                .FirstOrDefaultAsync(a => a.IdAlquiler == seleccionadoId.Value);

            if (detalle is not null)
            {
                seleccionado = CrearSeleccion(detalle, hoy);
            }
        }

        var politica = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(p => p.IdConfiguracion)
            .FirstOrDefaultAsync();

        var global = await _db.Alquileres
            .AsNoTracking()
            .Select(a => new
            {
                a.IdAlquiler,
                a.EstadoContrato,
                a.FechaDevolucionPactada,
                a.Subtotal,
                a.TotalGarantia,
                a.IdCliente,
                ClienteNombres = a.Cliente!.Nombres,
                ClienteApellidos = a.Cliente!.Apellidos,
                Cantidad = a.Detalles.Count,
                TieneDevolucion = a.Devolucion != null
            })
            .ToListAsync();

        var abiertos = global.Where(a => a.EstadoContrato != EstadoContrato.Cerrado).ToList();
        var vencidos = abiertos
            .Where(a => a.FechaDevolucionPactada.Date < hoy)
            .OrderBy(a => a.FechaDevolucionPactada)
            .ToList();
        var devueltos = global
            .Where(a => a.EstadoContrato == EstadoContrato.Cerrado || a.TieneDevolucion)
            .ToList();

        var moraEstimada = vencidos.Sum(v => CalcularMora(v.FechaDevolucionPactada, politica));
        var alerta = vencidos.FirstOrDefault();
        AlquilerAlertaViewModel? alertaVm = null;
        if (alerta is not null)
        {
            var liquidacion = politica is null
                ? null
                : _reglas.CalcularLiquidacion(
                    alerta.FechaDevolucionPactada,
                    DateTime.Now,
                    EstadoFisicoRecepcion.Optimo,
                    politica);

            alertaVm = new AlquilerAlertaViewModel
            {
                IdAlquiler = alerta.IdAlquiler,
                Contrato = CodigoContrato(alerta.IdAlquiler),
                Cliente = $"{alerta.ClienteNombres} {alerta.ClienteApellidos}".Trim(),
                CantidadPrendas = alerta.Cantidad,
                FechaRetorno = alerta.FechaDevolucionPactada,
                FechaCorte = DateTime.Now,
                DiasMora = liquidacion?.DiasRetraso ?? 0,
                MoraEstimada = liquidacion?.MontoMora ?? 0m
            };
        }

        var modelo = new AlquileresIndexViewModel
        {
            Q = termino,
            EstadoTab = estadoTab,
            Desde = desdeFecha,
            Hasta = hastaFecha,
            KpiVigentes = abiertos.Count,
            KpiPendientes = abiertos.Count,
            KpiVencidos = vencidos.Count,
            KpiDevueltos = devueltos.Count,
            PrendasPorRetornar = abiertos.Sum(a => a.Cantidad),
            MoraEstimada = moraEstimada,
            TarifaMora = politica?.TarifaMoraDiaria ?? 0m,
            Alerta = alertaVm,
            ConteoTodos = conteoTodos,
            ConteoVigentes = conteoAbiertos,
            ConteoPendientes = conteoAbiertos,
            ConteoVencidos = conteoVencidos,
            ConteoDevueltos = conteoDevueltos,
            Filas = filas,
            Seleccionado = seleccionado,
            Page = page,
            TotalPages = totalPages,
            TotalFiltrado = totalFiltrado
        };

        return View(modelo);
    }

    [HttpGet]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Create()
    {
        var modelo = new NuevoAlquilerViewModel
        {
            FechaAlquiler = AhoraSinSegundos(),
            MedioPago = "Transferencia",
            ReferenciaPago = GenerarReferencia()
        };
        modelo.FechaDevolucionPactada = modelo.FechaAlquiler.AddDays(2);

        await CargarDatosFormularioAsync(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Create(NuevoAlquilerViewModel modelo)
    {
        modelo.PrendasSeleccionadas = (modelo.PrendasSeleccionadas ?? []).Distinct().ToArray();
        modelo.Observaciones = string.IsNullOrWhiteSpace(modelo.Observaciones) ? null : modelo.Observaciones.Trim();
        modelo.MedioPago = string.IsNullOrWhiteSpace(modelo.MedioPago) ? string.Empty : modelo.MedioPago.Trim();
        modelo.ReferenciaPago = string.IsNullOrWhiteSpace(modelo.ReferenciaPago) ? null : modelo.ReferenciaPago.Trim();

        await CargarDatosFormularioAsync(modelo);

        if (modelo.FechaAlquiler == default)
        {
            ModelState.AddModelError(nameof(modelo.FechaAlquiler), "Indica la fecha y hora de salida.");
        }
        if (modelo.FechaDevolucionPactada == default)
        {
            ModelState.AddModelError(nameof(modelo.FechaDevolucionPactada), "Indica la fecha y hora de retorno pactada.");
        }
        else
        {
            if (modelo.FechaDevolucionPactada <= modelo.FechaAlquiler)
            {
                ModelState.AddModelError(nameof(modelo.FechaDevolucionPactada),
                    "El retorno debe ser posterior a la salida.");
            }
            if (modelo.FechaDevolucionPactada <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(modelo.FechaDevolucionPactada),
                    "El retorno pactado debe estar en el futuro.");
            }
        }

        if (modelo.IdCliente <= 0 || modelo.Cliente is null)
        {
            ModelState.AddModelError(nameof(modelo.IdCliente), "Selecciona un cliente activo para el alquiler.");
        }
        else
        {
            if (!modelo.Cliente.Activo)
            {
                ModelState.AddModelError(nameof(modelo.IdCliente), "El cliente está dado de baja y no puede alquilar.");
            }
            if (!modelo.Cliente.ConsentimientoDatos)
            {
                ModelState.AddModelError(nameof(modelo.IdCliente),
                    "El cliente no cuenta con consentimiento para el tratamiento de datos (Ley N° 29733).");
            }
        }

        if (modelo.PrendasSeleccionadas.Length == 0)
        {
            ModelState.AddModelError(nameof(modelo.PrendasSeleccionadas), "Selecciona al menos una prenda disponible.");
        }

        if (!NuevoAlquilerViewModel.MediosPago.Contains(modelo.MedioPago))
        {
            ModelState.AddModelError(nameof(modelo.MedioPago), "Selecciona un medio de pago válido.");
        }
        else if (modelo.MedioPago != "Efectivo" && string.IsNullOrWhiteSpace(modelo.ReferenciaPago))
        {
            ModelState.AddModelError(nameof(modelo.ReferenciaPago),
                "Ingresa la referencia del pago o selecciona Efectivo.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var ids = modelo.PrendasSeleccionadas;
            var prendas = await _db.Prendas
                .Where(p => ids.Contains(p.IdPrenda))
                .OrderBy(p => p.CodigoUnico)
                .ToListAsync();

            if (prendas.Count != ids.Length)
            {
                await transaccion.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Alguna de las prendas seleccionadas ya no existe. Vuelve a elegirlas.");
                await CargarDatosFormularioAsync(modelo);
                return View(modelo);
            }

            if (prendas.Any(p => !p.Activo || p.Estado != EstadoPrenda.Disponible))
            {
                await transaccion.RollbackAsync();
                ModelState.AddModelError(string.Empty,
                    "Una o más prendas dejaron de estar disponibles mientras preparabas el contrato. Revisa la selección.");
                await CargarDatosFormularioAsync(modelo);
                return View(modelo);
            }

            var alquiler = new Alquiler
            {
                IdCliente = modelo.IdCliente,
                FechaAlquiler = modelo.FechaAlquiler,
                FechaDevolucionPactada = modelo.FechaDevolucionPactada,
                Subtotal = prendas.Sum(p => p.PrecioAlquiler),
                TotalGarantia = prendas.Sum(p => p.CostoGarantia),
                EstadoContrato = EstadoContrato.Activo,
                Observaciones = modelo.Observaciones,
                MedioPago = modelo.MedioPago,
                ReferenciaPago = modelo.ReferenciaPago,
                UserId = _userManager.GetUserId(User)
            };
            alquiler.MontoTotal = alquiler.Subtotal + alquiler.TotalGarantia;

            foreach (var prenda in prendas)
            {
                alquiler.Detalles.Add(new DetalleAlquiler
                {
                    IdPrenda = prenda.IdPrenda,
                    PrecioUnitarioAlquiler = prenda.PrecioAlquiler,
                    GarantiaUnitaria = prenda.CostoGarantia
                });
                prenda.Estado = EstadoPrenda.Alquilada;
            }

            _db.Alquileres.Add(alquiler);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            var codigo = CodigoContrato(alquiler.IdAlquiler);
            _logger.LogInformation(
                "Contrato {Contrato} emitido por {Usuario} para el cliente {Cliente} con {Cantidad} prendas.",
                codigo, User.Identity?.Name, modelo.Cliente?.NombreCompleto, prendas.Count);

            TempData["Exito"] = $"El contrato {codigo} se emitió correctamente. " +
                $"{prendas.Count} {(prendas.Count == 1 ? "prenda quedó" : "prendas quedaron")} en alquiler.";
            return RedirectToAction(nameof(ContratoDigital), new { id = alquiler.IdAlquiler });
        }
        catch (DbUpdateException ex)
        {
            await transaccion.RollbackAsync();
            _logger.LogError(ex, "No se pudo emitir el contrato para el cliente {Cliente}.", modelo.IdCliente);
            ModelState.AddModelError(string.Empty,
                "No se pudo confirmar el contrato porque una prenda cambió de estado. Vuelve a intentarlo.");
            await CargarDatosFormularioAsync(modelo);
            return View(modelo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ContratoDigital(int id)
    {
        var alquiler = await _db.Alquileres
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Cliente)
            .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
            .Include(a => a.Devolucion)
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.IdAlquiler == id);

        if (alquiler is null)
        {
            return NotFound();
        }

        var politica = await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(p => p.IdConfiguracion)
            .FirstOrDefaultAsync();

        var hoy = DateTime.Today;
        var devuelto = alquiler.Devolucion is not null || alquiler.EstadoContrato == EstadoContrato.Cerrado;
        var vencido = !devuelto && alquiler.FechaDevolucionPactada.Date < hoy;
        var estado = devuelto ? "Devuelto" : vencido ? "Vencido" : "Vigente";
        var pill = devuelto ? "pill-completado" : vencido ? "pill-vencido" : "pill-vigente";
        var nota = devuelto
            ? "Contrato cerrado. La garantía se liquidó al registrar el retorno."
            : vencido
                ? "Pendiente de devolución. La mora se liquida al registrar el retorno real."
                : "Contrato en curso. Registra la devolución al recibir las prendas.";

        var telefono = FormatearTelefono(alquiler.Cliente?.Telefono ?? string.Empty);

        var modelo = new ContratoDigitalViewModel
        {
            IdAlquiler = alquiler.IdAlquiler,
            Codigo = CodigoContrato(alquiler.IdAlquiler),
            FechaEmision = alquiler.FechaAlquiler,
            FechaEntrega = alquiler.FechaAlquiler,
            FechaRetorno = alquiler.FechaDevolucionPactada,
            FechaDevolucionReal = alquiler.Devolucion?.FechaDevolucionReal,
            FechaAceptacion = alquiler.FechaAlquiler,
            ClienteNombre = alquiler.Cliente?.NombreCompleto ?? "Cliente no disponible",
            ClienteDni = alquiler.Cliente?.Dni ?? "—",
            ClienteTelefono = telefono,
            ClienteDireccion = alquiler.Cliente?.Direccion,
            Prendas = alquiler.Detalles
                .OrderBy(d => d.Prenda?.CodigoUnico)
                .Select(d => new ContratoPrendaViewModel
                {
                    Codigo = d.Prenda?.CodigoUnico ?? "—",
                    Nombre = d.Prenda?.Nombre ?? "Prenda no disponible",
                    Talla = d.Prenda?.Talla ?? "—",
                    Precio = d.PrecioUnitarioAlquiler,
                    Garantia = d.GarantiaUnitaria
                })
                .ToList(),
            Subtotal = alquiler.Subtotal,
            TotalGarantia = alquiler.TotalGarantia,
            MontoTotal = alquiler.MontoTotal,
            Observaciones = alquiler.Observaciones,
            MedioPago = alquiler.MedioPago,
            ReferenciaPago = alquiler.ReferenciaPago,
            Vendedor = alquiler.Usuario?.NombreCompleto,
            Estado = estado,
            PillClass = pill,
            NotaEstado = nota,
            Devuelto = devuelto,
            Vencido = vencido,
            Consentimiento = alquiler.Cliente?.ConsentimientoDatos ?? false,
            AceptacionRegistrada = true,
            TarifaMora = politica?.TarifaMoraDiaria ?? 0m,
            CostoBaseReparacion = politica?.CostoBaseReparacion ?? 0m,
            PorcentajeDanioLeve = politica?.PorcentajeDanioLeve ?? 0m,
            PorcentajeDanioGrave = politica?.PorcentajeDanioGrave ?? 0m,
            TelefonoDestino = string.IsNullOrWhiteSpace(telefono) ? null : telefono,
            DevolucionObservaciones = alquiler.Devolucion?.Observaciones
        };

        return View(modelo);
    }

    [HttpGet]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> BuscarClientes(string? q)
    {
        var hoy = DateTime.Today;
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var consulta = _db.Clientes
            .AsNoTracking()
            .Where(c => c.Activo);

        if (termino is not null)
        {
            consulta = consulta.Where(c =>
                c.Dni.Contains(termino) ||
                c.Telefono.Contains(termino) ||
                c.Nombres.Contains(termino) ||
                c.Apellidos.Contains(termino));
        }

        var clientes = await consulta
            .OrderBy(c => c.Apellidos)
            .ThenBy(c => c.Nombres)
            .Take(8)
            .Select(c => new
            {
                c.IdCliente,
                c.Nombres,
                c.Apellidos,
                c.Dni,
                c.Telefono,
                c.Direccion,
                c.ConsentimientoDatos,
                c.FechaRegistro
            })
            .ToListAsync();

        var ids = clientes.Select(c => c.IdCliente).ToList();
        var vencidos = await _db.Alquileres
            .AsNoTracking()
            .Where(a => ids.Contains(a.IdCliente) &&
                        a.EstadoContrato != EstadoContrato.Cerrado &&
                        a.FechaDevolucionPactada.Date < hoy)
            .GroupBy(a => a.IdCliente)
            .Select(g => new { IdCliente = g.Key, Total = g.Count() })
            .ToListAsync();

        var resultado = clientes.Select(c =>
        {
            var mora = vencidos.FirstOrDefault(v => v.IdCliente == c.IdCliente)?.Total ?? 0;
            return new
            {
                id = c.IdCliente,
                nombre = $"{c.Nombres} {c.Apellidos}".Trim(),
                dni = c.Dni,
                telefono = FormatearTelefono(c.Telefono),
                direccion = string.IsNullOrWhiteSpace(c.Direccion) ? "No registrada" : c.Direccion,
                iniciales = Iniciales(c.Nombres, c.Apellidos),
                consentimiento = c.ConsentimientoDatos,
                fechaConsentimiento = FormatearFechaCorta(c.FechaRegistro),
                alDia = mora == 0,
                moraActiva = mora
            };
        }).ToList();

        return Json(resultado);
    }

    [HttpGet]
    [Authorize(Roles = RolesGestion)]
    public async Task<IActionResult> Exportar(string? q, string? estado, DateTime? desde, DateTime? hasta)
    {
        var hoy = DateTime.Today;
        var desdeFecha = (desde ?? hoy.AddDays(-14)).Date;
        var hastaFecha = (hasta ?? hoy).Date;
        if (desdeFecha > hastaFecha)
        {
            (desdeFecha, hastaFecha) = (hastaFecha, desdeFecha);
        }

        var consulta = _db.Alquileres
            .AsNoTracking()
            .Where(a => a.FechaAlquiler >= desdeFecha && a.FechaAlquiler < hastaFecha.AddDays(1));
        consulta = AplicarBusqueda(consulta, string.IsNullOrWhiteSpace(q) ? null : q.Trim());
        consulta = AplicarEstado(consulta, NormalizarEstado(estado), hoy);

        var alquileres = await consulta
            .Include(a => a.Cliente)
            .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
            .OrderBy(a => a.FechaAlquiler)
            .ThenBy(a => a.IdAlquiler)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Contrato;DNI;Cliente;Prendas;Salida;Retorno pactado;Alquiler;Garantia;Total;Estado;Medio de pago;Referencia");

        foreach (var alquiler in alquileres)
        {
            var estadoVisual = EstadoVisual(alquiler, hoy);
            var prendas = string.Join(" + ", alquiler.Detalles
                .OrderBy(d => d.Prenda?.CodigoUnico)
                .Select(d => d.Prenda?.CodigoUnico ?? "—"));

            sb.AppendLine(string.Join(';',
                Csv(CodigoContrato(alquiler.IdAlquiler)),
                Csv(alquiler.Cliente?.Dni ?? string.Empty),
                Csv(alquiler.Cliente?.NombreCompleto ?? string.Empty),
                Csv(prendas),
                Csv(alquiler.FechaAlquiler.ToString("yyyy-MM-dd HH:mm")),
                Csv(alquiler.FechaDevolucionPactada.ToString("yyyy-MM-dd HH:mm")),
                Csv(alquiler.Subtotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(alquiler.TotalGarantia.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(alquiler.MontoTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(estadoVisual.Estado),
                Csv(alquiler.MedioPago),
                Csv(alquiler.ReferenciaPago ?? string.Empty)));
        }

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"contratos-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    private async Task CargarDatosFormularioAsync(NuevoAlquilerViewModel modelo)
    {
        if (modelo.IdCliente > 0)
        {
            var cliente = await _db.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdCliente == modelo.IdCliente);

            if (cliente is not null)
            {
                modelo.Cliente = await CrearClienteSeleccionadoAsync(cliente);
            }
        }

        var disponibles = await _db.Prendas
            .AsNoTracking()
            .Where(p => p.Activo && p.Estado == EstadoPrenda.Disponible)
            .OrderBy(p => p.CodigoUnico)
            .Select(p => new PrendaAlquilerViewModel
            {
                IdPrenda = p.IdPrenda,
                CodigoUnico = p.CodigoUnico,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Talla = p.Talla,
                PrecioAlquiler = p.PrecioAlquiler,
                CostoGarantia = p.CostoGarantia
            })
            .ToListAsync();

        var seleccionadas = modelo.PrendasSeleccionadas.ToHashSet();
        foreach (var prenda in disponibles)
        {
            prenda.Seleccionada = seleccionadas.Contains(prenda.IdPrenda);
        }
        modelo.Prendas = disponibles;

        var noDisponibles = await _db.Prendas
            .AsNoTracking()
            .Where(p => !p.Activo || p.Estado != EstadoPrenda.Disponible)
            .OrderBy(p => p.CodigoUnico)
            .Select(p => new
            {
                p.CodigoUnico,
                p.Nombre,
                p.Talla,
                p.Estado,
                p.Activo,
                Contrato = p.Detalles
                    .Where(d => d.Alquiler!.EstadoContrato != EstadoContrato.Cerrado)
                    .OrderByDescending(d => d.Alquiler!.FechaAlquiler)
                    .Select(d => (int?)d.IdAlquiler)
                    .FirstOrDefault()
            })
            .ToListAsync();

        modelo.NoDisponibles = noDisponibles
            .Select(p => new PrendaNoDisponibleViewModel
            {
                CodigoUnico = p.CodigoUnico,
                Nombre = p.Nombre,
                Talla = p.Talla,
                Motivo = MotivoNoDisponible(p.Activo, p.Estado, p.Contrato)
            })
            .ToList();
    }

    private async Task<ClienteSeleccionadoViewModel> CrearClienteSeleccionadoAsync(Cliente cliente)
    {
        var hoy = DateTime.Today;
        var fechasAbiertas = await _db.Alquileres
            .AsNoTracking()
            .Where(a => a.IdCliente == cliente.IdCliente && a.EstadoContrato != EstadoContrato.Cerrado)
            .Select(a => a.FechaDevolucionPactada)
            .ToListAsync();

        return new ClienteSeleccionadoViewModel
        {
            IdCliente = cliente.IdCliente,
            NombreCompleto = cliente.NombreCompleto,
            Dni = cliente.Dni,
            Telefono = FormatearTelefono(cliente.Telefono),
            Direccion = cliente.Direccion,
            ConsentimientoDatos = cliente.ConsentimientoDatos,
            FechaRegistro = cliente.FechaRegistro,
            Activo = cliente.Activo,
            MoraActiva = fechasAbiertas.Count(f => f.Date < hoy),
            Iniciales = Iniciales(cliente.Nombres, cliente.Apellidos)
        };
    }

    private static AlquilerFilaViewModel CrearFila(Alquiler alquiler, DateTime hoy)
    {
        var estado = EstadoVisual(alquiler, hoy);
        return new AlquilerFilaViewModel
        {
            IdAlquiler = alquiler.IdAlquiler,
            Contrato = CodigoContrato(alquiler.IdAlquiler),
            Cliente = alquiler.Cliente?.NombreCompleto ?? "Cliente no disponible",
            Dni = alquiler.Cliente?.Dni ?? "—",
            Prendas = alquiler.Detalles
                .OrderBy(d => d.Prenda?.CodigoUnico)
                .Select(d => d.Prenda?.CodigoUnico ?? "—")
                .ToList(),
            FechaSalida = alquiler.FechaAlquiler,
            FechaRetorno = alquiler.FechaDevolucionPactada,
            Subtotal = alquiler.Subtotal,
            Garantia = alquiler.TotalGarantia,
            Estado = estado.Estado,
            PillClass = estado.PillClass,
            Vencido = estado.Vencido,
            Cerrado = estado.Cerrado
        };
    }

    private static AlquilerSeleccionViewModel CrearSeleccion(Alquiler alquiler, DateTime hoy)
    {
        var estado = EstadoVisual(alquiler, hoy);
        return new AlquilerSeleccionViewModel
        {
            IdAlquiler = alquiler.IdAlquiler,
            Contrato = CodigoContrato(alquiler.IdAlquiler),
            Cliente = alquiler.Cliente?.NombreCompleto ?? "Cliente no disponible",
            Dni = alquiler.Cliente?.Dni ?? "—",
            Telefono = FormatearTelefono(alquiler.Cliente?.Telefono ?? string.Empty),
            Prendas = alquiler.Detalles
                .OrderBy(d => d.Prenda?.CodigoUnico)
                .Select(d => new AlquilerPrendaViewModel
                {
                    Codigo = d.Prenda?.CodigoUnico ?? "—",
                    Nombre = d.Prenda?.Nombre ?? "Prenda no disponible",
                    Talla = d.Prenda?.Talla ?? "—",
                    Precio = d.PrecioUnitarioAlquiler,
                    Garantia = d.GarantiaUnitaria
                })
                .ToList(),
            FechaSalida = alquiler.FechaAlquiler,
            FechaRetorno = alquiler.FechaDevolucionPactada,
            Subtotal = alquiler.Subtotal,
            Garantia = alquiler.TotalGarantia,
            MontoTotal = alquiler.MontoTotal,
            Estado = estado.Estado,
            PillClass = estado.PillClass,
            Vencido = estado.Vencido,
            Cerrado = estado.Cerrado,
            Observaciones = alquiler.Observaciones,
            MedioPago = alquiler.MedioPago,
            ReferenciaPago = alquiler.ReferenciaPago
        };
    }

    private static (string Estado, string PillClass, bool Vencido, bool Cerrado) EstadoVisual(
        Alquiler alquiler,
        DateTime hoy)
    {
        var cerrado = alquiler.Devolucion is not null || alquiler.EstadoContrato == EstadoContrato.Cerrado;
        if (cerrado)
        {
            return ("Devuelto", "pill-completado", false, true);
        }

        if (alquiler.FechaDevolucionPactada.Date < hoy)
        {
            return ("Vencido", "pill-vencido", true, false);
        }

        return ("Vigente", "pill-vigente", false, false);
    }

    private decimal CalcularMora(DateTime fechaPactada, ConfiguracionPolitica? politica)
    {
        if (politica is null)
        {
            return 0m;
        }

        return _reglas.CalcularLiquidacion(
            fechaPactada,
            DateTime.Now,
            EstadoFisicoRecepcion.Optimo,
            politica).MontoMora;
    }

    private static IQueryable<Alquiler> AplicarBusqueda(IQueryable<Alquiler> consulta, string? termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return consulta;
        }

        var texto = termino.Trim();
        var idContrato = ParsearContrato(texto);

        if (idContrato.HasValue)
        {
            var id = idContrato.Value;
            return consulta.Where(a =>
                a.IdAlquiler == id ||
                a.Cliente!.Dni.Contains(texto) ||
                a.Cliente.Nombres.Contains(texto) ||
                a.Cliente.Apellidos.Contains(texto) ||
                a.Cliente.Telefono.Contains(texto));
        }

        return consulta.Where(a =>
            a.Cliente!.Dni.Contains(texto) ||
            a.Cliente.Nombres.Contains(texto) ||
            a.Cliente.Apellidos.Contains(texto) ||
            a.Cliente.Telefono.Contains(texto));
    }

    private static IQueryable<Alquiler> AplicarEstado(
        IQueryable<Alquiler> consulta,
        string estado,
        DateTime hoy) => estado switch
    {
        "vigentes" or "pendientes" => consulta.Where(a => a.EstadoContrato != EstadoContrato.Cerrado),
        "vencidos" => consulta.Where(a =>
            a.EstadoContrato != EstadoContrato.Cerrado &&
            a.FechaDevolucionPactada.Date < hoy),
        "devueltos" => consulta.Where(a =>
            a.EstadoContrato == EstadoContrato.Cerrado || a.Devolucion != null),
        _ => consulta
    };

    private static string NormalizarEstado(string? estado) =>
        estado is "vigentes" or "pendientes" or "vencidos" or "devueltos" ? estado : "todos";

    private static int? ParsearContrato(string valor)
    {
        var texto = valor.Trim().ToUpperInvariant();
        if (texto.StartsWith("LV-", StringComparison.Ordinal))
        {
            texto = texto[3..];
        }
        else if (texto.StartsWith("CTR-", StringComparison.Ordinal))
        {
            var guion = texto.LastIndexOf('-');
            texto = guion >= 0 ? texto[(guion + 1)..] : texto;
        }

        return int.TryParse(texto, out var id) && id is > 0 and < 1_000_000 ? id : null;
    }

    private static string MotivoNoDisponible(bool activo, EstadoPrenda estado, int? contrato)
    {
        if (!activo || estado == EstadoPrenda.DeBaja)
        {
            return "De baja";
        }

        return estado switch
        {
            EstadoPrenda.EnMantenimiento => "En Mantenimiento",
            EstadoPrenda.Alquilada when contrato.HasValue => $"Alquilada ({CodigoContrato(contrato.Value)})",
            EstadoPrenda.Alquilada => "Alquilada",
            _ => "No disponible"
        };
    }

    private static string CodigoContrato(int id) => $"LV-{id:D4}";

    private static string FormatearTelefono(string telefono)
    {
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        return digitos.Length == 9
            ? $"{digitos[..3]} {digitos[3..6]} {digitos[6..]}"
            : digitos;
    }

    private static string FormatearFechaCorta(DateTime fecha) =>
        $"{fecha.Day:00} {MesesCortos[fecha.Month - 1]} {fecha.Year}";

    private static string Iniciales(string nombres, string apellidos)
    {
        var inicialNombre = string.IsNullOrWhiteSpace(nombres) ? "?" : nombres.Trim()[..1];
        var inicialApellido = string.IsNullOrWhiteSpace(apellidos) ? "?" : apellidos.Trim()[..1];
        return $"{inicialNombre}{inicialApellido}".ToUpperInvariant();
    }

    private static DateTime AhoraSinSegundos()
    {
        var ahora = DateTime.Now;
        return new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0);
    }

    private static string GenerarReferencia() =>
        $"BCP-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 10000)}";

    private static string Csv(string? valor)
    {
        var texto = valor ?? string.Empty;
        return texto.Contains(';') || texto.Contains('"') || texto.Contains('\n')
            ? $"\"{texto.Replace("\"", "\"\"")}\""
            : texto;
    }
}

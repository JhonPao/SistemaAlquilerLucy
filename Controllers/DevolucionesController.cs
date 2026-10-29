using System.Data;
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
/// Recepción física, inspección de calidad y liquidación final (RF-05 / RF-06).
/// La mora y los cargos por deterioro se calculan exclusivamente en la capa de
/// negocio; la vista solo presenta el resultado.
/// </summary>
[Authorize]
public class DevolucionesController : Controller
{
    private const int PageSize = 8;

    private readonly ApplicationDbContext _db;
    private readonly IReglasNegocioService _reglas;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DevolucionesController> _logger;

    public DevolucionesController(
        ApplicationDbContext db,
        IReglasNegocioService reglas,
        UserManager<ApplicationUser> userManager,
        ILogger<DevolucionesController> logger)
    {
        _db = db;
        _reglas = reglas;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? estado, int page = 1)
    {
        ViewData["ActiveNav"] = "devoluciones";

        var hoy = DateTime.Today;
        var ahora = DateTime.Now;
        var politica = await ObtenerPoliticaAsync();
        var termino = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var estadoTab = termino is not null && string.IsNullOrWhiteSpace(estado)
            ? "todos"
            : NormalizarEstado(estado);

        var consulta = _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
            .Include(a => a.Devolucion).ThenInclude(d => d!.Penalidad)
            .AsSplitQuery();

        consulta = AplicarBusqueda(consulta, termino);

        var conteoTodos = await consulta.CountAsync();
        var conteoPendientes = await consulta.CountAsync(a =>
            a.Devolucion == null && a.EstadoContrato != EstadoContrato.Cerrado);
        var conteoVencidos = await consulta.CountAsync(a =>
            a.Devolucion == null &&
            a.EstadoContrato != EstadoContrato.Cerrado &&
            a.FechaDevolucionPactada < ahora);
        var conteoDevueltos = await consulta.CountAsync(a =>
            a.Devolucion != null || a.EstadoContrato == EstadoContrato.Cerrado);

        var filtrada = AplicarEstado(consulta, estadoTab, ahora);
        var totalFiltrado = await filtrada.CountAsync();
        var totalPages = totalFiltrado == 0 ? 1 : (int)Math.Ceiling(totalFiltrado / (double)PageSize);
        page = Math.Clamp(page, 1, totalPages);

        var entidades = await filtrada
            .OrderBy(a => a.Devolucion == null ? 0 : 1)
            .ThenBy(a => a.FechaDevolucionPactada)
            .ThenBy(a => a.IdAlquiler)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var filas = entidades.Select(a =>
        {
            var devuelto = a.Devolucion is not null;
            var vencido = !devuelto && a.FechaDevolucionPactada < ahora;
            var dias = devuelto
                ? a.Devolucion?.Penalidad?.DiasRetraso
                    ?? _reglas.CalcularDiasRetraso(a.FechaDevolucionPactada, a.Devolucion?.FechaDevolucionReal ?? ahora)
                : _reglas.CalcularDiasRetraso(a.FechaDevolucionPactada, ahora);
            var mora = devuelto
                ? a.Devolucion?.Penalidad?.MontoMora ?? 0m
                : politica is null ? 0m : _reglas.CalcularLiquidacion(
                    a.FechaDevolucionPactada,
                    ahora,
                    EstadoFisicoRecepcion.Optimo,
                    politica).MontoMora;

            var estadoVisual = devuelto
                ? "Devuelto"
                : vencido ? "Vencido" : "Pendiente";

            return new DevolucionFilaViewModel
            {
                IdAlquiler = a.IdAlquiler,
                IdDevolucion = a.Devolucion?.IdDevolucion,
                Contrato = a.CodigoContrato,
                Cliente = a.Cliente?.NombreCompleto ?? "Sin cliente",
                Dni = a.Cliente?.Dni ?? string.Empty,
                Prendas = a.Detalles
                    .OrderBy(d => d.Prenda?.CodigoUnico)
                    .Select(d => d.Prenda?.CodigoUnico ?? "—")
                    .ToList(),
                CantidadPrendas = a.Detalles.Count,
                FechaPactada = a.FechaDevolucionPactada,
                FechaReal = a.Devolucion?.FechaDevolucionReal,
                Garantia = a.TotalGarantia,
                MoraEstimada = mora,
                TotalPenalidad = a.Devolucion?.Penalidad?.TotalPenalidad ?? 0m,
                DiasRetraso = dias,
                Estado = estadoVisual,
                PillClass = devuelto ? "pill-completado" : vencido ? "pill-vencido" : "pill-vigente",
                Devuelto = devuelto
            };
        }).ToList();

        var pendientes = await _db.Alquileres
            .AsNoTracking()
            .Where(a => a.Devolucion == null && a.EstadoContrato != EstadoContrato.Cerrado)
            .Select(a => new
            {
                a.IdAlquiler,
                a.CodigoContrato,
                a.FechaDevolucionPactada,
                a.TotalGarantia,
                CantidadPrendas = a.Detalles.Count
            })
            .ToListAsync();

        var vencidos = pendientes
            .Where(a => a.FechaDevolucionPactada < ahora)
            .ToList();

        var moraTotal = politica is null
            ? 0m
            : vencidos.Sum(a => _reglas.CalcularLiquidacion(
                a.FechaDevolucionPactada,
                ahora,
                EstadoFisicoRecepcion.Optimo,
                politica).MontoMora);

        var devolucionesHoy = await _db.Devoluciones
            .AsNoTracking()
            .CountAsync(d => d.FechaDevolucionReal >= hoy && d.FechaDevolucionReal < hoy.AddDays(1));

        var alerta = vencidos
            .OrderBy(a => a.FechaDevolucionPactada)
            .Select(a => new AlertaDevolucionViewModel
            {
                IdAlquiler = a.IdAlquiler,
                Contrato = a.CodigoContrato,
                Cliente = string.Empty,
                CantidadPrendas = a.CantidadPrendas,
                FechaRetorno = a.FechaDevolucionPactada,
                FechaCorte = ahora,
                DiasMora = _reglas.CalcularDiasRetraso(a.FechaDevolucionPactada, ahora),
                MoraEstimada = politica is null
                    ? 0m
                    : _reglas.CalcularLiquidacion(
                        a.FechaDevolucionPactada,
                        ahora,
                        EstadoFisicoRecepcion.Optimo,
                        politica).MontoMora
            })
            .FirstOrDefault();

        if (alerta is not null)
        {
            var contratoAlerta = await _db.Alquileres
                .AsNoTracking()
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.IdAlquiler == alerta.IdAlquiler);
            alerta.Cliente = contratoAlerta?.Cliente?.NombreCompleto ?? "Cliente";
        }

        var modelo = new DevolucionesIndexViewModel
        {
            Q = termino,
            EstadoTab = estadoTab,
            Page = page,
            TotalPages = totalPages,
            TotalFiltrado = totalFiltrado,
            ConteoTodos = conteoTodos,
            ConteoPendientes = conteoPendientes,
            ConteoVencidos = conteoVencidos,
            ConteoDevueltos = conteoDevueltos,
            KpiPorRecibir = pendientes.Count,
            KpiVencidos = vencidos.Count,
            KpiDevueltosHoy = devolucionesHoy,
            PrendasPorRecibir = pendientes.Sum(a => a.CantidadPrendas),
            GarantiasEnCustodia = pendientes.Sum(a => a.TotalGarantia),
            MoraEstimada = moraTotal,
            TarifaMora = politica?.TarifaMoraDiaria ?? 0m,
            FechaCorte = ahora,
            Alerta = alerta,
            Filas = filas
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int id)
    {
        ViewData["ActiveNav"] = "devoluciones";

        var alquiler = await CargarContratoAsync(id);
        if (alquiler is null)
        {
            return NotFound();
        }

        if (alquiler.Devolucion is not null)
        {
            TempData["Info"] = $"El contrato {alquiler.CodigoContrato} ya tiene una devolución registrada.";
            return RedirectToAction(nameof(Liquidacion), new { id = alquiler.Devolucion.IdDevolucion });
        }

        if (alquiler.EstadoContrato == EstadoContrato.Cerrado)
        {
            TempData["Error"] = $"El contrato {alquiler.CodigoContrato} ya está cerrado y no admite una nueva devolución.";
            return RedirectToAction(nameof(Index));
        }

        var politica = await ObtenerPoliticaAsync() ?? new ConfiguracionPolitica();
        var modelo = PrepararModelo(alquiler, politica);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int id, RegistroDevolucionViewModel modelo)
    {
        ViewData["ActiveNav"] = "devoluciones";

        if (id <= 0)
        {
            id = modelo.IdAlquiler;
        }

        var alquiler = await CargarContratoAsync(id);
        if (alquiler is null)
        {
            return NotFound();
        }

        if (alquiler.Devolucion is not null)
        {
            TempData["Info"] = $"El contrato {alquiler.CodigoContrato} ya tiene una devolución registrada.";
            return RedirectToAction(nameof(Liquidacion), new { id = alquiler.Devolucion.IdDevolucion });
        }

        if (alquiler.EstadoContrato == EstadoContrato.Cerrado)
        {
            TempData["Error"] = $"El contrato {alquiler.CodigoContrato} ya está cerrado y no admite una nueva devolución.";
            return RedirectToAction(nameof(Index));
        }

        var politica = await ObtenerPoliticaAsync() ?? new ConfiguracionPolitica();

        if (!modelo.ClienteInformado)
        {
            ModelState.AddModelError(nameof(modelo.ClienteInformado),
                "Confirma que el cliente fue informado del desglose y del cargo aplicado.");
        }

        if (modelo.FechaDevolucionReal == default)
        {
            ModelState.AddModelError(nameof(modelo.FechaDevolucionReal), "Indica la fecha y hora reales de retorno.");
        }
        else
        {
            if (modelo.FechaDevolucionReal > DateTime.Now.AddMinutes(5))
            {
                ModelState.AddModelError(nameof(modelo.FechaDevolucionReal),
                    "La fecha de retorno real no puede estar en el futuro.");
            }

            if (modelo.FechaDevolucionReal < alquiler.FechaAlquiler)
            {
                ModelState.AddModelError(nameof(modelo.FechaDevolucionReal),
                    "La fecha de retorno real no puede ser anterior a la salida del contrato.");
            }
        }

        var mediosValidos = new[] { "Transferencia", "Yape/Plin", "Efectivo" };
        if (!mediosValidos.Contains(modelo.MedioReembolso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(modelo.MedioReembolso), "Selecciona un medio de reembolso válido.");
        }

        var inspecciones = (modelo.Prendas ?? new List<InspeccionPrendaViewModel>())
            .GroupBy(p => p.IdDetalle)
            .ToDictionary(g => g.Key, g => g.First());

        var detallesContrato = alquiler.Detalles.OrderBy(d => d.IdDetalle).ToList();
        if (detallesContrato.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El contrato no tiene prendas asociadas.");
        }
        else if (inspecciones.Count != detallesContrato.Count)
        {
            ModelState.AddModelError(string.Empty,
                "Debes registrar la inspección de todas las prendas del contrato.");
        }
        else if (detallesContrato.Any(d => !inspecciones.ContainsKey(d.IdDetalle)))
        {
            ModelState.AddModelError(string.Empty,
                "La inspección no coincide con las prendas del contrato. Vuelve a cargar la página.");
        }

        foreach (var inspeccion in inspecciones.Values)
        {
            if (!Enum.IsDefined(inspeccion.EstadoFisicoRecepcion))
            {
                ModelState.AddModelError(string.Empty, "Selecciona una condición válida para cada prenda.");
                break;
            }

            if (inspeccion.Observaciones?.Length > 300)
            {
                ModelState.AddModelError(string.Empty, "Las observaciones de una prenda no pueden superar 300 caracteres.");
                break;
            }
        }

        if (!ModelState.IsValid)
        {
            var modelInvalid = PrepararModelo(alquiler, politica, modelo);
            return View(modelInvalid);
        }

        var estados = detallesContrato
            .Select(d => inspecciones[d.IdDetalle].EstadoFisicoRecepcion)
            .ToList();
        var liquidacion = _reglas.CalcularLiquidacion(
            alquiler.FechaDevolucionPactada,
            modelo.FechaDevolucionReal,
            estados,
            politica);

        var reembolso = Math.Max(0m, alquiler.TotalGarantia - liquidacion.TotalPenalidad);
        var saldoPendiente = Math.Max(0m, liquidacion.TotalPenalidad - alquiler.TotalGarantia);
        var usuario = await _userManager.GetUserAsync(User);

        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var contrato = await _db.Alquileres
                .Include(a => a.Cliente)
                .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
                .Include(a => a.Devolucion)
                .FirstOrDefaultAsync(a => a.IdAlquiler == id);

            if (contrato is null)
            {
                await transaccion.RollbackAsync();
                return NotFound();
            }

            if (contrato.Devolucion is not null)
            {
                await transaccion.RollbackAsync();
                TempData["Info"] = $"El contrato {contrato.CodigoContrato} ya tiene una devolución registrada.";
                return RedirectToAction(nameof(Liquidacion), new { id = contrato.Devolucion.IdDevolucion });
            }

            if (contrato.EstadoContrato == EstadoContrato.Cerrado)
            {
                await transaccion.RollbackAsync();
                TempData["Error"] = "El contrato fue cerrado mientras se registraba la devolución.";
                return RedirectToAction(nameof(Index));
            }

            var devolucion = new Devolucion
            {
                IdAlquiler = contrato.IdAlquiler,
                FechaDevolucionReal = modelo.FechaDevolucionReal,
                EstadoFisicoRecepcion = PeorEstado(estados),
                Observaciones = string.IsNullOrWhiteSpace(modelo.Observaciones)
                    ? null
                    : modelo.Observaciones.Trim(),
                MedioReembolso = modelo.MedioReembolso,
                MontoReembolso = reembolso,
                SaldoPendiente = saldoPendiente,
                ClienteInformado = true,
                UserId = usuario?.Id,
                Penalidad = new Penalidad
                {
                    DiasRetraso = liquidacion.DiasRetraso,
                    TarifaMoraAplicada = liquidacion.TarifaMoraAplicada,
                    MontoMora = liquidacion.MontoMora,
                    CostoBaseAplicado = liquidacion.CostoBaseAplicado,
                    MontoDanio = liquidacion.MontoDanio,
                    TotalPenalidad = liquidacion.TotalPenalidad,
                    Liquidado = true
                }
            };

            foreach (var detalle in contrato.Detalles)
            {
                var inspeccion = inspecciones[detalle.IdDetalle];
                var cargo = _reglas.CalcularCargoDanio(inspeccion.EstadoFisicoRecepcion, politica);

                devolucion.Detalles.Add(new DetalleDevolucion
                {
                    IdDetalleAlquiler = detalle.IdDetalle,
                    EstadoFisicoRecepcion = inspeccion.EstadoFisicoRecepcion,
                    CargoDanio = cargo,
                    Observaciones = string.IsNullOrWhiteSpace(inspeccion.Observaciones)
                        ? null
                        : inspeccion.Observaciones.Trim()
                });

                if (detalle.Prenda is not null)
                {
                    if (inspeccion.EstadoFisicoRecepcion == EstadoFisicoRecepcion.Optimo)
                    {
                        detalle.Prenda.Estado = EstadoPrenda.Disponible;
                    }
                    else
                    {
                        detalle.Prenda.Estado = EstadoPrenda.EnMantenimiento;
                        _db.Mantenimientos.Add(new Mantenimiento
                        {
                            IdPrenda = detalle.Prenda.IdPrenda,
                            FechaIngreso = modelo.FechaDevolucionReal,
                            FechaSalidaEstimada = modelo.FechaDevolucionReal.Date.AddDays(2),
                            TipoServicio = inspeccion.EstadoFisicoRecepcion == EstadoFisicoRecepcion.DanioGrave
                                ? TipoServicio.Costura
                                : TipoServicio.Lavanderia,
                            CostoServicio = cargo,
                            Observaciones = string.IsNullOrWhiteSpace(inspeccion.Observaciones)
                                ? $"Derivado por la devolución del contrato {contrato.CodigoContrato}."
                                : inspeccion.Observaciones.Trim()
                        });
                    }
                }
            }

            contrato.EstadoContrato = EstadoContrato.Cerrado;
            _db.Devoluciones.Add(devolucion);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            _logger.LogInformation(
                "Devolución registrada para el contrato {Contrato}: mora {Mora}, cargos {Cargos}, reembolso {Reembolso}.",
                contrato.CodigoContrato,
                liquidacion.MontoMora,
                liquidacion.MontoDanio,
                reembolso);

            TempData["Exito"] = $"Devolución de {contrato.CodigoContrato} registrada. La liquidación quedó guardada.";
            return RedirectToAction(nameof(Liquidacion), new { id = devolucion.IdDevolucion });
        }
        catch (DbUpdateException ex)
        {
            await transaccion.RollbackAsync();
            _logger.LogError(ex, "No se pudo registrar la devolución del contrato {IdAlquiler}.", id);
            ModelState.AddModelError(string.Empty,
                "No se pudo registrar la devolución. Es posible que otro usuario la haya confirmado. Recarga e inténtalo de nuevo.");
            var modelError = PrepararModelo(alquiler, politica, modelo);
            return View(modelError);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Liquidacion(int id)
    {
        ViewData["ActiveNav"] = "devoluciones";

        var devolucion = await _db.Devoluciones
            .AsNoTracking()
            .Include(d => d.Usuario)
            .Include(d => d.Penalidad)
            .Include(d => d.Alquiler).ThenInclude(a => a!.Cliente)
            .Include(d => d.Alquiler).ThenInclude(a => a!.Detalles).ThenInclude(d => d.Prenda)
            .Include(d => d.Detalles).ThenInclude(d => d.DetalleAlquiler).ThenInclude(d => d!.Prenda)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.IdDevolucion == id);

        if (devolucion?.Alquiler is null)
        {
            return NotFound();
        }

        var alquiler = devolucion.Alquiler;
        var politica = await ObtenerPoliticaAsync();

        var items = devolucion.Detalles
            .OrderBy(d => d.DetalleAlquiler?.Prenda?.CodigoUnico)
            .Select(d => new ConstanciaItemViewModel
            {
                Codigo = d.DetalleAlquiler?.Prenda?.CodigoUnico ?? "—",
                Nombre = d.DetalleAlquiler?.Prenda?.Nombre ?? "Prenda",
                EstadoFisicoRecepcion = d.EstadoFisicoRecepcion,
                CargoDanio = d.CargoDanio,
                Observaciones = d.Observaciones
            })
            .ToList();

        if (items.Count == 0)
        {
            // Compatibilidad con devoluciones históricas anteriores a la inspección por prenda.
            items = alquiler.Detalles
                .OrderBy(d => d.Prenda?.CodigoUnico)
                .Select(d => new ConstanciaItemViewModel
                {
                    Codigo = d.Prenda?.CodigoUnico ?? "—",
                    Nombre = d.Prenda?.Nombre ?? "Prenda",
                    EstadoFisicoRecepcion = devolucion.EstadoFisicoRecepcion,
                    CargoDanio = 0m,
                    Observaciones = devolucion.Observaciones
                })
                .ToList();
        }

        var montoMora = devolucion.Penalidad?.MontoMora ?? 0m;
        var totalCargos = devolucion.Penalidad?.MontoDanio ?? items.Sum(i => i.CargoDanio);
        var totalPenalidad = devolucion.Penalidad?.TotalPenalidad ?? (montoMora + totalCargos);

        if (devolucion.Penalidad is null && politica is not null)
        {
            var calculo = _reglas.CalcularLiquidacion(
                alquiler.FechaDevolucionPactada,
                devolucion.FechaDevolucionReal,
                items.Select(i => i.EstadoFisicoRecepcion),
                politica);
            montoMora = calculo.MontoMora;
            totalCargos = calculo.MontoDanio;
            totalPenalidad = calculo.TotalPenalidad;
        }

        var modelo = new ConstanciaDevolucionViewModel
        {
            IdDevolucion = devolucion.IdDevolucion,
            IdAlquiler = alquiler.IdAlquiler,
            Contrato = alquiler.CodigoContrato,
            Cliente = alquiler.Cliente?.NombreCompleto ?? "Sin cliente",
            Dni = alquiler.Cliente?.Dni ?? string.Empty,
            Telefono = alquiler.Cliente?.Telefono,
            FechaPactada = alquiler.FechaDevolucionPactada,
            FechaReal = devolucion.FechaDevolucionReal,
            FechaRegistro = devolucion.FechaDevolucionReal,
            RecibidoPor = devolucion.Usuario?.NombreCompleto,
            Observaciones = devolucion.Observaciones,
            ClienteInformado = devolucion.ClienteInformado,
            Garantia = alquiler.TotalGarantia,
            MontoMora = montoMora,
            TotalCargos = totalCargos,
            TotalPenalidad = totalPenalidad,
            Reembolso = devolucion.MontoReembolso,
            SaldoPendiente = devolucion.SaldoPendiente,
            MedioReembolso = devolucion.MedioReembolso,
            DiasRetraso = devolucion.Penalidad?.DiasRetraso
                ?? _reglas.CalcularDiasRetraso(alquiler.FechaDevolucionPactada, devolucion.FechaDevolucionReal),
            Prendas = items
        };

        return View(modelo);
    }

    private async Task<Alquiler?> CargarContratoAsync(int id) =>
        await _db.Alquileres
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Detalles).ThenInclude(d => d.Prenda)
            .Include(a => a.Devolucion)
            .FirstOrDefaultAsync(a => a.IdAlquiler == id);

    private async Task<ConfiguracionPolitica?> ObtenerPoliticaAsync() =>
        await _db.ConfiguracionesPolitica
            .AsNoTracking()
            .OrderBy(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();

    private RegistroDevolucionViewModel PrepararModelo(
        Alquiler alquiler,
        ConfiguracionPolitica politica,
        RegistroDevolucionViewModel? existente = null)
    {
        var modelo = new RegistroDevolucionViewModel
        {
            IdAlquiler = alquiler.IdAlquiler,
            Contrato = alquiler.CodigoContrato,
            Cliente = alquiler.Cliente?.NombreCompleto ?? "Sin cliente",
            Dni = alquiler.Cliente?.Dni ?? string.Empty,
            Telefono = alquiler.Cliente?.Telefono,
            FechaAlquiler = alquiler.FechaAlquiler,
            FechaPactada = alquiler.FechaDevolucionPactada,
            Subtotal = alquiler.Subtotal,
            TotalGarantia = alquiler.TotalGarantia,
            CantidadPrendas = alquiler.Detalles.Count,
            ObservacionesEntrega = alquiler.Observaciones,
            FechaDevolucionReal = existente is not null && existente.FechaDevolucionReal != default
                ? existente.FechaDevolucionReal
                : DateTime.Now,
            MedioReembolso = string.IsNullOrWhiteSpace(existente?.MedioReembolso)
                ? "Transferencia"
                : existente.MedioReembolso,
            ClienteInformado = existente?.ClienteInformado ?? false,
            Observaciones = existente?.Observaciones
        };

        var publicadas = (existente?.Prendas ?? new List<InspeccionPrendaViewModel>())
            .GroupBy(p => p.IdDetalle)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var detalle in alquiler.Detalles.OrderBy(d => d.Prenda?.CodigoUnico))
        {
            var item = new InspeccionPrendaViewModel
            {
                IdDetalle = detalle.IdDetalle,
                IdPrenda = detalle.IdPrenda,
                Codigo = detalle.Prenda?.CodigoUnico ?? "—",
                Nombre = detalle.Prenda?.Nombre ?? "Prenda",
                Talla = detalle.Prenda?.Talla ?? "—",
                PrecioAlquiler = detalle.PrecioUnitarioAlquiler,
                GarantiaUnitaria = detalle.GarantiaUnitaria,
                EstadoFisicoRecepcion = EstadoFisicoRecepcion.Optimo
            };

            if (publicadas.TryGetValue(detalle.IdDetalle, out var publicada))
            {
                item.EstadoFisicoRecepcion = publicada.EstadoFisicoRecepcion;
                item.Observaciones = publicada.Observaciones;
            }

            modelo.Prendas.Add(item);
        }

        CalcularResumen(modelo, politica);
        return modelo;
    }

    private void CalcularResumen(RegistroDevolucionViewModel modelo, ConfiguracionPolitica politica)
    {
        foreach (var prenda in modelo.Prendas)
        {
            prenda.CargoDanio = _reglas.CalcularCargoDanio(prenda.EstadoFisicoRecepcion, politica);
        }

        var liquidacion = _reglas.CalcularLiquidacion(
            modelo.FechaPactada,
            modelo.FechaDevolucionReal,
            modelo.Prendas.Select(p => p.EstadoFisicoRecepcion).ToList(),
            politica);

        modelo.DiasRetraso = liquidacion.DiasRetraso;
        modelo.DiferenciaHoras = Math.Max(0d, (modelo.FechaDevolucionReal - modelo.FechaPactada).TotalHours);
        modelo.TarifaMora = liquidacion.TarifaMoraAplicada;
        modelo.MontoMora = liquidacion.MontoMora;
        modelo.CostoBaseReparacion = liquidacion.CostoBaseAplicado;
        modelo.PorcentajeDanioLeve = politica.PorcentajeDanioLeve;
        modelo.PorcentajeDanioGrave = politica.PorcentajeDanioGrave;
        modelo.TotalCargos = modelo.Prendas.Sum(p => p.CargoDanio);
        modelo.TotalPenalidad = liquidacion.TotalPenalidad;
        modelo.Reembolso = Math.Max(0m, modelo.TotalGarantia - liquidacion.TotalPenalidad);
        modelo.SaldoPendiente = Math.Max(0m, liquidacion.TotalPenalidad - modelo.TotalGarantia);
    }

    private static EstadoFisicoRecepcion PeorEstado(IEnumerable<EstadoFisicoRecepcion> estados)
    {
        var lista = estados.ToList();
        if (lista.Contains(EstadoFisicoRecepcion.DanioGrave))
        {
            return EstadoFisicoRecepcion.DanioGrave;
        }

        return lista.Contains(EstadoFisicoRecepcion.DanioLeve)
            ? EstadoFisicoRecepcion.DanioLeve
            : EstadoFisicoRecepcion.Optimo;
    }

    private static IQueryable<Alquiler> AplicarEstado(
        IQueryable<Alquiler> consulta,
        string estado,
        DateTime ahora) => estado switch
    {
        "vencidos" => consulta.Where(a =>
            a.Devolucion == null &&
            a.EstadoContrato != EstadoContrato.Cerrado &&
            a.FechaDevolucionPactada < ahora),
        "devueltos" => consulta.Where(a =>
            a.Devolucion != null || a.EstadoContrato == EstadoContrato.Cerrado),
        "todos" => consulta,
        _ => consulta.Where(a =>
            a.Devolucion == null && a.EstadoContrato != EstadoContrato.Cerrado)
    };

    private static string NormalizarEstado(string? estado) =>
        estado is "pendientes" or "vencidos" or "devueltos" or "todos" ? estado : "pendientes";

    private static IQueryable<Alquiler> AplicarBusqueda(IQueryable<Alquiler> consulta, string? termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return consulta;
        }

        var texto = termino.Trim();
        var tokens = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var idContrato = ParsearContrato(texto);

        if (idContrato.HasValue && tokens.Length == 1)
        {
            var id = idContrato.Value;
            return consulta.Where(a =>
                a.IdAlquiler == id ||
                a.Cliente!.Dni.Contains(texto) ||
                a.Cliente.Nombres.Contains(texto) ||
                a.Cliente.Apellidos.Contains(texto) ||
                a.Cliente.Telefono.Contains(texto));
        }

        foreach (var token in tokens)
        {
            var parte = token;
            consulta = consulta.Where(a =>
                a.Cliente!.Dni.Contains(parte) ||
                a.Cliente.Nombres.Contains(parte) ||
                a.Cliente.Apellidos.Contains(parte) ||
                a.Cliente.Telefono.Contains(parte));
        }

        return consulta;
    }

    private static int? ParsearContrato(string valor)
    {
        var texto = valor.Trim().ToUpperInvariant();
        if (texto.StartsWith("LV-", StringComparison.Ordinal))
        {
            texto = texto[3..];
        }

        return int.TryParse(texto, out var id) && id is > 0 and < 1_000_000 ? id : null;
    }
}

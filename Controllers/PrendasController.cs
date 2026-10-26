using System.Text;
using AlquilerVestimentasLucy.Data;
using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Catálogo e inventario de prendas (RF-02 / RF-03).
/// Cada fila del catálogo es una unidad física trazable por su código único.
/// </summary>
[Authorize]
public class PrendasController : Controller
{
    private const int PageSize = 8;

    private static readonly Dictionary<string, (string Descripcion, string Imagen)> InfoCategorias =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tradición peruana"] = ("Marineras, huaynos y polleras", "/img/categorias/tradicion.jpg"),
            ["Trajes y gala"] = ("Sacos, conjuntos y chalecos", "/img/categorias/gala.jpg"),
            ["Personajes"] = ("Disfraces para cada historia", "/img/categorias/personajes.jpg")
        };

    private static readonly string[] OrdenTallas = ["XS", "S", "M", "L", "XL", "XXL", "Única"];

    private readonly ApplicationDbContext _db;
    private readonly ILogger<PrendasController> _logger;

    public PrendasController(ApplicationDbContext db, ILogger<PrendasController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        EstadoPrenda? estado,
        string? categoria,
        string? talla,
        int page = 1)
    {
        var filtro = ConstruirFiltro(q, estado, categoria, talla);

        var totalFiltrado = await filtro.CountAsync();
        var totalPages = totalFiltrado == 0 ? 1 : (int)Math.Ceiling(totalFiltrado / (double)PageSize);
        page = Math.Clamp(page, 1, totalPages);

        var prendas = await filtro
            .OrderBy(p => p.Estado == EstadoPrenda.Alquilada ? 0
                : p.Estado == EstadoPrenda.Disponible ? 1
                : p.Estado == EstadoPrenda.EnMantenimiento ? 2 : 3)
            .ThenBy(p => p.CodigoUnico)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new PrendaFilaViewModel
            {
                IdPrenda = p.IdPrenda,
                CodigoUnico = p.CodigoUnico,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Talla = p.Talla,
                PrecioAlquiler = p.PrecioAlquiler,
                CostoGarantia = p.CostoGarantia,
                Estado = p.Estado
            })
            .ToListAsync();

        var porEstado = await _db.Prendas
            .AsNoTracking()
            .GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        int Contar(EstadoPrenda valor) =>
            porEstado.FirstOrDefault(x => x.Estado == valor)?.Total ?? 0;

        var categorias = await _db.Prendas
            .AsNoTracking()
            .GroupBy(p => p.Categoria)
            .Select(g => new { Nombre = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Nombre)
            .Take(3)
            .ToListAsync();

        var categoriasDisponibles = await _db.Prendas
            .AsNoTracking()
            .Select(p => p.Categoria)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var tallasDb = await _db.Prendas
            .AsNoTracking()
            .Select(p => p.Talla)
            .Distinct()
            .ToListAsync();

        var tallas = tallasDb
            .OrderBy(t =>
            {
                var posicion = Array.IndexOf(OrdenTallas, t);
                return posicion < 0 ? int.MaxValue : posicion;
            })
            .ThenBy(t => t)
            .ToList();

        var modelo = new PrendasIndexViewModel
        {
            Prendas = prendas,
            Categorias = categorias.Select(c =>
            {
                var info = InfoCategoria(c.Nombre);
                return new CategoriaResumenViewModel
                {
                    Nombre = c.Nombre,
                    Descripcion = info.Descripcion,
                    Imagen = info.Imagen,
                    Total = c.Total
                };
            }).ToList(),
            Tallas = tallas,
            CategoriasDisponibles = categoriasDisponibles,
            Q = q,
            Estado = estado,
            Categoria = categoria,
            Talla = talla,
            Page = page,
            TotalPages = totalPages,
            TotalFiltrado = totalFiltrado,
            MostradosDesde = totalFiltrado == 0 ? 0 : (page - 1) * PageSize + 1,
            MostradosHasta = Math.Min(page * PageSize, totalFiltrado),
            TotalTodas = porEstado.Sum(x => x.Total),
            TotalDisponibles = Contar(EstadoPrenda.Disponible),
            TotalAlquiladas = Contar(EstadoPrenda.Alquilada),
            TotalMantenimiento = Contar(EstadoPrenda.EnMantenimiento),
            TotalBaja = Contar(EstadoPrenda.DeBaja)
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var prenda = await _db.Prendas
            .AsNoTracking()
            .Include(p => p.Mantenimientos)
            .FirstOrDefaultAsync(p => p.IdPrenda == id);

        if (prenda is null)
        {
            return NotFound();
        }

        ViewData["ClienteActual"] = await _db.DetallesAlquiler
            .AsNoTracking()
            .Where(d => d.IdPrenda == id && d.Alquiler!.EstadoContrato == EstadoContrato.Activo)
            .Select(d => d.Alquiler!.Cliente!.Nombres + " " + d.Alquiler!.Cliente!.Apellidos)
            .FirstOrDefaultAsync();

        return View(prenda);
    }

    [HttpGet]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public IActionResult Create()
    {
        return View(new PrendaFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> Create(PrendaFormViewModel modelo)
    {
        Normalizar(modelo);
        await ValidarCodigoUnicoAsync(modelo.CodigoUnico, null);

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var prenda = modelo.AEntidad();
        prenda.Estado = EstadoPrenda.Disponible;
        prenda.Activo = true;
        prenda.FechaRegistro = DateTime.Now;

        _db.Prendas.Add(prenda);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Prenda {Codigo} registrada por {Usuario}.", prenda.CodigoUnico, User.Identity?.Name);
        TempData["Exito"] = $"La prenda {prenda.CodigoUnico} se registró correctamente y está disponible para alquiler.";
        return RedirectToAction(nameof(Details), new { id = prenda.IdPrenda });
    }

    [HttpGet]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> Edit(int id)
    {
        var prenda = await _db.Prendas.FindAsync(id);
        return prenda is null ? NotFound() : View(PrendaFormViewModel.DesdeEntidad(prenda));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> Edit(int id, PrendaFormViewModel modelo)
    {
        var actual = await _db.Prendas.FindAsync(id);
        if (actual is null)
        {
            return NotFound();
        }

        Normalizar(modelo);
        await ValidarCodigoUnicoAsync(modelo.CodigoUnico, id);

        if (!ModelState.IsValid)
        {
            modelo.IdPrenda = id;
            modelo.Estado = actual.Estado;
            modelo.FechaRegistro = actual.FechaRegistro;
            return View(modelo);
        }

        actual.CodigoUnico = modelo.CodigoUnico;
        actual.Nombre = modelo.Nombre;
        actual.Categoria = modelo.Categoria;
        actual.Talla = modelo.Talla;
        actual.PrecioAlquiler = modelo.PrecioAlquiler ?? 0m;
        actual.CostoGarantia = modelo.CostoGarantia ?? 0m;

        await _db.SaveChangesAsync();

        TempData["Exito"] = $"La ficha de {actual.CodigoUnico} se actualizó correctamente.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> DarDeBaja(int id)
    {
        var prenda = await _db.Prendas.FindAsync(id);
        if (prenda is null)
        {
            return NotFound();
        }

        if (prenda.Estado == EstadoPrenda.DeBaja)
        {
            TempData["Error"] = "La prenda ya se encuentra de baja.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (prenda.Estado is EstadoPrenda.Alquilada or EstadoPrenda.EnMantenimiento)
        {
            TempData["Error"] = "No se puede dar de baja una prenda alquilada o en mantenimiento. Primero debe regresar al taller.";
            return RedirectToAction(nameof(Details), new { id });
        }

        prenda.Estado = EstadoPrenda.DeBaja;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Prenda {Codigo} dada de baja por {Usuario}.", prenda.CodigoUnico, User.Identity?.Name);
        TempData["Exito"] = $"La prenda {prenda.CodigoUnico} quedó de baja y conserva todo su historial.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbInitializer.RolAdministradora)]
    public async Task<IActionResult> Reactivar(int id)
    {
        var prenda = await _db.Prendas.FindAsync(id);
        if (prenda is null)
        {
            return NotFound();
        }

        if (prenda.Estado != EstadoPrenda.DeBaja)
        {
            TempData["Error"] = "Solo las prendas de baja pueden reactivarse desde esta ficha.";
            return RedirectToAction(nameof(Details), new { id });
        }

        prenda.Estado = EstadoPrenda.Disponible;
        await _db.SaveChangesAsync();

        TempData["Exito"] = $"La prenda {prenda.CodigoUnico} volvió a estar disponible.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Exportar(string? q, EstadoPrenda? estado, string? categoria, string? talla)
    {
        var filas = await ConstruirFiltro(q, estado, categoria, talla)
            .OrderBy(p => p.CodigoUnico)
            .ToListAsync();

        var contenido = new StringBuilder();
        contenido.AppendLine("Codigo unico;Prenda;Categoria;Talla;Alquiler;Garantia;Estado");

        foreach (var prenda in filas)
        {
            contenido.AppendLine(string.Join(';',
                prenda.CodigoUnico,
                Limpiar(prenda.Nombre),
                Limpiar(prenda.Categoria),
                Limpiar(prenda.Talla),
                prenda.PrecioAlquiler.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                prenda.CostoGarantia.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                prenda.Estado.Etiqueta()));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"inventario-lucyvest-{DateTime.Now:yyyyMMdd}.csv");
    }

    private IQueryable<Prenda> ConstruirFiltro(string? q, EstadoPrenda? estado, string? categoria, string? talla)
    {
        var query = _db.Prendas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termino = q.Trim();
            query = query.Where(p =>
                p.CodigoUnico.Contains(termino) ||
                p.Nombre.Contains(termino) ||
                p.Categoria.Contains(termino));
        }

        if (estado.HasValue)
        {
            query = query.Where(p => p.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(p => p.Categoria == categoria);
        }

        if (!string.IsNullOrWhiteSpace(talla))
        {
            query = query.Where(p => p.Talla == talla);
        }

        return query;
    }

    private async Task ValidarCodigoUnicoAsync(string codigo, int? idExcluir)
    {
        var existe = await _db.Prendas.AnyAsync(p =>
            p.CodigoUnico == codigo &&
            (!idExcluir.HasValue || p.IdPrenda != idExcluir.Value));

        if (existe)
        {
            ModelState.AddModelError(nameof(Prenda.CodigoUnico),
                $"Ya existe una prenda registrada con el código {codigo}.");
        }
    }

    private static void Normalizar(PrendaFormViewModel modelo)
    {
        modelo.CodigoUnico = (modelo.CodigoUnico ?? string.Empty).Trim().ToUpperInvariant();
        modelo.Nombre = (modelo.Nombre ?? string.Empty).Trim();
        modelo.Categoria = (modelo.Categoria ?? string.Empty).Trim();
        modelo.Talla = (modelo.Talla ?? string.Empty).Trim();
    }

    private static (string Descripcion, string? Imagen) InfoCategoria(string nombre) =>
        InfoCategorias.TryGetValue(nombre, out var info)
            ? info
            : ("Prendas del taller", null);

    private static string Limpiar(string valor) => valor.Replace(';', ',').Replace('\n', ' ').Trim();
}

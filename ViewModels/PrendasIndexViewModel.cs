using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.ViewModels;

public class PrendaFilaViewModel
{
    public int IdPrenda { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public decimal PrecioAlquiler { get; set; }
    public decimal CostoGarantia { get; set; }
    public EstadoPrenda Estado { get; set; }
}

public class CategoriaResumenViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Imagen { get; set; }
    public int Total { get; set; }
}

public class PrendasIndexViewModel
{
    public List<PrendaFilaViewModel> Prendas { get; set; } = new();
    public List<CategoriaResumenViewModel> Categorias { get; set; } = new();
    public List<string> Tallas { get; set; } = new();
    public List<string> CategoriasDisponibles { get; set; } = new();

    public string? Q { get; set; }
    public EstadoPrenda? Estado { get; set; }
    public string? Categoria { get; set; }
    public string? Talla { get; set; }

    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalFiltrado { get; set; }
    public int MostradosDesde { get; set; }
    public int MostradosHasta { get; set; }

    public int TotalTodas { get; set; }
    public int TotalDisponibles { get; set; }
    public int TotalAlquiladas { get; set; }
    public int TotalMantenimiento { get; set; }
    public int TotalBaja { get; set; }

    public int TotalUnidades => TotalTodas;
}

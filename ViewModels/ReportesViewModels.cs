using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.ViewModels;

public class ReportesIndexViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public bool RangoInvalido { get; set; }

    public decimal IngresosAlquiler { get; set; }
    public decimal GarantiasCustodia { get; set; }
    public decimal Penalidades { get; set; }
    public decimal Reembolsos { get; set; }
    public decimal CostosMantenimiento { get; set; }
    public decimal BalanceOperativo => IngresosAlquiler + Penalidades - Reembolsos - CostosMantenimiento;

    public int Contratos { get; set; }
    public int Devoluciones { get; set; }
    public int ContratosAbiertos { get; set; }
    public int ContratosVencidos { get; set; }

    public int PrendasDisponibles { get; set; }
    public int PrendasAlquiladas { get; set; }
    public int PrendasEnMantenimiento { get; set; }
    public int PrendasDeBaja { get; set; }
    public int TotalPrendas { get; set; }
    public decimal PorcentajeDisponible => TotalPrendas == 0 ? 0 : PrendasDisponibles * 100m / TotalPrendas;
    public decimal PorcentajeAlquilada => TotalPrendas == 0 ? 0 : PrendasAlquiladas * 100m / TotalPrendas;
    public decimal PorcentajeMantenimiento => TotalPrendas == 0 ? 0 : PrendasEnMantenimiento * 100m / TotalPrendas;
    public decimal PorcentajeBaja => TotalPrendas == 0 ? 0 : PrendasDeBaja * 100m / TotalPrendas;

    public List<ReporteSerieViewModel> Serie { get; set; } = new();
    public List<ReporteClienteViewModel> TopClientes { get; set; } = new();
    public List<ReporteActividadViewModel> Actividad { get; set; } = new();
}

public class ReporteSerieViewModel
{
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Ingresos { get; set; }
    public int Contratos { get; set; }
    public decimal Altura { get; set; }
}

public class ReporteClienteViewModel
{
    public int IdCliente { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public int Contratos { get; set; }
    public decimal Ingresos { get; set; }
    public decimal Altura { get; set; }
}

public class ReporteActividadViewModel
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string PillClass { get; set; } = "pill-neutral";
    public string Controller { get; set; } = "Home";
    public string Action { get; set; } = "Index";
    public int? RouteId { get; set; }
}

public class ReportesIngresosViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public bool RangoInvalido { get; set; }
    public decimal TotalAlquiler { get; set; }
    public decimal TotalGarantia { get; set; }
    public decimal TotalRecibido { get; set; }
    public decimal TotalPenalidades { get; set; }
    public decimal TotalReembolsos { get; set; }
    public List<ReporteIngresoFilaViewModel> Filas { get; set; } = new();
}

public class ReporteIngresoFilaViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Garantia { get; set; }
    public decimal Total { get; set; }
    public decimal Penalidad { get; set; }
    public decimal Reembolso { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = "pill-neutral";
    public bool TieneDevolucion { get; set; }
}

public class ReportesStockViewModel
{
    public string? Q { get; set; }
    public EstadoPrenda? Estado { get; set; }
    public string? Categoria { get; set; }
    public int Total { get; set; }
    public int Disponibles { get; set; }
    public int Alquiladas { get; set; }
    public int EnMantenimiento { get; set; }
    public int DeBaja { get; set; }
    public List<string> Categorias { get; set; } = new();
    public List<ReporteStockFilaViewModel> Filas { get; set; } = new();
}

public class ReporteStockFilaViewModel
{
    public int IdPrenda { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public EstadoPrenda Estado { get; set; }
    public decimal PrecioAlquiler { get; set; }
    public decimal CostoGarantia { get; set; }
}

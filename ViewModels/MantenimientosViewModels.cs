using System.ComponentModel.DataAnnotations;
using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.ViewModels;

public class MantenimientosIndexViewModel
{
    public string? Q { get; set; }
    public string EstadoTab { get; set; } = "encurso";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalFiltrado { get; set; }

    public int ConteoEnCurso { get; set; }
    public int ConteoVencidos { get; set; }
    public int ConteoFinalizados { get; set; }
    public int ConteoTodos { get; set; }

    public int KpiEnTaller { get; set; }
    public int KpiVencidos { get; set; }
    public int KpiFinalizadosMes { get; set; }
    public decimal KpiCostoMes { get; set; }
    public int PrendasEnTaller { get; set; }
    public DateTime FechaCorte { get; set; } = DateTime.Now;

    public MantenimientoFilaViewModel? Alerta { get; set; }
    public List<MantenimientoFilaViewModel> Filas { get; set; } = new();
}

public class MantenimientoFilaViewModel
{
    public int IdMantenimiento { get; set; }
    public int IdPrenda { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public TipoServicio TipoServicio { get; set; }
    public string TipoServicioTexto => TipoServicio.Etiqueta();
    public DateTime FechaIngreso { get; set; }
    public DateTime FechaSalidaEstimada { get; set; }
    public DateTime? FechaSalidaReal { get; set; }
    public decimal CostoServicio { get; set; }
    public string? Observaciones { get; set; }
    public int DiasEnTaller { get; set; }
    public string DiasEnTallerTexto => DiasEnTaller == 0
        ? "menos de 1 día"
        : $"{DiasEnTaller} {(DiasEnTaller == 1 ? "día" : "días")}";
    public bool EnCurso { get; set; }
    public bool Vencido { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
}

public class MantenimientoFormViewModel
{
    public int IdMantenimiento { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una prenda.")]
    [Display(Name = "Prenda")]
    public int IdPrenda { get; set; }

    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Categoria { get; set; }
    public string? Talla { get; set; }
    public EstadoPrenda EstadoPrenda { get; set; }

    [Display(Name = "Tipo de servicio")]
    public TipoServicio TipoServicio { get; set; } = TipoServicio.Lavanderia;

    [Required(ErrorMessage = "Indica la fecha de ingreso.")]
    [Display(Name = "Fecha de ingreso")]
    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-ddTHH:mm}")]
    public DateTime FechaIngreso { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Indica la fecha estimada de salida.")]
    [Display(Name = "Salida estimada")]
    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-ddTHH:mm}")]
    public DateTime FechaSalidaEstimada { get; set; } = DateTime.Today.AddDays(2);

    [Range(0, 99999, ErrorMessage = "El costo no puede ser negativo.")]
    [Display(Name = "Costo del servicio")]
    public decimal CostoServicio { get; set; }

    [StringLength(300)]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    public List<PrendaMantenimientoOpcionViewModel> Prendas { get; set; } = new();

    public bool EsEdicion => IdMantenimiento > 0;
}

public class PrendaMantenimientoOpcionViewModel
{
    public int IdPrenda { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public EstadoPrenda Estado { get; set; }
    public bool TieneOrdenActiva { get; set; }
    public string EstadoTexto => TieneOrdenActiva
        ? "Con orden activa"
        : Estado.Etiqueta();
}

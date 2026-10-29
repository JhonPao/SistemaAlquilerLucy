using System.ComponentModel.DataAnnotations;
using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.ViewModels;

public class DevolucionesIndexViewModel
{
    public string? Q { get; set; }
    public string EstadoTab { get; set; } = "pendientes";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalFiltrado { get; set; }

    public int ConteoPendientes { get; set; }
    public int ConteoVencidos { get; set; }
    public int ConteoDevueltos { get; set; }
    public int ConteoTodos { get; set; }

    public int KpiPorRecibir { get; set; }
    public int KpiVencidos { get; set; }
    public int KpiDevueltosHoy { get; set; }
    public int PrendasPorRecibir { get; set; }
    public decimal GarantiasEnCustodia { get; set; }
    public decimal MoraEstimada { get; set; }
    public decimal TarifaMora { get; set; }
    public DateTime FechaCorte { get; set; } = DateTime.Now;

    public AlertaDevolucionViewModel? Alerta { get; set; }
    public List<DevolucionFilaViewModel> Filas { get; set; } = new();
}

public class AlertaDevolucionViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public int CantidadPrendas { get; set; }
    public DateTime FechaRetorno { get; set; }
    public DateTime FechaCorte { get; set; }
    public int DiasMora { get; set; }
    public decimal MoraEstimada { get; set; }
}

public class DevolucionFilaViewModel
{
    public int IdAlquiler { get; set; }
    public int? IdDevolucion { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public List<string> Prendas { get; set; } = new();
    public int CantidadPrendas { get; set; }
    public DateTime FechaPactada { get; set; }
    public DateTime? FechaReal { get; set; }
    public decimal Garantia { get; set; }
    public decimal MoraEstimada { get; set; }
    public decimal TotalPenalidad { get; set; }
    public int DiasRetraso { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
    public bool Devuelto { get; set; }
}

public class RegistroDevolucionViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public DateTime FechaAlquiler { get; set; }
    public DateTime FechaPactada { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalGarantia { get; set; }
    public int CantidadPrendas { get; set; }
    public string? ObservacionesEntrega { get; set; }

    [Display(Name = "Retorno real")]
    [Required(ErrorMessage = "Indica la fecha y hora reales de retorno.")]
    public DateTime FechaDevolucionReal { get; set; } = DateTime.Now;

    [Display(Name = "Medio de reembolso")]
    [Required(ErrorMessage = "Selecciona el medio de reembolso.")]
    [StringLength(30)]
    public string MedioReembolso { get; set; } = "Transferencia";

    [Display(Name = "Cliente informado del desglose")]
    public bool ClienteInformado { get; set; }

    [Display(Name = "Observaciones generales")]
    [StringLength(500)]
    public string? Observaciones { get; set; }

    public List<InspeccionPrendaViewModel> Prendas { get; set; } = new();

    // Resumen calculado por el servidor para la vista y la validación.
    public int DiasRetraso { get; set; }
    public double DiferenciaHoras { get; set; }
    public decimal TarifaMora { get; set; }
    public decimal MontoMora { get; set; }
    public decimal CostoBaseReparacion { get; set; }
    public decimal PorcentajeDanioLeve { get; set; }
    public decimal PorcentajeDanioGrave { get; set; }
    public decimal TotalCargos { get; set; }
    public decimal TotalPenalidad { get; set; }
    public decimal Reembolso { get; set; }
    public decimal SaldoPendiente { get; set; }
    public bool MoraActiva => DiasRetraso > 0;
}

public class InspeccionPrendaViewModel
{
    public int IdDetalle { get; set; }
    public int IdPrenda { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public decimal PrecioAlquiler { get; set; }
    public decimal GarantiaUnitaria { get; set; }

    [Display(Name = "Condición")]
    public EstadoFisicoRecepcion EstadoFisicoRecepcion { get; set; } = EstadoFisicoRecepcion.Optimo;

    public decimal CargoDanio { get; set; }

    [Display(Name = "Observaciones de la prenda")]
    [StringLength(300)]
    public string? Observaciones { get; set; }

    public bool RequiereMantenimiento =>
        EstadoFisicoRecepcion is EstadoFisicoRecepcion.DanioLeve or EstadoFisicoRecepcion.DanioGrave;
}

public class ConstanciaDevolucionViewModel
{
    public int IdDevolucion { get; set; }
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public DateTime FechaPactada { get; set; }
    public DateTime FechaReal { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? RecibidoPor { get; set; }
    public string? Observaciones { get; set; }
    public bool ClienteInformado { get; set; }
    public decimal Garantia { get; set; }
    public decimal MontoMora { get; set; }
    public decimal TotalCargos { get; set; }
    public decimal TotalPenalidad { get; set; }
    public decimal Reembolso { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string? MedioReembolso { get; set; }
    public int DiasRetraso { get; set; }
    public List<ConstanciaItemViewModel> Prendas { get; set; } = new();
}

public class ConstanciaItemViewModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public EstadoFisicoRecepcion EstadoFisicoRecepcion { get; set; }
    public decimal CargoDanio { get; set; }
    public string? Observaciones { get; set; }
    public string Destino => EstadoFisicoRecepcion == EstadoFisicoRecepcion.Optimo
        ? "Disponible"
        : "En mantenimiento";
}

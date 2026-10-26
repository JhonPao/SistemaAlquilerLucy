using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.ViewModels;

public class ClienteSeleccionadoViewModel
{
    public int IdCliente { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public bool ConsentimientoDatos { get; set; }
    public DateTime FechaRegistro { get; set; }
    public bool Activo { get; set; }
    public int MoraActiva { get; set; }

    public bool AlDia => Activo && MoraActiva == 0;
    public string Iniciales { get; set; } = string.Empty;
}

public class PrendaAlquilerViewModel
{
    public int IdPrenda { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public decimal PrecioAlquiler { get; set; }
    public decimal CostoGarantia { get; set; }
    public bool Seleccionada { get; set; }
}

public class PrendaNoDisponibleViewModel
{
    public string CodigoUnico { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public class NuevoAlquilerViewModel
{
    public static readonly string[] MediosPago =
        ["Transferencia", "Efectivo", "Yape/Plin", "Tarjeta"];

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente para el alquiler.")]
    [Display(Name = "Cliente")]
    public int IdCliente { get; set; }

    public ClienteSeleccionadoViewModel? Cliente { get; set; }

    [Required(ErrorMessage = "La fecha de salida es obligatoria.")]
    [Display(Name = "Salida")]
    public DateTime FechaAlquiler { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "La fecha de retorno pactada es obligatoria.")]
    [Display(Name = "Retorno pactado")]
    public DateTime FechaDevolucionPactada { get; set; } = DateTime.Now.AddDays(2);

    public List<PrendaAlquilerViewModel> Prendas { get; set; } = new();
    public List<PrendaNoDisponibleViewModel> NoDisponibles { get; set; } = new();
    public int[] PrendasSeleccionadas { get; set; } = [];

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Condición y accesorios")]
    public string? Observaciones { get; set; }

    [Required(ErrorMessage = "Selecciona un medio de pago.")]
    [StringLength(30)]
    [Display(Name = "Medio de pago")]
    public string MedioPago { get; set; } = "Transferencia";

    [StringLength(50, ErrorMessage = "La referencia no puede superar los 50 caracteres.")]
    [Display(Name = "Referencia de pago")]
    public string? ReferenciaPago { get; set; }

    [Range(typeof(bool), "true", "true",
        ErrorMessage = "Confirma que verificaste las prendas, la identidad del cliente y los importes.")]
    [Display(Name = "Verificación de entrega")]
    public bool AceptaCondiciones { get; set; }

    public List<PrendaAlquilerViewModel> PrendasElegidas =>
        Prendas.Where(p => p.Seleccionada).OrderBy(p => p.CodigoUnico).ToList();

    public decimal Subtotal => PrendasElegidas.Sum(p => p.PrecioAlquiler);
    public decimal TotalGarantia => PrendasElegidas.Sum(p => p.CostoGarantia);
    public decimal MontoTotal => Subtotal + TotalGarantia;
}

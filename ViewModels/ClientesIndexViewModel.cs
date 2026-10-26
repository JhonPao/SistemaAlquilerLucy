using System.ComponentModel.DataAnnotations;

namespace AlquilerVestimentasLucy.ViewModels;

public class ClienteResumenViewModel
{
    public int IdCliente { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Iniciales { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string EstadoResumen { get; set; } = string.Empty;
    public string ClaseResumen { get; set; } = string.Empty;
}

public class ClienteAlquilerViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Prendas { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public DateTime FechaRetorno { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
    public string? Puntualidad { get; set; }
}

public class ClienteDetalleViewModel
{
    public int IdCliente { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public bool Activo { get; set; }
    public bool ConsentimientoDatos { get; set; }

    public int ContratosTotal { get; set; }
    public int DevolucionesPuntuales { get; set; }
    public int DevolucionesTotal { get; set; }
    public int MoraActiva { get; set; }

    public decimal MoraEstimada { get; set; }
    public int DiasMora { get; set; }
    public decimal TarifaMora { get; set; }
    public DateTime FechaCorte { get; set; }
    public string NotaPuntualidad { get; set; } = string.Empty;

    public List<ClienteAlquilerViewModel> Historial { get; set; } = new();
}

public class ClientesIndexViewModel
{
    public string? Q { get; set; }
    public int TotalClientes { get; set; }
    public List<ClienteResumenViewModel> Clientes { get; set; } = new();
    public ClienteDetalleViewModel? Detalle { get; set; }
}

public class ClienteFormViewModel
{
    public int IdCliente { get; set; }

    [Required(ErrorMessage = "El DNI es obligatorio.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener exactamente 8 dígitos.")]
    [Display(Name = "DNI")]
    public string Dni { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100)]
    [Display(Name = "Nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100)]
    [Display(Name = "Apellidos")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20)]
    [RegularExpression(@"^(?=(?:\D*\d){6,15}\D*$)[\d\s()+-]+$",
        ErrorMessage = "El teléfono debe contener entre 6 y 15 dígitos.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [Range(typeof(bool), "true", "true",
        ErrorMessage = "El cliente debe autorizar el tratamiento de sus datos personales (Ley N° 29733).")]
    [Display(Name = "Consentimiento de datos (Ley N° 29733)")]
    public bool ConsentimientoDatos { get; set; }

    /// <summary>Estado actual del cliente. Solo se usa para la ficha de edición.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Indica si la baja está bloqueada por contratos vigentes.</summary>
    public bool TieneContratosActivos { get; set; }

    public Models.Cliente AEntidad()
    {
        return new Models.Cliente
        {
            IdCliente = IdCliente,
            Dni = Dni,
            Nombres = Nombres,
            Apellidos = Apellidos,
            Telefono = Telefono,
            Direccion = Direccion,
            ConsentimientoDatos = ConsentimientoDatos
        };
    }

    public static ClienteFormViewModel DesdeEntidad(Models.Cliente cliente)
    {
        return new ClienteFormViewModel
        {
            IdCliente = cliente.IdCliente,
            Dni = cliente.Dni,
            Nombres = cliente.Nombres,
            Apellidos = cliente.Apellidos,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            ConsentimientoDatos = cliente.ConsentimientoDatos
        };
    }
}

namespace AlquilerVestimentasLucy.ViewModels;

public class ContratoPrendaViewModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Garantia { get; set; }
}

public class ContratoDigitalViewModel
{
    public int IdAlquiler { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaEntrega { get; set; }
    public DateTime FechaRetorno { get; set; }
    public DateTime? FechaDevolucionReal { get; set; }
    public DateTime FechaAceptacion { get; set; }

    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDni { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string? ClienteDireccion { get; set; }

    public List<ContratoPrendaViewModel> Prendas { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal TotalGarantia { get; set; }
    public decimal MontoTotal { get; set; }

    public string? Observaciones { get; set; }
    public string? MedioPago { get; set; }
    public string? ReferenciaPago { get; set; }
    public string? Vendedor { get; set; }

    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
    public string NotaEstado { get; set; } = string.Empty;
    public bool Devuelto { get; set; }
    public bool Vencido { get; set; }
    public bool Consentimiento { get; set; }
    public bool AceptacionRegistrada { get; set; } = true;

    public decimal TarifaMora { get; set; }
    public decimal CostoBaseReparacion { get; set; }
    public decimal PorcentajeDanioLeve { get; set; }
    public decimal PorcentajeDanioGrave { get; set; }

    public string EmisorNombre { get; set; } = "Taller LucyVest";
    public string EmisorRuc { get; set; } = "20612345678";
    public string EmisorDireccion { get; set; } = "Av. Sucre 420, Pueblo Libre, Lima";
    public string EmisorRepresentante { get; set; } = "Lucía Torres";

    public string? TelefonoDestino { get; set; }
    public string? DevolucionObservaciones { get; set; }

    public int CantidadPrendas => Prendas.Count;
    public string CondicionEntrega => string.IsNullOrWhiteSpace(Observaciones)
        ? "Prendas revisadas y entregadas en buen estado."
        : Observaciones;
}

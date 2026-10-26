namespace AlquilerVestimentasLucy.ViewModels;

public class AlquilerFilaViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public List<string> Prendas { get; set; } = new();
    public DateTime FechaSalida { get; set; }
    public DateTime FechaRetorno { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Garantia { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
    public bool Vencido { get; set; }
    public bool Cerrado { get; set; }

    public int CantidadPrendas => Prendas.Count;
}

public class AlquilerPrendaViewModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Talla { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Garantia { get; set; }
}

public class AlquilerSeleccionViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public List<AlquilerPrendaViewModel> Prendas { get; set; } = new();
    public DateTime FechaSalida { get; set; }
    public DateTime FechaRetorno { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Garantia { get; set; }
    public decimal MontoTotal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string PillClass { get; set; } = string.Empty;
    public bool Vencido { get; set; }
    public bool Cerrado { get; set; }
    public string? Observaciones { get; set; }
    public string? MedioPago { get; set; }
    public string? ReferenciaPago { get; set; }

    public int CantidadPrendas => Prendas.Count;
    public string PrendasResumen => Prendas.Count == 0
        ? "Sin detalle de prendas"
        : string.Join(" · ", Prendas.Select(p => $"{p.Codigo} {p.Nombre}"));
}

public class AlquilerAlertaViewModel
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

public class AlquileresIndexViewModel
{
    public string? Q { get; set; }
    public string EstadoTab { get; set; } = "todos";
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }

    public int KpiVigentes { get; set; }
    public int KpiPendientes { get; set; }
    public int KpiVencidos { get; set; }
    public int KpiDevueltos { get; set; }
    public int PrendasPorRetornar { get; set; }
    public decimal MoraEstimada { get; set; }
    public decimal TarifaMora { get; set; }
    public AlquilerAlertaViewModel? Alerta { get; set; }

    public int ConteoTodos { get; set; }
    public int ConteoVigentes { get; set; }
    public int ConteoPendientes { get; set; }
    public int ConteoVencidos { get; set; }
    public int ConteoDevueltos { get; set; }

    public List<AlquilerFilaViewModel> Filas { get; set; } = new();
    public AlquilerSeleccionViewModel? Seleccionado { get; set; }

    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalFiltrado { get; set; }
}

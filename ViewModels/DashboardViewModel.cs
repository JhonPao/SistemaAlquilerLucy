namespace AlquilerVestimentasLucy.ViewModels;

public class ProximaDevolucionViewModel
{
    public int IdAlquiler { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public DateTime FechaPactada { get; set; }
    public int CantidadPrendas { get; set; }
    public bool Vencido { get; set; }
}

public class ActividadViewModel
{
    public string Hora { get; set; } = string.Empty;
    public string Movimiento { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public string Nombre { get; set; } = string.Empty;

    public int ContratosVigentes { get; set; }
    public int DevolucionesHoy { get; set; }
    public int VencidosConMora { get; set; }
    public int PrendasDisponibles { get; set; }
    public int TotalPrendas { get; set; }

    public int Alquiladas { get; set; }
    public int EnMantenimiento { get; set; }
    public int DeBaja { get; set; }

    public decimal TarifaMoraDiaria { get; set; }

    public List<ProximaDevolucionViewModel> ProximasDevoluciones { get; set; } = new();
    public List<ActividadViewModel> ActividadReciente { get; set; } = new();

    public int PrendasFueraDelTaller => Alquiladas;
    public decimal PorcentajeDisponible => TotalPrendas == 0 ? 0 : PrendasDisponibles * 100m / TotalPrendas;
    public decimal PorcentajeAlquilada => TotalPrendas == 0 ? 0 : Alquiladas * 100m / TotalPrendas;
    public decimal PorcentajeMantenimiento => TotalPrendas == 0 ? 0 : EnMantenimiento * 100m / TotalPrendas;
    public decimal PorcentajeBaja => TotalPrendas == 0 ? 0 : DeBaja * 100m / TotalPrendas;
}

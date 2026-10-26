using AlquilerVestimentasLucy.Models;

namespace AlquilerVestimentasLucy.Services;

public record ResultadoLiquidacion(
    int DiasRetraso,
    decimal TarifaMoraAplicada,
    decimal MontoMora,
    decimal CostoBaseAplicado,
    decimal MontoDanio,
    decimal TotalPenalidad);

/// <summary>
/// Reglas de negocio RNC01 (mora por retraso) y RNC02 (penalidad por deterioro).
/// Vive en la capa de negocio para que las vistas y controladores nunca
/// calculen montos por su cuenta.
/// </summary>
public interface IReglasNegocioService
{
    ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica);
}

public class ReglasNegocioService : IReglasNegocioService
{
    public ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        // RNC01: penalidad por mora diaria
        var diasRetraso = 0;
        if (fechaReal.Date > fechaPactada.Date)
        {
            diasRetraso = (fechaReal.Date - fechaPactada.Date).Days;
        }
        var montoMora = diasRetraso * politica.TarifaMoraDiaria;

        // RNC02: penalidad por deterioro físico
        var montoDanio = estadoFisico switch
        {
            EstadoFisicoRecepcion.DanioLeve => politica.CostoBaseReparacion * politica.PorcentajeDanioLeve,
            EstadoFisicoRecepcion.DanioGrave => politica.CostoBaseReparacion * politica.PorcentajeDanioGrave,
            _ => 0m
        };

        return new ResultadoLiquidacion(
            DiasRetraso: diasRetraso,
            TarifaMoraAplicada: politica.TarifaMoraDiaria,
            MontoMora: montoMora,
            CostoBaseAplicado: politica.CostoBaseReparacion,
            MontoDanio: montoDanio,
            TotalPenalidad: montoMora + montoDanio);
    }
}

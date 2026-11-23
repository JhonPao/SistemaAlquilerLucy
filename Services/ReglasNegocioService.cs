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
    int CalcularDiasRetraso(DateTime fechaPactada, DateTime fechaReal);

    decimal CalcularCargoDanio(
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica);

    ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica);

    ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        IEnumerable<EstadoFisicoRecepcion> estadosFisicos,
        ConfiguracionPolitica politica);
}

public class ReglasNegocioService : IReglasNegocioService
{
    public int CalcularDiasRetraso(DateTime fechaPactada, DateTime fechaReal)
    {
        if (fechaReal <= fechaPactada)
        {
            return 0;
        }

        // RNC01: cada bloque de 24 horas iniciado desde la hora pactada cuenta
        // como un día de mora, aunque el retorno ocurra minutos después del vencimiento.
        var horasTranscurridas = (fechaReal - fechaPactada).TotalHours;
        return (int)Math.Ceiling(horasTranscurridas / 24d);
    }

    public decimal CalcularCargoDanio(
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        var montoCargo = estadoFisico switch
        {
            EstadoFisicoRecepcion.DanioLeve =>
                politica.CostoBaseReparacion * politica.PorcentajeDanioLeve,
            EstadoFisicoRecepcion.DanioGrave =>
                politica.CostoBaseReparacion * politica.PorcentajeDanioGrave,
            _ => 0m
        };

        return Math.Round(montoCargo, 2, MidpointRounding.AwayFromZero);
    }

    public ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        EstadoFisicoRecepcion estadoFisico,
        ConfiguracionPolitica politica) =>
        CalcularLiquidacion(fechaPactada, fechaReal, [estadoFisico], politica);

    public ResultadoLiquidacion CalcularLiquidacion(
        DateTime fechaPactada,
        DateTime fechaReal,
        IEnumerable<EstadoFisicoRecepcion> estadosFisicos,
        ConfiguracionPolitica politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(estadosFisicos);

        // RNC01: penalidad por mora diaria.
        var diasDeRetraso = CalcularDiasRetraso(fechaPactada, fechaReal);
        var montoPorMora = diasDeRetraso * politica.TarifaMoraDiaria;

        // RNC02: penalidad por deterioro físico, calculada por cada prenda.
        var montoPorDanio = estadosFisicos.Sum(estadoRecepcion =>
            CalcularCargoDanio(estadoRecepcion, politica));

        return new ResultadoLiquidacion(
            DiasRetraso: diasDeRetraso,
            TarifaMoraAplicada: politica.TarifaMoraDiaria,
            MontoMora: montoPorMora,
            CostoBaseAplicado: politica.CostoBaseReparacion,
            MontoDanio: montoPorDanio,
            TotalPenalidad: montoPorMora + montoPorDanio);
    }
}

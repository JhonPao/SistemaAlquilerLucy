namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Ciclo de vida de una prenda dentro del inventario (máquina de estados).
/// </summary>
public enum EstadoPrenda
{
    Disponible = 1,
    Alquilada = 2,
    EnMantenimiento = 3,
    DeBaja = 4
}

/// <summary>
/// Estado del contrato de alquiler.
/// </summary>
public enum EstadoContrato
{
    Activo = 1,
    Cerrado = 2,
    Vencido = 3
}

/// <summary>
/// Resultado de la inspección física al recibir una prenda devuelta.
/// </summary>
public enum EstadoFisicoRecepcion
{
    Optimo = 1,
    DanioLeve = 2,
    DanioGrave = 3
}

/// <summary>
/// Tipo de servicio aplicado en el módulo de mantenimiento.
/// </summary>
public enum TipoServicio
{
    Lavanderia = 1,
    Costura = 2,
    Desinfeccion = 3
}

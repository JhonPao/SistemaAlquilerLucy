namespace AlquilerVestimentasLucy.Models;

/// <summary>
/// Etiquetas legibles y clases visuales de los enums del dominio.
/// Centralizarlas evita cadenas duplicadas en las vistas.
/// </summary>
public static class EnumExtensions
{
    public static string Etiqueta(this EstadoPrenda estado) => estado switch
    {
        EstadoPrenda.Disponible => "Disponible",
        EstadoPrenda.Alquilada => "Alquilada",
        EstadoPrenda.EnMantenimiento => "En Mantenimiento",
        EstadoPrenda.DeBaja => "De Baja",
        _ => "Desconocido"
    };

    public static string ClasePill(this EstadoPrenda estado) => estado switch
    {
        EstadoPrenda.Disponible => "pill-completado",
        EstadoPrenda.Alquilada => "pill-vigente",
        EstadoPrenda.EnMantenimiento => "pill-atencion",
        EstadoPrenda.DeBaja => "pill-neutral",
        _ => "pill-neutral"
    };

    public static string Etiqueta(this TipoServicio tipo) => tipo switch
    {
        TipoServicio.Lavanderia => "Lavandería",
        TipoServicio.Costura => "Costura",
        TipoServicio.Desinfeccion => "Desinfección",
        _ => "Servicio"
    };
}

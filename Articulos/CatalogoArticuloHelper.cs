using System;
using System.Collections.Generic;
using System.Linq;

namespace AlquilerVestimentasLucy.Articulos
{
    public class ArticuloResumen
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal PrecioReferencial { get; set; }
        public bool Disponible { get; set; }
    }

    public class CatalogoArticuloHelper
    {
        public List<ArticuloResumen> ObtenerArticulosDestacados()
        {
            return new List<ArticuloResumen>
            {
                new ArticuloResumen { Codigo = "ART-001", Nombre = "Terno Ejecutivo Negro", Categoria = "Gala", PrecioReferencial = 85.00m, Disponible = true },
                new ArticuloResumen { Codigo = "ART-002", Nombre = "Vestido de Fiesta Largo", Categoria = "Gala", PrecioReferencial = 120.00m, Disponible = true },
                new ArticuloResumen { Codigo = "ART-003", Nombre = "Traje Típico Marinera", Categoria = "Folklore", PrecioReferencial = 95.00m, Disponible = true }
            };
        }

        public List<ArticuloResumen> FiltrarPorCategoria(List<ArticuloResumen> lista, string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return lista;

            return lista
                .Where(a => a.Categoria.Equals(categoria, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
using AlquilerVestimentasLucy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Data;

/// <summary>
/// Sembrado inicial de roles, usuario administrador y tarifas por defecto.
/// </summary>
public static class DbInitializer
{
    public const string RolAdministradora = "Administradora";
    public const string RolVendedor = "Vendedor";
    public const string RolAsistente = "Asistente";

    public const string AdminEmail = "admin@lucy.local";
    public const string AdminPassword = "Lucy2026*";

    public static readonly string[] Roles = [RolAdministradora, RolVendedor, RolAsistente];

    public static async Task SeedAsync(IServiceProvider services, bool incluirDemo = false)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.MigrateAsync();

        foreach (var rol in Roles)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new IdentityRole(rol));
            }
        }

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                NombreCompleto = "Lucía Torres",
                Activo = true
            };

            var resultado = await userManager.CreateAsync(admin, AdminPassword);
            if (resultado.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, RolAdministradora);
            }
            else
            {
                throw new InvalidOperationException(
                    "No se pudo crear el usuario administrador: " +
                    string.Join("; ", resultado.Errors.Select(e => e.Description)));
            }
        }

        if (!await context.ConfiguracionesPolitica.AnyAsync())
        {
            context.ConfiguracionesPolitica.Add(new ConfiguracionPolitica
            {
                TarifaMoraDiaria = 15.00m,
                PorcentajeDanioLeve = 0.50m,
                PorcentajeDanioGrave = 1.00m,
                CostoBaseReparacion = 40.00m,
                FechaActualizacion = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        if (incluirDemo)
        {
            await SeedDemoAsync(context, admin.Id);
        }
    }

    /// <summary>
    /// Datos de demostración del taller: catálogo, clientes, contratos activos
    /// y un contrato histórico ya devuelto. Es idempotente: completa lo que falte
    /// sin duplicar registros ni borrar datos creados desde la aplicación.
    /// </summary>
    private static async Task SeedDemoAsync(ApplicationDbContext context, string userId)
    {
        if (!await context.Prendas.AnyAsync())
        {
            var prendas = new List<Prenda>
            {
                new() { CodigoUnico = "MN001", Nombre = "Vestido marinera marfil", Categoria = "Tradición peruana", Talla = "M", PrecioAlquiler = 120.00m, CostoGarantia = 150.00m, Estado = EstadoPrenda.Alquilada },
                new() { CodigoUnico = "MN002", Nombre = "Falda marinera ciruela", Categoria = "Tradición peruana", Talla = "M", PrecioAlquiler = 100.00m, CostoGarantia = 150.00m, Estado = EstadoPrenda.Alquilada },
                new() { CodigoUnico = "POL001", Nombre = "Pollera cusqueña", Categoria = "Tradición peruana", Talla = "S", PrecioAlquiler = 90.00m, CostoGarantia = 120.00m, Estado = EstadoPrenda.EnMantenimiento },
                new() { CodigoUnico = "MN003", Nombre = "Blusa bordada cusqueña", Categoria = "Tradición peruana", Talla = "S", PrecioAlquiler = 80.00m, CostoGarantia = 120.00m },
                new() { CodigoUnico = "MN004", Nombre = "Montera bordada", Categoria = "Tradición peruana", Talla = "Única", PrecioAlquiler = 40.00m, CostoGarantia = 60.00m },
                new() { CodigoUnico = "MN005", Nombre = "Faja tejida andina", Categoria = "Tradición peruana", Talla = "Única", PrecioAlquiler = 35.00m, CostoGarantia = 50.00m },

                new() { CodigoUnico = "TRJ001", Nombre = "Terno azul noche", Categoria = "Trajes y gala", Talla = "L", PrecioAlquiler = 140.00m, CostoGarantia = 200.00m, Estado = EstadoPrenda.Alquilada },
                new() { CodigoUnico = "TRJ002", Nombre = "Terno carbón", Categoria = "Trajes y gala", Talla = "M", PrecioAlquiler = 150.00m, CostoGarantia = 200.00m },
                new() { CodigoUnico = "CHA001", Nombre = "Chaleco marfil", Categoria = "Trajes y gala", Talla = "M", PrecioAlquiler = 50.00m, CostoGarantia = 80.00m },
                new() { CodigoUnico = "TRJ003", Nombre = "Saco de gala negro", Categoria = "Trajes y gala", Talla = "L", PrecioAlquiler = 110.00m, CostoGarantia = 160.00m },
                new() { CodigoUnico = "TRJ004", Nombre = "Conjunto chaleco y corbata", Categoria = "Trajes y gala", Talla = "S", PrecioAlquiler = 85.00m, CostoGarantia = 120.00m },

                new() { CodigoUnico = "DIS001", Nombre = "Disfraz de pirata", Categoria = "Personajes", Talla = "M", PrecioAlquiler = 120.00m, CostoGarantia = 150.00m, Estado = EstadoPrenda.Alquilada },
                new() { CodigoUnico = "DIS003", Nombre = "Capa de mago", Categoria = "Personajes", Talla = "L", PrecioAlquiler = 70.00m, CostoGarantia = 90.00m, Estado = EstadoPrenda.DeBaja },
                new() { CodigoUnico = "DIS005", Nombre = "Disfraz de superhéroe", Categoria = "Personajes", Talla = "S", PrecioAlquiler = 90.00m, CostoGarantia = 120.00m }
            };

            context.Prendas.AddRange(prendas);
            await context.SaveChangesAsync();
        }

        var hoy = DateTime.Today;

        var clientesDemo = new (string Dni, string Nombres, string Apellidos, string Telefono, string Direccion, int DiasDesdeRegistro)[]
        {
            ("45678912", "Rosa Elena", "Quispe Huamán", "987654321", "Av. Los Álamos 245, Surco", 13),
            ("41234567", "Miguel Ángel", "Flores Ríos", "956781234", "Jr. Puno 118, Cercado de Lima", 40),
            ("70123456", "Ana Lucía", "Rojas Campos", "912345678", "Calle Las Begonias 320, San Isidro", 25),
            ("48912736", "Rosa", "Cárdenas León", "943567890", "Jr. Amazonas 812, Cercado de Lima", 8),
            ("76432158", "Andrea", "Ponce Ruiz", "956789123", "Av. Arequipa 1540, Lince", 3)
        };

        var dnisDemo = clientesDemo.Select(c => c.Dni).ToArray();
        var existentes = await context.Clientes
            .Where(c => dnisDemo.Contains(c.Dni))
            .ToDictionaryAsync(c => c.Dni);

        foreach (var demo in clientesDemo)
        {
            var fechaRegistro = hoy.AddDays(-demo.DiasDesdeRegistro).AddHours(11).AddMinutes(24);

            if (existentes.TryGetValue(demo.Dni, out var cliente))
            {
                // Completa la fecha de los registros sembrados antes de esta mejora.
                if (cliente.FechaRegistro.Date == hoy)
                {
                    cliente.FechaRegistro = fechaRegistro;
                }

                continue;
            }

            context.Clientes.Add(new Cliente
            {
                Dni = demo.Dni,
                Nombres = demo.Nombres,
                Apellidos = demo.Apellidos,
                Telefono = demo.Telefono,
                Direccion = demo.Direccion,
                ConsentimientoDatos = true,
                Activo = true,
                FechaRegistro = fechaRegistro
            });
        }

        await context.SaveChangesAsync();

        var clientes = await context.Clientes
            .Where(c => dnisDemo.Contains(c.Dni))
            .ToDictionaryAsync(c => c.Dni);

        var prendasPorCodigo = await context.Prendas.ToDictionaryAsync(p => p.CodigoUnico);

        if (!await context.Alquileres.AnyAsync())
        {
            var alquilerUno = new Alquiler
            {
                IdCliente = clientes["45678912"].IdCliente,
                FechaAlquiler = hoy.AddDays(-4).AddHours(10),
                FechaDevolucionPactada = hoy.AddDays(-1).AddHours(19),
                Subtotal = 220.00m,
                TotalGarantia = 300.00m,
                MontoTotal = 520.00m,
                EstadoContrato = EstadoContrato.Activo,
                Observaciones = "Ambas unidades sin daños visibles. Incluye 2 fundas.",
                MedioPago = "Transferencia",
                ReferenciaPago = "BCP-2310-0520",
                UserId = userId,
                Detalles = new List<DetalleAlquiler>
                {
                    new() { IdPrenda = prendasPorCodigo["MN001"].IdPrenda, PrecioUnitarioAlquiler = 120.00m, GarantiaUnitaria = 150.00m },
                    new() { IdPrenda = prendasPorCodigo["MN002"].IdPrenda, PrecioUnitarioAlquiler = 100.00m, GarantiaUnitaria = 150.00m }
                }
            };

            var alquilerDos = new Alquiler
            {
                IdCliente = clientes["41234567"].IdCliente,
                FechaAlquiler = hoy.AddDays(-2).AddHours(15),
                FechaDevolucionPactada = hoy.AddHours(19),
                Subtotal = 140.00m,
                TotalGarantia = 200.00m,
                MontoTotal = 340.00m,
                EstadoContrato = EstadoContrato.Activo,
                Observaciones = "Terno completo revisado en entrega. Incluye funda y perchero.",
                MedioPago = "Transferencia",
                ReferenciaPago = "BCP-2410-0340",
                UserId = userId,
                Detalles = new List<DetalleAlquiler>
                {
                    new() { IdPrenda = prendasPorCodigo["TRJ001"].IdPrenda, PrecioUnitarioAlquiler = 140.00m, GarantiaUnitaria = 200.00m }
                }
            };

            var alquilerTres = new Alquiler
            {
                IdCliente = clientes["70123456"].IdCliente,
                FechaAlquiler = hoy.AddDays(-1).AddHours(11),
                FechaDevolucionPactada = hoy.AddDays(2).AddHours(18),
                Subtotal = 120.00m,
                TotalGarantia = 150.00m,
                MontoTotal = 270.00m,
                EstadoContrato = EstadoContrato.Activo,
                Observaciones = "Disfraz completo; incluye accesorios según inventario.",
                MedioPago = "Yape/Plin",
                ReferenciaPago = "YAPE-2510-0270",
                UserId = userId,
                Detalles = new List<DetalleAlquiler>
                {
                    new() { IdPrenda = prendasPorCodigo["DIS001"].IdPrenda, PrecioUnitarioAlquiler = 120.00m, GarantiaUnitaria = 150.00m }
                }
            };

            context.Alquileres.AddRange(alquilerUno, alquilerDos, alquilerTres);
        }

        // Contrato histórico ya devuelto: alimenta el historial y el indicador
        // de puntualidad de la ficha de clientes.
        var rosaElena = clientes["45678912"];
        var tieneContratoDevuelto = await context.Alquileres
            .AnyAsync(a => a.IdCliente == rosaElena.IdCliente && a.Devolucion != null);

        if (!tieneContratoDevuelto)
        {
            var detalleTerno = new DetalleAlquiler
            {
                IdPrenda = prendasPorCodigo["TRJ002"].IdPrenda,
                PrecioUnitarioAlquiler = 150.00m,
                GarantiaUnitaria = 200.00m
            };
            var detalleChaleco = new DetalleAlquiler
            {
                IdPrenda = prendasPorCodigo["CHA001"].IdPrenda,
                PrecioUnitarioAlquiler = 50.00m,
                GarantiaUnitaria = 80.00m
            };

            context.Alquileres.Add(new Alquiler
            {
                IdCliente = rosaElena.IdCliente,
                FechaAlquiler = hoy.AddDays(-10).AddHours(10),
                FechaDevolucionPactada = hoy.AddDays(-7).AddHours(18),
                Subtotal = 200.00m,
                TotalGarantia = 280.00m,
                MontoTotal = 480.00m,
                EstadoContrato = EstadoContrato.Cerrado,
                Observaciones = "Devolución completa, sin observaciones.",
                MedioPago = "Transferencia",
                ReferenciaPago = "BCP-1810-0180",
                UserId = userId,
                Detalles = new List<DetalleAlquiler> { detalleTerno, detalleChaleco },
                Devolucion = new Devolucion
                {
                    FechaDevolucionReal = hoy.AddDays(-7).AddHours(16).AddMinutes(30),
                    EstadoFisicoRecepcion = EstadoFisicoRecepcion.Optimo,
                    Observaciones = "Devolución completa, sin observaciones.",
                    MedioReembolso = "Transferencia",
                    MontoReembolso = 280.00m,
                    SaldoPendiente = 0m,
                    ClienteInformado = true,
                    UserId = userId,
                    Detalles = new List<DetalleDevolucion>
                    {
                        new()
                        {
                            DetalleAlquiler = detalleTerno,
                            EstadoFisicoRecepcion = EstadoFisicoRecepcion.Optimo,
                            CargoDanio = 0m,
                            Observaciones = "Terno completo, sin daños."
                        },
                        new()
                        {
                            DetalleAlquiler = detalleChaleco,
                            EstadoFisicoRecepcion = EstadoFisicoRecepcion.Optimo,
                            CargoDanio = 0m,
                            Observaciones = "Chaleco completo, sin daños."
                        }
                    }
                }
            });
        }

        if (!await context.Mantenimientos.AnyAsync())
        {
            context.Mantenimientos.Add(new Mantenimiento
            {
                IdPrenda = prendasPorCodigo["POL001"].IdPrenda,
                FechaIngreso = hoy.AddDays(-2).AddHours(9),
                FechaSalidaEstimada = hoy.AddDays(1),
                TipoServicio = TipoServicio.Lavanderia,
                CostoServicio = 25.00m,
                Observaciones = "Limpieza de pollera bordada; revisar el hilvanado del ruedo."
            });
        }

        await context.SaveChangesAsync();
    }
}

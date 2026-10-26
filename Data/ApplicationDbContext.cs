using AlquilerVestimentasLucy.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AlquilerVestimentasLucy.Data;

/// <summary>
/// Contexto de EF Core (Capa 3). Mapea las entidades del dominio normalizadas
/// a 3FN y hereda las tablas de ASP.NET Core Identity.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Prenda> Prendas => Set<Prenda>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Alquiler> Alquileres => Set<Alquiler>();
    public DbSet<DetalleAlquiler> DetallesAlquiler => Set<DetalleAlquiler>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<Penalidad> Penalidades => Set<Penalidad>();
    public DbSet<Mantenimiento> Mantenimientos => Set<Mantenimiento>();
    public DbSet<ConfiguracionPolitica> ConfiguracionesPolitica => Set<ConfiguracionPolitica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Prenda>(entity =>
        {
            entity.HasIndex(p => p.CodigoUnico).IsUnique();
            entity.Property(p => p.PrecioAlquiler).HasPrecision(10, 2);
            entity.Property(p => p.CostoGarantia).HasPrecision(10, 2);
            entity.Property(p => p.Estado).HasConversion<int>();
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.Property(c => c.Dni).HasMaxLength(8).IsRequired();
            entity.HasIndex(c => c.Dni).IsUnique();
            entity.Ignore(c => c.NombreCompleto);
        });

        modelBuilder.Entity<Alquiler>(entity =>
        {
            entity.Property(a => a.Subtotal).HasPrecision(10, 2);
            entity.Property(a => a.TotalGarantia).HasPrecision(10, 2);
            entity.Property(a => a.MontoTotal).HasPrecision(10, 2);
            entity.Property(a => a.EstadoContrato).HasConversion<int>();
            entity.Property(a => a.Observaciones).HasMaxLength(500);
            entity.Property(a => a.MedioPago).HasMaxLength(30);
            entity.Property(a => a.ReferenciaPago).HasMaxLength(50);
            entity.Ignore(a => a.CodigoContrato);

            entity.HasOne(a => a.Cliente)
                .WithMany(c => c.Alquileres)
                .HasForeignKey(a => a.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DetalleAlquiler>(entity =>
        {
            entity.Property(d => d.PrecioUnitarioAlquiler).HasPrecision(10, 2);
            entity.Property(d => d.GarantiaUnitaria).HasPrecision(10, 2);

            entity.HasOne(d => d.Alquiler)
                .WithMany(a => a.Detalles)
                .HasForeignKey(d => d.IdAlquiler)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Prenda)
                .WithMany(p => p.Detalles)
                .HasForeignKey(d => d.IdPrenda)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Devolucion>(entity =>
        {
            entity.HasIndex(d => d.IdAlquiler).IsUnique();
            entity.Property(d => d.EstadoFisicoRecepcion).HasConversion<int>();

            entity.HasOne(d => d.Alquiler)
                .WithOne(a => a.Devolucion)
                .HasForeignKey<Devolucion>(d => d.IdAlquiler)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Usuario)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Penalidad>(entity =>
        {
            entity.Property(p => p.TarifaMoraAplicada).HasPrecision(10, 2);
            entity.Property(p => p.MontoMora).HasPrecision(10, 2);
            entity.Property(p => p.CostoBaseAplicado).HasPrecision(10, 2);
            entity.Property(p => p.MontoDanio).HasPrecision(10, 2);
            entity.Property(p => p.TotalPenalidad).HasPrecision(10, 2);

            entity.HasOne(p => p.Devolucion)
                .WithOne(d => d.Penalidad)
                .HasForeignKey<Penalidad>(p => p.IdDevolucion)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Mantenimiento>(entity =>
        {
            entity.Property(m => m.CostoServicio).HasPrecision(10, 2);
            entity.Property(m => m.TipoServicio).HasConversion<int>();
            entity.Ignore(m => m.EnCurso);

            entity.HasOne(m => m.Prenda)
                .WithMany(p => p.Mantenimientos)
                .HasForeignKey(m => m.IdPrenda)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConfiguracionPolitica>(entity =>
        {
            entity.Property(c => c.TarifaMoraDiaria).HasPrecision(10, 2);
            entity.Property(c => c.PorcentajeDanioLeve).HasPrecision(5, 2);
            entity.Property(c => c.PorcentajeDanioGrave).HasPrecision(5, 2);
            entity.Property(c => c.CostoBaseReparacion).HasPrecision(10, 2);
        });
    }
}

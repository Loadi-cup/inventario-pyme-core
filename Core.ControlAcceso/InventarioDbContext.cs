using Microsoft.EntityFrameworkCore;
using Core.ControlAcceso.Modelos;

namespace Core.ControlAcceso;

public class InventarioDbContext : DbContext
{
    public InventarioDbContext(DbContextOptions<InventarioDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<CorreoEnCola> CorreosEnCola => Set<CorreoEnCola>();
    public DbSet<TokenActivacion> TokensActivacion => Set<TokenActivacion>();
    public DbSet<CodigoRecuperacion> CodigosRecuperacion => Set<CodigoRecuperacion>();
    public DbSet<TokenInvalidado> TokensInvalidados => Set<TokenInvalidado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Correo)
            .IsUnique();

        modelBuilder.Entity<TokenInvalidado>()
            .HasIndex(t => t.Jti)
            .IsUnique();
    }
}
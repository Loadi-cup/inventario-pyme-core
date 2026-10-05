using Microsoft.EntityFrameworkCore;
using Core.Negocio.Modelos;

namespace Core.Negocio;

public class NegocioDbContext : DbContext
{
    public NegocioDbContext(DbContextOptions<NegocioDbContext> options)
        : base(options)
    {
    }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<OrdenDeCompra> OrdenesDeCompra => Set<OrdenDeCompra>();
    public DbSet<ItemOrdenDeCompra> ItemsOrdenDeCompra => Set<ItemOrdenDeCompra>();
}
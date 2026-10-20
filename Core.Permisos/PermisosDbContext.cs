using Microsoft.EntityFrameworkCore;
using Core.Permisos.Modelos;

namespace Core.Permisos;

public class PermisosDbContext : DbContext
{
    public PermisosDbContext(DbContextOptions<PermisosDbContext> options)
        : base(options)
    {
    }

    public DbSet<SolicitudPermiso> Solicitudes => Set<SolicitudPermiso>();
    public DbSet<PermisoOtorgado> PermisosOtorgados => Set<PermisoOtorgado>();
}
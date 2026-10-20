using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Core.Permisos.Modelos;
using Xunit;

namespace Core.Permisos.Tests;

// RD-12: prueba la pieza directamente, sin levantar la API.
// xUnit crea una instancia nueva de esta clase por cada prueba, asi que cada una
// tiene su propia base SQLite en memoria y son independientes entre si.
public class GestionPermisosTests : IDisposable
{
    private const int Solicitante = 1;
    private const int Administrador = 2;

    private readonly SqliteConnection _conexion;
    private readonly PermisosDbContext _db;
    private readonly SolicitudPermisoService _servicio;

    public GestionPermisosTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        var opciones = new DbContextOptionsBuilder<PermisosDbContext>()
            .UseSqlite(_conexion)
            .Options;
        _db = new PermisosDbContext(opciones);
        _db.Database.EnsureCreated();
        _servicio = new SolicitudPermisoService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private async Task<int> CrearSolicitudPendienteAsync(int solicitanteId = Solicitante)
    {
        var r = await _servicio.CrearAsync(solicitanteId, "exportar-reportes");
        return r.Solicitud!.Id;
    }

    // Lee el estado real guardado, no el objeto en memoria
    private SolicitudPermiso LeerDeLaBaseDeDatos(int id)
    {
        _db.ChangeTracker.Clear();
        return _db.Solicitudes.Single(s => s.Id == id);
    }

    [Fact]
    public async Task Crear_Solicitud_NaceEnEstadoPendiente()
    {
        // Preparar
        // (nada previo)

        // Actuar
        var resultado = await _servicio.CrearAsync(Solicitante, "exportar-reportes");

        // Verificar
        Assert.True(resultado.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, LeerDeLaBaseDeDatos(resultado.Solicitud!.Id).Estado);
    }

    [Fact]
    public async Task Aprobar_SolicitudPendiente_QuedaAplicadaConAprobadorYFecha()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();
        var antes = DateTime.UtcNow;

        // Actuar
        var resultado = await _servicio.AprobarAsync(id, Administrador);

        // Verificar
        Assert.True(resultado.Exitoso);
        var guardada = LeerDeLaBaseDeDatos(id);
        Assert.Equal(EstadoSolicitud.Aplicada, guardada.Estado);
        Assert.Equal(Administrador, guardada.AprobadorId);
        Assert.NotNull(guardada.FechaResolucion);
        Assert.InRange(guardada.FechaResolucion!.Value, antes, DateTime.UtcNow);
    }

    [Fact]
    public async Task Aprobar_Solicitud_OtorgaElPermisoAlSolicitante()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();

        // Actuar
        await _servicio.AprobarAsync(id, Administrador);

        // Verificar
        Assert.True(await _servicio.TienePermisoAsync(Solicitante, "exportar-reportes"));
    }

    [Fact]
    public async Task Rechazar_SolicitudPendienteConMotivo_QuedaRechazadaConMotivoYAprobador()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();

        // Actuar
        var resultado = await _servicio.RechazarAsync(id, Administrador, "No corresponde a su rol");

        // Verificar
        Assert.True(resultado.Exitoso);
        var guardada = LeerDeLaBaseDeDatos(id);
        Assert.Equal(EstadoSolicitud.Rechazada, guardada.Estado);
        Assert.Equal("No corresponde a su rol", guardada.MotivoRechazo);
        Assert.Equal(Administrador, guardada.AprobadorId);
    }

    [Fact]
    public async Task Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();
        await _servicio.AprobarAsync(id, Administrador);

        // Actuar
        var segundoIntento = await _servicio.AprobarAsync(id, Administrador);

        // Verificar
        Assert.False(segundoIntento.Exitoso);
        Assert.Equal(EstadoSolicitud.Aplicada, LeerDeLaBaseDeDatos(id).Estado);
    }

    [Fact]
    public async Task Aprobar_SolicitudRechazada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();
        await _servicio.RechazarAsync(id, Administrador, "Motivo de prueba");

        // Actuar
        var intento = await _servicio.AprobarAsync(id, Administrador);

        // Verificar
        Assert.False(intento.Exitoso);
        Assert.Equal(EstadoSolicitud.Rechazada, LeerDeLaBaseDeDatos(id).Estado);
    }

    [Fact]
    public async Task Rechazar_SinMotivo_NoSeCompletaYLaSolicitudSigueEnPendiente()
    {
        // Preparar
        var id = await CrearSolicitudPendienteAsync();

        // Actuar
        var resultado = await _servicio.RechazarAsync(id, Administrador, "   ");

        // Verificar
        Assert.False(resultado.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, LeerDeLaBaseDeDatos(id).Estado);
    }

    [Fact]
    public async Task Aprobar_SolicitudPropia_SeRechazaYLaSolicitudSigueEnPendiente()
    {
        // Preparar: el administrador (id 2) crea su propia solicitud
        var id = await CrearSolicitudPendienteAsync(solicitanteId: Administrador);

        // Actuar
        var resultado = await _servicio.AprobarAsync(id, Administrador);

        // Verificar
        Assert.False(resultado.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, LeerDeLaBaseDeDatos(id).Estado);
    }
}
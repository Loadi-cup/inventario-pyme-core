using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Core.Permisos.Modelos;
using Xunit;

namespace Core.Permisos.Tests;

// RD-12: prueba la pieza directamente, sin levantar la API.
// xUnit crea una instancia nueva por prueba: cada una tiene su propia base SQLite
// en memoria, asi que son independientes y pasan en cualquier orden.
public class GestionPermisosTests : IDisposable
{
    private const int Solicitante = 1;
    private const int Administrador = 2;
    private const int OtroUsuario = 3;
    private const string Permiso = "exportar-reportes";

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

    private async Task<int> CrearPendienteAsync(int solicitanteId = Solicitante, string permiso = Permiso)
    {
        var r = await _servicio.CrearAsync(solicitanteId, permiso);
        return r.Solicitud!.Id;
    }

    // Deja una solicitud nueva en el estado pedido, usando solo transiciones validas
    private async Task<int> CrearEnEstadoAsync(EstadoSolicitud estado)
    {
        var id = await CrearPendienteAsync();
        switch (estado)
        {
            case EstadoSolicitud.Aprobada:
                await _servicio.TransicionarAsync(id, EstadoSolicitud.Aprobada);
                break;
            case EstadoSolicitud.Aplicada:
                await _servicio.AprobarAsync(id, Administrador);
                break;
            case EstadoSolicitud.Rechazada:
                await _servicio.RechazarAsync(id, Administrador, "Motivo");
                break;
        }
        return id;
    }

    // Lee lo realmente guardado, no el objeto en memoria
    private SolicitudPermiso Leer(int id)
    {
        _db.ChangeTracker.Clear();
        return _db.Solicitudes.Single(s => s.Id == id);
    }

    private int ContarPermisosOtorgados() => _db.PermisosOtorgados.Count();

    // ---------- RF-GP-01 ----------

    [Fact]
    public async Task RF_GP_01_Crear_Solicitud_NaceEnEstadoPendiente()
    {
        // Preparar: nada previo
        // Actuar
        var r = await _servicio.CrearAsync(Solicitante, Permiso);
        // Verificar
        Assert.True(r.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, Leer(r.Solicitud!.Id).Estado);
    }

    [Fact]
    public async Task RF_GP_01_Crear_Solicitud_GuardaSolicitanteYPermisoPedido()
    {
        // Preparar: nada previo
        // Actuar
        var r = await _servicio.CrearAsync(Solicitante, Permiso);
        // Verificar
        var guardada = Leer(r.Solicitud!.Id);
        Assert.Equal(Solicitante, guardada.SolicitanteId);
        Assert.Equal(Permiso, guardada.PermisoSolicitado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RF_GP_01_Crear_ConPermisoVacio_SeRechazaYNoSeGuardaNada(string permiso)
    {
        // Preparar: nada previo
        // Actuar
        var r = await _servicio.CrearAsync(Solicitante, permiso);
        // Verificar
        Assert.False(r.Exitoso);
        Assert.Empty(_db.Solicitudes);
    }

    // ---------- RF-GP-02 ----------

    [Fact]
    public async Task RF_GP_02_ListarPendientes_DevuelveLasPendientesDeTodosLosUsuarios()
    {
        // Preparar
        var a = await CrearPendienteAsync(Solicitante);
        var b = await CrearPendienteAsync(OtroUsuario);
        // Actuar
        var lista = await _servicio.ListarPendientesAsync();
        // Verificar
        Assert.Equal(new[] { a, b }, lista.Select(s => s.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task RF_GP_02_ListarPendientes_NoIncluyeLasYaResueltas()
    {
        // Preparar
        var aprobada = await CrearPendienteAsync(Solicitante);
        var rechazada = await CrearPendienteAsync(Solicitante);
        var pendiente = await CrearPendienteAsync(Solicitante);
        await _servicio.AprobarAsync(aprobada, Administrador);
        await _servicio.RechazarAsync(rechazada, Administrador, "Motivo");
        // Actuar
        var lista = await _servicio.ListarPendientesAsync();
        // Verificar
        Assert.Equal(new[] { pendiente }, lista.Select(s => s.Id));
    }

    [Fact]
    public async Task RF_GP_02_ListarDeUsuario_NoMuestraSolicitudesDeOtrosUsuarios()
    {
        // Preparar
        var mia = await CrearPendienteAsync(Solicitante);
        await CrearPendienteAsync(OtroUsuario);
        // Actuar
        var lista = await _servicio.ListarDeUsuarioAsync(Solicitante);
        // Verificar
        Assert.Equal(new[] { mia }, lista.Select(s => s.Id));
    }

    // ---------- RF-GP-03 ----------

    [Fact]
    public async Task RF_GP_03_Aprobar_RegistraQuienAprobo()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.Equal(Administrador, Leer(id).AprobadorId);
    }

    [Fact]
    public async Task RF_GP_03_Aprobar_RegistraCuandoSeAprobo()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        var antes = DateTime.UtcNow;
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        var fecha = Leer(id).FechaResolucion;
        Assert.NotNull(fecha);
        Assert.InRange(fecha!.Value, antes, DateTime.UtcNow);
    }

    // ---------- RF-GP-04 ----------

    [Fact]
    public async Task RF_GP_04_Rechazar_ConMotivo_QuedaRechazada()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        var r = await _servicio.RechazarAsync(id, Administrador, "No corresponde a su rol");
        // Verificar
        Assert.True(r.Exitoso);
        Assert.Equal(EstadoSolicitud.Rechazada, Leer(id).Estado);
    }

    [Fact]
    public async Task RF_GP_04_Rechazar_GuardaElMotivoEscrito()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        await _servicio.RechazarAsync(id, Administrador, "No corresponde a su rol");
        // Verificar
        Assert.Equal("No corresponde a su rol", Leer(id).MotivoRechazo);
    }

    [Fact]
    public async Task RF_GP_04_Rechazar_RegistraQuienRechazoYCuando()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        var antes = DateTime.UtcNow;
        // Actuar
        await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Verificar
        var guardada = Leer(id);
        Assert.Equal(Administrador, guardada.AprobadorId);
        Assert.InRange(guardada.FechaResolucion!.Value, antes, DateTime.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RF_GP_04_Rechazar_SinMotivo_NoSeCompletaYElEstadoNoCambia(string? motivo)
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        var r = await _servicio.RechazarAsync(id, Administrador, motivo!);
        // Verificar
        Assert.False(r.Exitoso);
        var guardada = Leer(id);
        Assert.Equal(EstadoSolicitud.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }

    // ---------- RF-GP-05 ----------

    [Fact]
    public async Task RF_GP_05_Aprobar_LlevaLaSolicitudAAplicada()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.Equal(EstadoSolicitud.Aplicada, Leer(id).Estado);
    }

    [Fact]
    public async Task RF_GP_05_Aprobar_DejaEfectivoElPermisoPedido()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.True(await _servicio.TienePermisoAsync(Solicitante, Permiso));
    }

    [Fact]
    public async Task RF_GP_05_Aprobar_NoOtorgaElPermisoAOtroUsuario()
    {
        // Preparar
        var id = await CrearPendienteAsync(Solicitante);
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(await _servicio.TienePermisoAsync(OtroUsuario, Permiso));
    }

    [Fact]
    public async Task RF_GP_05_Rechazar_NoOtorgaElPermiso()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        // Actuar
        await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Verificar
        Assert.False(await _servicio.TienePermisoAsync(Solicitante, Permiso));
    }

    // ---------- RF-GP-06 ----------

    [Fact]
    public async Task RF_GP_06_Aprobar_DosVeces_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.AprobarAsync(id, Administrador);
        // Actuar
        var segundo = await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(segundo.Exitoso);
        Assert.Equal(EstadoSolicitud.Aplicada, Leer(id).Estado);
    }

    [Fact]
    public async Task RF_GP_06_Aprobar_DosVeces_NoDuplicaElPermisoOtorgado()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.AprobarAsync(id, Administrador);
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.Equal(1, ContarPermisosOtorgados());
    }

    [Fact]
    public async Task RF_GP_06_Aprobar_UnaRechazada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Actuar
        var intento = await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(intento.Exitoso);
        Assert.Equal(EstadoSolicitud.Rechazada, Leer(id).Estado);
    }

    [Fact]
    public async Task RF_GP_06_Aprobar_UnaRechazada_NoOtorgaElPermiso()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(await _servicio.TienePermisoAsync(Solicitante, Permiso));
    }

    [Fact]
    public async Task RF_GP_06_Rechazar_UnaAplicada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.AprobarAsync(id, Administrador);
        // Actuar
        var intento = await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Verificar
        Assert.False(intento.Exitoso);
        var guardada = Leer(id);
        Assert.Equal(EstadoSolicitud.Aplicada, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }

    [Fact]
    public async Task RF_GP_06_Rechazar_DosVeces_SeRechazaYElMotivoNoCambia()
    {
        // Preparar
        var id = await CrearPendienteAsync();
        await _servicio.RechazarAsync(id, Administrador, "Primer motivo");
        // Actuar
        var segundo = await _servicio.RechazarAsync(id, Administrador, "Segundo motivo");
        // Verificar
        Assert.False(segundo.Exitoso);
        var guardada = Leer(id);
        Assert.Equal(EstadoSolicitud.Rechazada, guardada.Estado);
        Assert.Equal("Primer motivo", guardada.MotivoRechazo);
    }

    [Fact]
    public async Task RF_GP_06_Resolver_SolicitudInexistente_SeRechazaSinExcepcion()
    {
        // Preparar: no existe ninguna solicitud
        // Actuar
        var aprobar = await _servicio.AprobarAsync(999, Administrador);
        var rechazar = await _servicio.RechazarAsync(999, Administrador, "Motivo");
        // Verificar
        Assert.False(aprobar.Exitoso);
        Assert.False(rechazar.Exitoso);
    }

    [Theory]
    [InlineData(EstadoSolicitud.Pendiente, EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Pendiente, EstadoSolicitud.Rechazada)]
    [InlineData(EstadoSolicitud.Aprobada, EstadoSolicitud.Aplicada)]
    public async Task RF_GP_06_Transicionar_DentroDeLaTabla_CambiaElEstado(EstadoSolicitud desde, EstadoSolicitud hacia)
    {
        // Preparar
        var id = await CrearEnEstadoAsync(desde);
        // Actuar
        var r = await _servicio.TransicionarAsync(id, hacia);
        // Verificar
        Assert.True(r.Exitoso);
        Assert.Equal(hacia, Leer(id).Estado);
    }

    [Theory]
    [InlineData(EstadoSolicitud.Pendiente, EstadoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Pendiente, EstadoSolicitud.Aplicada)]
    [InlineData(EstadoSolicitud.Aprobada, EstadoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Aprobada, EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Aprobada, EstadoSolicitud.Rechazada)]
    [InlineData(EstadoSolicitud.Aplicada, EstadoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Aplicada, EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Aplicada, EstadoSolicitud.Aplicada)]
    [InlineData(EstadoSolicitud.Aplicada, EstadoSolicitud.Rechazada)]
    [InlineData(EstadoSolicitud.Rechazada, EstadoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Rechazada, EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Rechazada, EstadoSolicitud.Aplicada)]
    [InlineData(EstadoSolicitud.Rechazada, EstadoSolicitud.Rechazada)]
    public async Task RF_GP_06_Transicionar_FueraDeLaTabla_SeRechazaYElEstadoNoCambia(EstadoSolicitud desde, EstadoSolicitud hacia)
    {
        // Preparar
        var id = await CrearEnEstadoAsync(desde);
        // Actuar
        var r = await _servicio.TransicionarAsync(id, hacia);
        // Verificar
        Assert.False(r.Exitoso);
        Assert.Equal(desde, Leer(id).Estado);
    }

    // ---------- RF-GP-07 ----------

    [Fact]
    public async Task RF_GP_07_Aprobar_LaPropiaSolicitud_SeRechazaYSigueEnPendiente()
    {
        // Preparar: el administrador crea su propia solicitud
        var id = await CrearPendienteAsync(solicitanteId: Administrador);
        // Actuar
        var r = await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(r.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, Leer(id).Estado);
    }

    [Fact]
    public async Task RF_GP_07_Aprobar_LaPropiaSolicitud_NoOtorgaElPermiso()
    {
        // Preparar
        var id = await CrearPendienteAsync(solicitanteId: Administrador);
        // Actuar
        await _servicio.AprobarAsync(id, Administrador);
        // Verificar
        Assert.False(await _servicio.TienePermisoAsync(Administrador, Permiso));
    }

    [Fact]
    public async Task RF_GP_07_Rechazar_LaPropiaSolicitud_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var id = await CrearPendienteAsync(solicitanteId: Administrador);
        // Actuar
        var r = await _servicio.RechazarAsync(id, Administrador, "Motivo");
        // Verificar
        Assert.False(r.Exitoso);
        Assert.Equal(EstadoSolicitud.Pendiente, Leer(id).Estado);
    }
}
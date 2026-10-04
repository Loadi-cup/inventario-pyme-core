namespace Core.ControlAcceso.Modelos;

public enum Rol
{
    Estandar,
    Administrador
}

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string HashContrasena { get; set; } = string.Empty;
    public Rol Rol { get; set; } = Rol.Estandar;
    public bool Activo { get; set; } = false;

    // RF-CA-19: bloqueo por intentos fallidos
    public int IntentosFallidos { get; set; } = 0;
    public DateTime? BloqueadoHasta { get; set; }
}
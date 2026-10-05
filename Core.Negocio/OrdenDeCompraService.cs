using Core.Negocio.Modelos;

namespace Core.Negocio;

public class OrdenDeCompraService
{
    // RD-04: unico lugar donde se decide que transiciones son validas
    private static readonly Dictionary<EstadoOrdenCompra, EstadoOrdenCompra[]> TransicionesPermitidas = new()
    {
        [EstadoOrdenCompra.Borrador] = new[] { EstadoOrdenCompra.Enviada },
        [EstadoOrdenCompra.Enviada] = new[] { EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada },
        [EstadoOrdenCompra.Recibida] = Array.Empty<EstadoOrdenCompra>(),   // RF-NEG-05: terminal
        [EstadoOrdenCompra.Cancelada] = Array.Empty<EstadoOrdenCompra>()  // RF-NEG-05: terminal
    };

    public bool PuedeTransicionar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia)
    {
        return TransicionesPermitidas.TryGetValue(desde, out var permitidas)
            && permitidas.Contains(hacia);
    }

    // RF-NEG-04: ejemplo de transicion explicitamente prohibida -> Recibida a Cancelada se rechaza aqui mismo
    public (bool Exitoso, string? Error) Transicionar(OrdenDeCompra orden, EstadoOrdenCompra nuevoEstado)
    {
        if (!PuedeTransicionar(orden.Estado, nuevoEstado))
            return (false, $"No se puede pasar de {orden.Estado} a {nuevoEstado}.");

        orden.Estado = nuevoEstado;
        if (nuevoEstado == EstadoOrdenCompra.Recibida)
            orden.FechaRecepcion = DateTime.UtcNow;

        return (true, null);
    }
}
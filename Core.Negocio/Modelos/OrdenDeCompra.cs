namespace Core.Negocio.Modelos;

public class OrdenDeCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    // RD-04: el estado vive en esta entidad, las transiciones se resuelven en OrdenDeCompraService
    public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Borrador;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRecepcion { get; set; }

    public List<ItemOrdenDeCompra> Items { get; set; } = new();
}
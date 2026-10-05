namespace Core.Negocio.Modelos;

public class ItemOrdenDeCompra
{
    public int Id { get; set; }
    public int OrdenDeCompraId { get; set; }
    public OrdenDeCompra OrdenDeCompra { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }
}
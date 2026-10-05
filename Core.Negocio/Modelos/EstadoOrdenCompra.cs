namespace Core.Negocio.Modelos;

// RF-NEG-03: estados declarados en un solo lugar, entre 3 y 5
public enum EstadoOrdenCompra
{
    Borrador,
    Enviada,
    Recibida,   // RF-NEG-05: estado terminal
    Cancelada   // RF-NEG-05: estado terminal
}
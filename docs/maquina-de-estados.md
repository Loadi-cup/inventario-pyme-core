# Máquina de estados — OrdenDeCompra

Entidad central: `OrdenDeCompra` (Core.Negocio), con atributo `Estado` de tipo `EstadoOrdenCompra`.

## Tabla de transiciones

| Desde     | Hacia      | Quién la ejecuta  | Condición                              |
|-----------|------------|--------------------|-----------------------------------------|
| Borrador  | Enviada    | Usuario Estandar   | La orden tiene al menos un item.        |
| Enviada   | Recibida   | Usuario Estandar   | Ninguna, se marca al llegar la mercancia.|
| Enviada   | Cancelada  | Usuario Estandar   | Ninguna.                                |
| Recibida  | —          | —                  | Estado terminal, no tiene salidas.      |
| Cancelada | —          | —                  | Estado terminal, no tiene salidas.      |

Cualquier otra combinación (por ejemplo, `Recibida -> Cancelada`) está prohibida
y se rechaza en `OrdenDeCompraService.Transicionar`, que es el único punto del
código donde se resuelven las transiciones (RD-04).
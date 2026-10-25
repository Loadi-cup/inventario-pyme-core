# Inventario Pyme Core

Sistema de gestión de inventario para pequeños negocios. Proyecto de Programación III — ITLA 2026-C-3.

- **Autor:** Loadi Romero
- **Lenguaje/Framework:** C# / ASP.NET Core 10
- **Base de datos:** SQLite (archivo `inventario.db`, se crea sola al arrancar)

## Estructura del proyecto

- `Core.ControlAcceso` — Pieza 1 del Core: usuarios, sesión, roles, recuperación de contraseña.
- `Core.Negocio` — Módulo de negocio: productos, proveedores, órdenes de compra y su máquina de estados.
- `Core.Permisos` — Gestión de permisos (solicitudes, aprobación, rechazo).
- `Core.Permisos.Tests` — Suite de pruebas de `Core.Permisos`.
- `Api` — ASP.NET Core Web API que expone los endpoints.
- `docs/maquina-de-estados.md` — Tabla de transiciones de la máquina de estados del negocio.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Una cuenta de correo SMTP (Gmail con contraseña de aplicación) solo si se quiere probar el envío real de correos.

## Variables de entorno

Se definen en la misma ventana de PowerShell donde se arranca el servidor (valen solo para esa ventana):

```powershell
$env:Jwt__Clave = "<cadena aleatoria de 32+ caracteres>"
$env:Admin__Correo = "admin@ejemplo.com"
$env:Admin__Contrasena = "Admin12345"
```

| Variable | Para qué sirve |
|---|---|
| `Jwt__Clave` | Firma los tokens de sesión (JWT). Cualquier cadena larga y aleatoria. |
| `Admin__Correo` / `Admin__Contrasena` | Crean el Administrador inicial al arrancar, si no existe. No hace falta tocar la base de datos. |
| `Smtp__Usuario` | Cuenta desde la que se envían los correos de activación y recuperación. Solo para el envío real. |
| `Smtp__ContrasenaApp` | Contraseña de aplicación de esa cuenta (no la contraseña normal). Solo para el envío real. |

Ninguna credencial está en el código ni en el repositorio.

## Cómo ejecutar el proyecto

```powershell
git clone https://github.com/Loadi-cup/inventario-pyme-core.git
cd inventario-pyme-core
# definir las variables de entorno de la sección anterior
dotnet run --project Api
```

Las migraciones se aplican solas al arrancar y `inventario.db` se crea si no existe. El servidor escucha en `http://localhost:5118` (solo HTTP).

## Envío de correos

Los correos de activación y recuperación se guardan en una cola y se envían con un comando independiente, fuera del servidor:

```powershell
$env:Smtp__Usuario = "<correo del remitente>"
$env:Smtp__ContrasenaApp = "<contraseña de aplicación>"
dotnet run --no-build --project Api -- enviar-correos
```

Imprime `Correos enviados: N`. Ejecutarlo dos veces seguidas no reenvía los correos ya enviados. Cada correo tiene estados `Pendiente`, `Enviado` y `Fallido`, con un máximo de 3 intentos.

**Nota de entorno:** en la máquina de desarrollo, Google rechazó las credenciales SMTP y no se pudo confirmar el envío real. El código usa MailKit con STARTTLS en el puerto 587. Quien lo pruebe debe definir sus propias variables `Smtp__*`.

Los códigos de activación y recuperación llegan **en el cuerpo del correo** (no hay enlace GET que abrir).

## Cómo provocar cada criterio de Control de acceso

Se usa `Invoke-RestMethod` en PowerShell, o Postman.

### Administrador inicial

Al arrancar con `Admin__Correo` y `Admin__Contrasena` definidos, el sistema crea ese usuario como Administrador activo.

```powershell
$login = @{ correo = "admin@ejemplo.com"; contrasena = "Admin12345" } | ConvertTo-Json
$tokenAdmin = (Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $login -ContentType "application/json").token
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios" -Headers @{ Authorization = "Bearer $tokenAdmin" }
```

### Registro y activación (RF-CA-01, 02, 14, 15, 16, 17)

```powershell
$body = @{ nombre = "Prueba"; correo = "correo@ejemplo.com"; contrasena = "Clave1234" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/registro" -Method Post -Body $body -ContentType "application/json"
# Repetir el registro: se rechaza por correo duplicado (RF-CA-01)
# Registrar con "abc" como contraseña: se rechaza por política (RF-CA-14)

# Enviar el correo de activación (comando independiente)
dotnet run --no-build --project Api -- enviar-correos

# Login antes de activar: se rechaza (RF-CA-15)
$bodyLogin = @{ correo = "correo@ejemplo.com"; contrasena = "Clave1234" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $bodyLogin -ContentType "application/json"

# Activar con el código recibido por correo
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/activar?codigo=<codigo-del-correo>" -Method Get
# Usar el mismo código otra vez: se rechaza (RF-CA-16)

# Reenviar activación con correo inexistente y existente: misma respuesta (RF-CA-17)
$bodyReenvio = @{ correo = "correo@ejemplo.com" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/reenviar-activacion" -Method Post -Body $bodyReenvio -ContentType "application/json"
```

### Sesión (RF-CA-03, 07, 18, 19)

```powershell
$token = (Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $bodyLogin -ContentType "application/json").token
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/yo" -Headers @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/logout" -Method Post -Headers @{ Authorization = "Bearer $token" }
# Reusar el token tras el logout: se rechaza (RF-CA-18)
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/yo" -Headers @{ Authorization = "Bearer $token" }
# 5 logins fallidos seguidos: el 6.º se rechaza durante 15 minutos aunque la clave sea correcta (RF-CA-19)
```

### Roles y administración (RF-CA-04, 05, 06, 08, 20, 21)

Todos los endpoints de `/api/admin/` exigen sesión de Administrador.

```powershell
$ha = @{ Authorization = "Bearer $tokenAdmin" }
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios" -Headers $ha                                  # RF-CA-21
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios" -Headers @{ Authorization = "Bearer $token" } # Estándar: rechazado (RF-CA-05, 06, RD-06)
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios"                                               # sin sesión: rechazado

$bodyRol = @{ nuevoRol = "Administrador" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/rol" -Method Put -Body $bodyRol -ContentType "application/json" -Headers $ha   # RF-CA-08
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/desactivar" -Method Post -Headers $ha                                          # RF-CA-20
# Cambiar el propio rol o desactivarse a sí mismo: se rechaza
```

### Contraseñas (RF-CA-09 a 13, 22)

```powershell
$bodyRecuperar = @{ correo = "correo@ejemplo.com" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/recuperar" -Method Post -Body $bodyRecuperar -ContentType "application/json"   # misma respuesta exista o no (RF-CA-09)
dotnet run --no-build --project Api -- enviar-correos

# El correo trae un código; se usa en el cuerpo de la petición (RF-CA-10, 11, 13)
$bodyRestablecer = @{ codigo = "<codigo-del-correo>"; nuevaContrasena = "ClaveNueva123" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/restablecer" -Method Post -Body $bodyRestablecer -ContentType "application/json"
# El token viejo tras restablecer: se rechaza (RF-CA-12)

Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/forzar-restablecimiento" -Method Post -Headers $ha   # RF-CA-13

$bodyCambiar = @{ contrasenaActual = "ClaveNueva123"; contrasenaNueva = "OtraClave456" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/cambiar-contrasena" -Method Post -Body $bodyCambiar -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }   # RF-CA-22
```

### Correo por cola (RF-NOT-08, 09, 12, 13)

Ver la sección «Envío de correos». El envío es independiente de la operación que generó el correo.

### Máquina de estados del negocio (RF-NEG-03, 04, 05)

Los estados y transiciones están en `Core.Negocio/Modelos/EstadoOrdenCompra.cs` y `Core.Negocio/OrdenDeCompraService.cs`. La tabla completa está en `docs/maquina-de-estados.md`.

## Gestión de permisos (Asignación 2)

Pieza `Core.Permisos`, expuesta por `Api/Controllers/PermisosController.cs`. No depende de `Core.ControlAcceso` ni de `Core.Negocio`: guarda los usuarios como ids numéricos.

Transiciones válidas: `Pendiente → Aprobada → Aplicada` y `Pendiente → Rechazada`. Cualquier otra se rechaza y el estado no cambia. Como `Aprobada → Aplicada` es automática e inmediata, aprobar deja la solicitud en `Aplicada`. Todas las transiciones se resuelven en un solo punto: el diccionario `TransicionesPermitidas` de `Core.Permisos/SolicitudPermisoService.cs`. El Administrador también puede crear solicitudes (se usa para probar RF-GP-07).

### Preparar y arrancar

Con las variables de entorno definidas:

```powershell
dotnet run --project Api
```

### Usuarios necesarios

1. El Administrador ya existe por `Admin__Correo` / `Admin__Contrasena`. Obtener su token como en «Administrador inicial».
2. Registrar un usuario Estándar (`POST /api/auth/registro`), enviar el correo con `enviar-correos` y activarlo con el código recibido.
3. Obtener su token:

```powershell
$loginStd = @{ correo = "<correo>"; contrasena = "<clave>" } | ConvertTo-Json
$tokenEstandar = (Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $loginStd -ContentType "application/json").token
```

### Cómo provocar cada criterio

Con una base de permisos vacía, las solicitudes se numeran 1, 2 y 3. La función `Probar` muestra los rechazos en lugar de lanzar error.

```powershell
function Probar($uri, $metodo, $headers, $body) {
  try {
    $p = @{ Uri = $uri; Method = $metodo; Headers = $headers }
    if ($body) { $p.Body = $body; $p.ContentType = "application/json" }
    Invoke-RestMethod @p
  } catch {
    "RECHAZADO: " + $_.Exception.Response.StatusCode.value__ + " " + $_.ErrorDetails.Message
  }
}

$base = "http://localhost:5118/api/permisos"
$h  = @{ Authorization = "Bearer $tokenEstandar" }
$ha = @{ Authorization = "Bearer $tokenAdmin" }
$b  = @{ permiso = "exportar-reportes" } | ConvertTo-Json

# RF-GP-01: un Estándar crea una solicitud; nace Pendiente (id 1)
Probar $base Post $h $b

# RF-GP-02: el Admin ve las pendientes de todos; el Estándar solo las suyas y recibe 403 en /pendientes
Probar "$base/pendientes" Get $ha $null
Probar "$base/mias" Get $h $null
Probar "$base/pendientes" Get $h $null

# RF-GP-03 y RF-GP-05: aprobar deja aprobador y fecha, y el permiso queda aplicado
Probar "$base/1/aprobar" Post $ha $null
Probar "$base/tengo?permiso=exportar-reportes" Get $h $null

# RF-GP-06: aprobar dos veces se rechaza y el estado no cambia
Probar "$base/1/aprobar" Post $ha $null

# RF-GP-04: rechazar sin motivo no se completa; con motivo sí (solicitud 2)
Probar $base Post $h $b
$sin = @{ motivo = "" } | ConvertTo-Json
$con = @{ motivo = "No corresponde a su rol" } | ConvertTo-Json
Probar "$base/2/rechazar" Post $ha $sin
Probar "$base/2/rechazar" Post $ha $con

# RF-GP-06: pasar de Rechazada a Aplicada se rechaza
Probar "$base/2/aprobar" Post $ha $null

# RF-GP-07: el Administrador crea su propia solicitud (id 3) e intenta aprobarla: se rechaza
Probar $base Post $ha $b
Probar "$base/3/aprobar" Post $ha $null

# RD-07: entrada inválida produce un rechazo controlado
$vacio = @{ permiso = "" } | ConvertTo-Json
Probar $base Post $h $vacio
```

RD-09: detener el servidor con `Ctrl + C`, volver a arrancarlo y repetir `Probar "$base/mias" Get $h $null`. Las solicitudes siguen ahí.

## Pruebas

Un solo comando, desde la raíz de un clon limpio, ejecuta toda la suite (45 pruebas):

```powershell
dotnet test Core.Permisos.Tests
```

Las pruebas ejercitan `Core.Permisos` directamente, con SQLite en memoria y sin levantar la aplicación (RD-12). Cada prueba crea su propia base, así que son independientes y pasan en cualquier orden. Cada nombre lleva el ID del requisito que verifica. Cubren las transiciones válidas (aprobar y rechazar, con aprobador, fecha y motivo) y todas las prohibidas contra la tabla de transiciones, además de rechazar sin motivo y aprobar la solicitud propia, verificando siempre que el estado no cambia.

## Defectos (Práctica 2, variante A)

Cada defecto se planta en `Core.Permisos/SolicitudPermisoService.cs` sobre la etiqueta `practica-2` (etiqueta `practica-2-defecto-N`). Las líneas indicadas son las del punto etiquetado `practica-2`. Comando para verificar: `dotnet test Core.Permisos.Tests`.

- **Defecto 1** (RF-GP-06, línea 103, condición de `AprobarAsync`): lo detecta `RF_GP_06_Aprobar_DosVeces_SeRechazaYElEstadoNoCambia`, que comprueba que aprobar dos veces la misma solicitud se rechaza y el estado no cambia.
- **Defecto 2** (RF-GP-06, línea 18, transición Rechazada → Aplicada): lo detecta `RF_GP_06_Transicionar_FueraDeLaTabla_SeRechazaYElEstadoNoCambia` con el caso Rechazada → Aplicada, que comprueba que una transición fuera de la tabla se rechaza y el estado no cambia.
- **Defecto 3** (RF-GP-07, línea 92, verificación de solicitud propia): lo detecta `RF_GP_07_Aprobar_LaPropiaSolicitud_SeRechazaYSigueEnPendiente`, que comprueba que un Administrador no puede aprobar su propia solicitud y esta sigue Pendiente.
- **Defecto 4** (RF-GP-02, línea 82, consulta del usuario): lo detecta `RF_GP_02_ListarDeUsuario_NoMuestraSolicitudesDeOtrosUsuarios`, que comprueba que un usuario solo ve sus solicitudes y no las de otros.
- **Defecto 5** (RF-GP-06, línea 16, transición Aprobada → Pendiente): lo detecta `RF_GP_06_Transicionar_FueraDeLaTabla_SeRechazaYElEstadoNoCambia` con el caso Aprobada → Pendiente, que comprueba que la solicitud no puede volver a Pendiente.
- **Defecto 6** (RF-GP-04, línea 143, guardado del motivo): lo detecta `RF_GP_04_Rechazar_GuardaElMotivoEscrito`, que comprueba que al rechazar queda guardado el motivo escrito.

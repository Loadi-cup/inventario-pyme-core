# Inventario Pyme Core

Sistema de gestión de inventario para pequeños negocios. Proyecto de Programación III — ITLA 2026-C-3.

- **Autor:** Loadi Romero
- **Lenguaje/Framework:** C# / ASP.NET Core 10
- **Base de datos:** SQLite (archivo `inventario.db`, generado automáticamente)

## Estructura del proyecto

- `Core.ControlAcceso` — Pieza 1 del Core: usuarios, sesión, roles, recuperación de contraseña.
- `Core.Negocio` — Módulo de negocio: productos, proveedores, órdenes de compra y su máquina de estados.
- `Api` — ASP.NET Core Web API que expone los endpoints de ambas piezas.
- `docs/maquina-de-estados.md` — Tabla de transiciones de la máquina de estados del negocio.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Una cuenta de Gmail con verificación en dos pasos activada, para generar una contraseña de aplicación (solo si se va a probar el envío real de correo).

## Variables de entorno necesarias

La aplicación no arranca si faltan. Se configuran como variables de entorno de usuario en Windows (PowerShell):

```powershell
[System.Environment]::SetEnvironmentVariable('Jwt__Clave', '<una cadena aleatoria de 32+ caracteres>', 'User')
[System.Environment]::SetEnvironmentVariable('Smtp__Usuario', '<tu correo de Gmail>', 'User')
[System.Environment]::SetEnvironmentVariable('Smtp__ContrasenaApp', '<contraseña de aplicación de Gmail, sin espacios>', 'User')
```

| Variable | Para qué sirve |
|---|---|
| `Jwt__Clave` | Firma los tokens de sesión (JWT). Cualquier cadena larga y aleatoria. |
| `Smtp__Usuario` | Cuenta de Gmail desde la que se envían los correos de activación y recuperación. |
| `Smtp__ContrasenaApp` | Contraseña de aplicación generada en https://myaccount.google.com/apppasswords (no la contraseña normal de la cuenta). |

Después de configurarlas, cierra y vuelve a abrir la terminal/VS Code para que se carguen.

## Cómo ejecutar el proyecto

```powershell
git clone https://github.com/Loadi-cup/inventario-pyme-core.git
cd inventario-pyme-core
dotnet ef database update --project Core.ControlAcceso --startup-project Api
dotnet ef database update --project Core.Negocio --startup-project Api --context NegocioDbContext
dotnet run --project Api
```

El servidor queda escuchando en `http://localhost:5118`.

## Cómo provocar cada criterio de aceptación

Todas las pruebas se hacen con `Invoke-RestMethod` en PowerShell, o con Postman/Swagger si se prefiere.

### Registro y activación (RF-CA-01, 02, 14, 15, 16, 17)

```powershell
# Registro
$body = @{ nombre = "Prueba"; correo = "correo@ejemplo.com"; contrasena = "Clave1234" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/registro" -Method Post -Body $body -ContentType "application/json"

# Repetir el mismo registro: debe rechazarse por correo duplicado (RF-CA-01)
# Registrar con "abc" como contraseña: debe rechazarse por politica (RF-CA-14)

# Procesar la cola para enviar el correo de activacion
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/enviar-correos-pendientes" -Method Post

# Intentar login antes de activar: debe rechazarse (RF-CA-15)
$bodyLogin = @{ correo = "correo@ejemplo.com"; contrasena = "Clave1234" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $bodyLogin -ContentType "application/json"

# Activar con el codigo recibido por correo (o visto en /api/admin/diagnostico-correos en desarrollo)
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/activar?codigo=<codigo-del-correo>" -Method Get

# Usar el mismo enlace una segunda vez: debe rechazarse (RF-CA-16)

# Reenviar activacion con correo inexistente y uno existente: misma respuesta (RF-CA-17)
$bodyReenvio = @{ correo = "correo@ejemplo.com" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/reenviar-activacion" -Method Post -Body $bodyReenvio -ContentType "application/json"
```

### Sesión (RF-CA-03, 07, 18, 19)

```powershell
# Login correcto (cuenta ya activada)
$token = (Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $bodyLogin -ContentType "application/json").token

# Consultar quien soy (requiere token)
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/yo" -Method Get -Headers @{ Authorization = "Bearer $token" }

# Cerrar sesion
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/logout" -Method Post -Headers @{ Authorization = "Bearer $token" }

# Volver a usar el mismo token tras el logout: debe rechazarse (RF-CA-18)
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/yo" -Method Get -Headers @{ Authorization = "Bearer $token" }

# Fallar el login 5 veces seguidas con contrasena incorrecta, y comprobar que el 6to intento
# (aun con la contrasena correcta) se rechaza durante 15 minutos (RF-CA-19)
```

### Roles y administración (RF-CA-04, 05, 06, 08, 20, 21)

El primer usuario Administrador se crea manualmente en la base de datos (no hay un endpoint de "primer admin" en esta entrega). Con un cliente SQLite, actualizar la columna `Rol` del usuario deseado a `1` (Administrador) en la tabla `Usuarios`.

```powershell
# Login como Administrador
$tokenAdmin = (Invoke-RestMethod -Uri "http://localhost:5118/api/auth/login" -Method Post -Body $bodyAdminLogin -ContentType "application/json").token

# Listar usuarios (RF-CA-21)
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios" -Method Get -Headers @{ Authorization = "Bearer $tokenAdmin" }

# Un Estandar intentando listar usuarios: debe rechazarse, incluso llamando el endpoint directamente (RF-CA-05, 06, RD-06)
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios" -Method Get -Headers @{ Authorization = "Bearer $token" }

# Cambiar rol de otro usuario (RF-CA-08)
$bodyRol = @{ nuevoRol = "Administrador" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/rol" -Method Put -Body $bodyRol -ContentType "application/json" -Headers @{ Authorization = "Bearer $tokenAdmin" }

# Intentar cambiar el propio rol: debe rechazarse

# Desactivar un usuario y probar que su sesion deja de funcionar (RF-CA-20)
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/desactivar" -Method Post -Headers @{ Authorization = "Bearer $tokenAdmin" }

# Intentar desactivarse a si mismo: debe rechazarse
```

### Contraseñas (RF-CA-09 a 13, 22)

```powershell
# Solicitar recuperacion (misma respuesta exista o no el correo) (RF-CA-09)
$bodyRecuperar = @{ correo = "correo@ejemplo.com" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/recuperar" -Method Post -Body $bodyRecuperar -ContentType "application/json"
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/enviar-correos-pendientes" -Method Post

# Restablecer con el codigo recibido (RF-CA-10, 11)
$bodyRestablecer = @{ codigo = "<codigo-del-correo>"; nuevaContrasena = "ClaveNueva123" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/restablecer" -Method Post -Body $bodyRestablecer -ContentType "application/json"

# Probar el token viejo tras el restablecimiento: debe rechazarse (RF-CA-12)

# Restablecimiento forzado por Administrador (RF-CA-13)
Invoke-RestMethod -Uri "http://localhost:5118/api/admin/usuarios/2/forzar-restablecimiento" -Method Post -Headers @{ Authorization = "Bearer $tokenAdmin" }

# Cambiar contrasena con sesion, indicando la actual (RF-CA-22)
$bodyCambiar = @{ contrasenaActual = "ClaveNueva123"; contrasenaNueva = "OtraClave456" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5118/api/auth/cambiar-contrasena" -Method Post -Body $bodyCambiar -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }
```

### Correo por cola (RF-NOT-08, 09, 12, 13)

El endpoint `POST /api/admin/enviar-correos-pendientes` procesa la cola de forma independiente a la operación que generó el correo. Ejecutarlo dos veces seguidas no reenvía los correos ya marcados como `Enviado`. Las credenciales SMTP se leen de `Smtp__Usuario` y `Smtp__ContrasenaApp` (variables de entorno), nunca están en el código ni en el repositorio.

**Nota de entorno:** en la máquina de desarrollo usada para esta entrega, Google bloqueó temporalmente el acceso SMTP con un aviso de verificación de seguridad en la cuenta, impidiendo confirmar el envío real al cierre del plazo. El código usa MailKit con STARTTLS en el puerto 587 y maneja reintentos (máximo 3) y estados (`Pendiente`, `Enviado`, `Fallido`) según el requisito. Puede probarse en un entorno sin ese bloqueo.

### Máquina de estados del negocio (RF-NEG-03, 04, 05)

Los estados y transiciones están declarados en `Core.Negocio/Modelos/EstadoOrdenCompra.cs` y `Core.Negocio/OrdenDeCompraService.cs`, respectivamente. La tabla completa de transiciones está en `docs/maquina-de-estados.md`.

## Diagnóstico (solo para desarrollo)

`GET /api/admin/diagnostico-correos` — lista el contenido de la cola de correos, útil para ver el código de activación/recuperación sin depender del envío real durante el desarrollo. No está pensado para producción.
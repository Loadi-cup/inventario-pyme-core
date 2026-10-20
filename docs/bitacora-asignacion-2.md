# Bitácora de sesión con el agente — Asignación 2

## Tareas que le delegué

1. **Entender la asignación.** Le pasé el PDF y me lo resumió: tres entregas (pieza, suite de pruebas, bitácora), la tabla de transiciones y un plan en 8 pasos.
2. **Diseñar y construir `Core.Permisos`.** Me dio el modelo (`SolicitudPermiso`, `PermisoOtorgado`), el `PermisosDbContext` y `SolicitudPermisoService`, con todas las transiciones en un único diccionario (RD-04).
3. **Endpoints.** Me dio `PermisosController`, con `[Authorize(Roles = "Administrador")]` en las operaciones de resolución (RD-06), y el nuevo `Program.cs`.
4. **Suite de pruebas.** Me propuso 8 casos con xUnit y SQLite en memoria, con preparar/actuar/verificar. Corrí `dotnet test Core.Permisos.Tests`: 8 correctas, 0 con errores.
5. **Preparar usuarios.** Como el correo de activación no salía (Google bloqueaba la contraseña de aplicación), me guió para activar usuarios y crear un Administrador con SQL sobre `inventario.db`.
6. **Recorrido, README y commits.** Me dio el recorrido de pruebas manuales, la sección del README y los mensajes de commit citando cada requisito.

## Errores del agente

### Error 1 — Fecha equivocada
- **Qué pasó:** dijo que hoy era lunes 19 de octubre, cuando era martes 20, el día del cierre.
- **Cómo lo detecté:** el propio agente se corrigió al revisar la fecha, y la marca de la migración (`2026-10-20`) lo confirmó.
- **Cómo se corrigió:** confirmé la fecha en el reloj de mi computadora y planifiqué entregar ese mismo día.

### Error 2 — Hora mal interpretada
- **Qué pasó:** dijo "son las 6:40 p. m. según tu migración". La marca (`18:40 +00:00`) está en UTC, y en República Dominicana (UTC-4) eran las 2:40 p. m.
- **Cómo lo detecté:** lo señaló el propio agente al repasar el dato, no yo.
- **Cómo se corrigió:** recalculé el tiempo disponible con la hora local. Además me recordó que el sistema guarda todas las fechas en UTC (RD-11).

### Error 3 — Fechas sin marca de zona horaria al leer
- **Qué pasó:** al crear la solicitud 1, `fechaCreacion` salió como `2026-10-20T19:26:20.5338676Z`, pero al consultarla después con `/mias` salió como `2026-10-20T19:26:20.5338676`, sin la Z. Es la misma fecha en UTC, pero sin la marca un cliente podría leerla como hora local. Pasa igual con `fechaResolucion`. Roza RD-11.
- **Cómo lo detecté:** comparando las salidas del recorrido con la de `/mias` después de reiniciar el servidor. Se repite siempre.
- **Cómo se corrigió:** no cambié el código a pocas horas del cierre. Lo dejo documentado como limitación: la fecha guardada es correcta (UTC), pero al leerla desde la base se pierde la marca. La corrección sería marcarla como UTC al leerla (`DateTime.SpecifyKind(..., DateTimeKind.Utc)`).

### Observación — Rechazos sin mensaje
Los 400 y 403 llegan con el cuerpo vacío, así que no se distingue qué regla se violó (por ejemplo, "ya aprobada" frente a "motivo vacío"). El estado no cambia en ningún caso, pero el mensaje sería útil para el cliente.

## Evidencia

### Resumen de las pruebas
```
Resumen de pruebas: total: 8, con errores: 0, correcto: 8, omitido: 0, duración: 9,6 s
Compilación realizado correctamente en 26,4s
```

### Aprobación de la solicitud 1 (RF-GP-03, RF-GP-05)
```
id                : 1
solicitanteId     : 3
permisoSolicitado : exportar-reportes
estado            : Aplicada
aprobadorId       : 4
fechaCreacion     : 2026-10-20T19:26:20.5338676
fechaResolucion   : 2026-10-20T19:26:21.4338559Z
motivoRechazo     :

permiso : exportar-reportes
tiene   : True
```

### Rechazo con motivo de la solicitud 2 (RF-GP-04)
```
id                : 2
solicitanteId     : 3
permisoSolicitado : exportar-reportes
estado            : Rechazada
aprobadorId       : 4
fechaCreacion     : 2026-10-20T19:26:21.5658709
fechaResolucion   : 2026-10-20T19:26:21.6134121Z
motivoRechazo     : No corresponde a su rol
```

### El Administrador no puede aprobar su propia solicitud (RF-GP-07)
Solicitud 3 creada por el Administrador (solicitanteId 4), que queda Pendiente. Al intentar aprobarla:
```
RECHAZADO: 400
```

### Persistencia tras reiniciar (RD-09)
`Probar "$base/mias" Get $h $null` después de detener y volver a arrancar el servidor:
```
id                : 1
estado            : Aplicada
aprobadorId       : 4
fechaCreacion     : 2026-10-20T19:26:20.5338676
fechaResolucion   : 2026-10-20T19:26:21.4338559

id                : 2
estado            : Rechazada
aprobadorId       : 4
motivoRechazo     : No corresponde a su rol
```

### Fechas con y sin Z (Error 3)
Al crear: `fechaCreacion : 2026-10-20T19:26:20.5338676Z`
Al leer con `/mias`: `fechaCreacion : 2026-10-20T19:26:20.5338676`
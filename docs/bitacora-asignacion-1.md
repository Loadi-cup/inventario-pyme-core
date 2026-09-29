# Bitácora de sesión con el agente — Asignación 1

**Curso:** Programación III · ITLA · 2026-C-3
**Estudiante:** Loadi-cup
**Repositorio:** https://github.com/Loadi-cup/inventario-pyme-core
**Pareja:** AsaelCodex (`sistema-de-reservas-de-espacios`)

## Contexto

Trabajé toda la asignación en una sola conversación con el agente (Claude). Le pedí
que me explicara los documentos del curso, me guiara con los comandos de Git y
redactara el contenido de los pull requests. Yo ejecuté los comandos en mi máquina
(PowerShell dentro de VS Code) y le pegué las salidas para que las interpretara.

## Tareas que le delegué

### 1. Entender qué pedía la asignación

- **Qué pedí:** que me explicara el PDF de la Asignación 1, porque no entendía qué
  tenía que hacer.
- **Qué devolvió:** un resumen de los cuatro entregables (historial limpio, tres PRs
  a mi pareja, revisión de los tres PRs de mi pareja y esta bitácora), más un orden
  sugerido de trabajo.
- **Resultado:** útil. Aclaró que la asignación evalúa el flujo de Git y no el código
  del Core.

### 2. Mi propia rama y pull request

- **Qué pedí:** los pasos para crear una rama, hacer un commit y abrir un PR en mi
  propio repositorio.
- **Qué devolvió:** la secuencia `git checkout -b`, editar el README, commit en
  imperativo, `git push` y PR desde GitHub. El mensaje de commit propuesto fue
  `Agregar descripcion del proyecto al README`.
- **Resultado:** PR #1 fusionado a `main`. Después repetí el proceso con la rama
  `feat/estructura-inicial` (PR #2) para subir la solución `.slnx` y los proyectos
  `Api` y `Core.ControlAcceso`.

### 3. Los tres PRs al repositorio de Asael

Le pedí que revisara el contenido del repo de mi pareja (le pegué la salida de
`git ls-files` y de `Get-Content`) y decidiera qué aportar en cada PR.

| PR | Aporte | Qué devolvió el agente |
|----|--------|------------------------|
| #3 | `.gitignore` | Detectó que ya cubría `bin/`, `obj/` y `.vs/`, pero no `.vscode/`, secretos (`.env`) ni bases de datos locales. Propuso agregar esas reglas sin borrar las existentes. |
| #4 | README de ejecución | Escribió las secciones "Requisitos" y "Cómo ejecutar el proyecto" **después** de que yo corrí `dotnet restore`, `build` y `run` en mi máquina y le pegué la salida (.NET 10, la consola muestra `Hello, World!`). |
| #5 | Plantilla de PR | Notó que la plantilla ya tenía las cuatro secciones pero vacías. Propuso agregar un comentario oculto de guía bajo cada título sin cambiar los títulos. |

Para cada PR el agente redactó la descripción con las cuatro secciones (Qué cambia,
Por qué, Cómo probarlo, Qué NO incluye). Antes de abrir el PR del README verifiqué las
instrucciones ejecutándolas yo mismo.

### 4. Explicación de comandos y errores de terminal

Le pegué las salidas de la terminal cuando algo no funcionaba y me explicó qué
significaban: el paginador de `git diff` (el `:` al final), el aviso
`\ No newline at end of file`, la línea `1 deletion(-)` que aparecía al agregar solo
texto, y los errores de PowerShell al pegar una URL como si fuera un comando.

## Errores del agente y cómo los corregí

### Error 1 — Botón "Fork" que en mi pantalla se llamaba "Tenedor"

- **Qué pasó:** el agente me indicó darle clic al botón **Fork**. En mi navegador
  GitHub estaba traducido al español y el botón aparece como **Tenedor**, así que no
  lo encontraba, y ni siquiera la captura que envié al inicio lo mostraba.
- **Cómo lo detecté:** no veía ningún botón con ese nombre y se lo dije. Le mandé una
  captura de la parte superior de la página del repo.
- **Cómo se corrigió:** con la captura, el agente identificó que "Tenedor" era el botón
  y me explicó que "Tenedor / Crear tenedor" equivale a "Fork / Create fork". Hice el
  fork y seguí el flujo.

### Error 2 — Patrones sin comillas invertidas en la descripción del PR #3

- **Qué pasó:** la primera descripción del PR del `.gitignore` incluía patrones como
  `*.nupkg` y `appsettings.*.local.json` escritos como texto normal. Markdown interpretó
  los asteriscos como formato: parte del texto salió en cursiva y
  `appsettings.*.local.json` se veía como `appsettings. .local.json`.
- **Cómo lo detecté:** al revisar el PR publicado en GitHub vi el texto deformado. El
  agente lo confirmó al ver mi captura y notó además que una regla se veía como
  `public/` en vez de `publish/`.
- **Cómo se corrigió:** edité el comentario del PR y puse cada patrón entre comillas
  invertidas. Después de la edición, la
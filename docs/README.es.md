---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · **Español** · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Un panel de inicio rápido para Windows: una cuadrícula de mosaicos con accesos directos, carpetas y pestañas, búsqueda difusa (fuzzy) integrada y un mini explorador con consola incorporada. Un solo EXE portable, sin instalador, .NET Framework 4.8 (WinForms).

Versión actual: **v0.6.0-beta** — descarga: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Registro de cambios](#changelog). Estado: **beta**.

## Características

- **Panel** — mosaicos de 1×1…6×6, pestañas ilimitadas (arrastrables, en varias filas), carpetas que se abren en su lugar o en ventanas emergentes, arrastrar y soltar desde el Explorador, cuadrícula personalizable (columnas/filas/transparencia), escalado de iconos.
- **Búsqueda** — busca en nombres, nombres de archivo, metadatos de programas (FileDescription / ProductName / CompanyName), rutas completas y descripciones del usuario; coincidencias difusas (fuzzy) con precisión ajustable y corrección de la distribución de teclado equivocada (`руддщ` → `hello`); los resultados se ordenan por calidad de coincidencia y los caracteres coincidentes se resaltan.
- **Mini explorador** — navegación por migas de pan, marcadores de carpetas/comandos/grupos, búsqueda de archivos (carpeta actual o todos los discos fijos) con un índice en segundo plano, y una consola `cmd.exe` incorporada con historial de comandos, comandos guardados y zoom de fuente con Ctrl+rueda.
- **Reglas por tipo de archivo** — iconos por extensión/máscara y asociaciones de «abrir con», importación/exportación.
- **Integración con el escritorio** — icono en la bandeja, inicio automático con Windows, atajo global, menús contextuales nativos del Explorador, ventana sin bordes con redimensión desde los bordes.
- **Primera ejecución y actualizaciones** — una ventana de bienvenida de un solo uso (nota de beta, elección de idioma, permiso de comprobación de actualizaciones, mosaicos de ejemplo) y una comprobación de actualizaciones contra GitHub Releases con una placa en la esquina cuando existe una versión más nueva.

## Primera ejecución y actualizaciones

- **Ventana de bienvenida** (solo en el primer arranque): una nota de agradecimiento, una advertencia de beta con enlace a [Issues](https://github.com/AlexNoVibe/Tilettes/issues), un mini-diagrama dibujado de «arrastra un acceso directo → aparece un mosaico», elección de idioma (banderas RU/EN), el permiso de comprobación de actualizaciones, direcciones de donación (clic para copiar) — y dos botones de salida: **Cerrar** a secas, o **Cerrar y crear mosaicos de ejemplo** (Bloc de notas, Calculadora, Explorador y Paint como mosaicos listos). Se puede volver a mostrar en cualquier momento mediante «Mostrar de nuevo la ventana de bienvenida» en los ajustes.
- **Comprobación de actualizaciones** — la aplicación consulta la API pública de GitHub Releases una vez cada N días (por defecto 3; la primera comprobación también ocurre N días después del primer arranque, no de inmediato). No se envía nada a ningún sitio y, con la comprobación desactivada en los ajustes, no se hace ninguna petición de red. Cuando existe una etiqueta más nueva, aparece una placa verde **⟳ Actualizar** junto al botón de ajustes y abre la página de [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). «Comprobar ahora» en los ajustes hace una comprobación manual sin importar el intervalo (informa del resultado en un cuadro de mensaje). La instalación automática es, por ahora, un marcador de posición (TODO).
- **Gancho de prueba** — inicia la aplicación con `WINPANEL_MOCK_UPDATE=0.6` para que se muestre la placa de Actualizar como si existiera una versión más nueva (sin usar la red).

## Referencia de ajustes

Todos los ajustes están en un único diálogo (botón ⚙ / menú de la bandeja) y se guardan en `settings.ini`.

### Arranque y ventana

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Tamaño de arranque (W × H) | 200–4000 | 900 × 800 | Tamaño del panel en cada inicio. El redimensionado durante una sesión no se guarda — solo la posición. |
| Posición de la ventana (X, Y) | −4000…4000 | 100, 100 | Posición en pantalla al iniciar. Se actualiza automáticamente al mover la ventana. |
| Atajo para mostrar la ventana | preajustes + personalizado | Ctrl+Q | Atajo global que muestra/activa el panel. Elige un preajuste (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) o escribe cualquier combinación `Mod+Tecla` (Ctrl/Alt/Shift/Win + una letra o dígito) directamente en el campo editable; la entrada que no se pueda interpretar se rechaza con una explicación. |
| Idioma | ru / en | ru | Idioma de la interfaz, se aplica al instante. |

### Cuadrícula y mosaicos

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Transparencia de la cuadrícula | 0–255 | 50 | Valor alfa de las líneas de la cuadrícula. 0 = invisible. Solo se dibuja cuando la cuadrícula (botón ▦) está activada. |
| Columnas de la cuadrícula | 1–100 | 16 | Celdas horizontales. Las posiciones de los mosaicos se ajustan a esta cuadrícula. |
| Filas de la cuadrícula | 1–100 | 16 | Celdas verticales. |
| Tamaño predeterminado del elemento | 1–6 | 2 | Tamaño de los mosaicos nuevos (1×1 … 6×6 celdas). |
| Escala de iconos (%) | 25–400 | 100 | Tamaño del icono dentro de un mosaico, en porcentaje del valor por defecto. |
| Permitir añadir iconos | activado/desactivado | activado | Modo de edición: arrastrar mosaicos, crear carpetas, soltar archivos. Cuando está desactivado, los mosaicos simplemente se inician al hacer clic. |
| Máscara y tema | Ninguna (oscura) / Clara / máscaras | Ninguna (oscura) | Tema clásico oscuro o claro, o una máscara decorativa (colores propios + borde de la ventana). |

### Carpetas

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Abrir carpetas en | Misma ventana / Ventana emergente | Misma ventana | Al hacer clic en una carpeta se navega dentro de la pestaña o se abre una ventana emergente por encima de todo. |
| seg de inactividad | 0–600 | 15 | Solo en el modo de misma ventana: vuelve hacia arriba automáticamente tras N segundos sin actividad de ratón/teclado. 0 = desactivado. |

### Fuentes

Una fila para cada grupo: **mosaicos**, **pestañas** e **interfaz**:

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| tamaño | 6–24 | 9 | Tamaño de la fuente del grupo. |
| muestra de color | cualquier color | vacío | Color de texto personalizado; vacío = el del tema. Se aplica a las etiquetas de los mosaicos, los títulos de las pestañas o todo el texto de la interfaz. |
| familia | cualquier fuente instalada | Segoe UI | Familia tipográfica del grupo. |

### Búsqueda

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Precisión fuzzy (0–3) | 0–3 | 2 | 0 = solo coincidencias de subcadena; 1–3 = coincidencia difusa cada vez más tolerante con las erratas. Los dígitos cuentan doble, así que los códigos numéricos coinciden de forma estricta. |
| Buscar en metadatos | activado/desactivado | activado | Nombre del archivo, destino del acceso directo, información de versión (descripción, producto, compañía). |
| Buscar en rutas completas | activado/desactivado | activado | El texto de la ruta completa, incluidas las carpetas contenedoras. |
| Buscar en descripciones | activado/desactivado | activado | Descripciones del usuario (clic derecho → Descripción…). |
| Fuente de búsqueda: campo | 7–30 | 9 | Tamaño de la fuente del campo de búsqueda. |
| Fuente de búsqueda: resultados | 7–30 | 9 | Tamaño de la fuente de las filas de resultados (la altura de la fila sigue a la fuente). |

### Actualizaciones

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Buscar actualizaciones automáticamente | activado/desactivado | activado | Consulta a GitHub Releases si hay una versión más nueva una vez cada N días. Nunca se ejecuta si está desmarcado — ni una sola petición de red. |
| Comprobar cada N días | 1–365 | 3 | Cada cuánto comprobar. La primera comprobación ocurre N días después del primer arranque. |
| Comprobar ahora | botón | — | Consulta a GitHub Releases de inmediato (la comprobación manual funciona incluso con la automática desactivada). |
| Instalar actualizaciones automáticamente | activado/desactivado | desactivado | **Marcador de posición (TODO)** — aún sin implementar. |
| ♥ Donar | botón | — | Lista emergente de carteras (un clic copia la dirección) más la sección de donación de GitHub. |
| Mostrar de nuevo la ventana de bienvenida | botón | — | Repite la ventana de bienvenida de la primera ejecución. |

### Mini explorador (claves INI)

| Clave | Rango | Por defecto | Descripción |
|---|---|---|---|
| Ctrl+clic en una carpeta abre el mini explorador | activado/desactivado | activado | El atajo Ctrl+clic en los mosaicos de carpeta. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | auto | Geometría de la ventana, se recuerda al cerrar. |
| `MiniExplorerBookmarks` | activado/desactivado | activado | Visibilidad del panel lateral de marcadores. |
| `MiniExplorerTopBar` | activado/desactivado | activado | Visibilidad de la barra superior de marcadores. |
| `MiniExplorerConsole` | 15–85 | 40 | Altura de la consola como porcentaje de la ventana. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Tamaño de la fuente de la consola ×10 (85 = 8,5 pt), se cambia con Ctrl+rueda. |

### Inicio automático y bandeja

| Ajuste | Rango | Por defecto | Descripción |
|---|---|---|---|
| Inicio automático con Windows | activado/desactivado | desactivado | Escribe en `HKCU\...\Run` («Tilettes»). |
| Tras el inicio automático, ir a la bandeja | activado/desactivado | desactivado | Añade `--minimized`: el panel arranca oculto en la bandeja. |
| Minimizar en lugar de cerrar | activado/desactivado | activado | ✕ / Alt+F4 oculta en la bandeja (o minimiza) en lugar de salir. La salida está en el menú de la bandeja. |
| Mantener siempre el icono de la bandeja | activado/desactivado | activado | El icono de la bandeja es visible en todo momento. |
| Recordar la pestaña activa | activado/desactivado | activado | Restaura la última pestaña activa al iniciar. |

### Copia de seguridad y sincronización del menú Inicio

- **Copia de seguridad ahora** — un zip de copia completa en `autoBackup\` (ajustes, mosaicos, iconos, marcadores, historial de búsqueda y el exe); programada mediante «Copia de seguridad cada N días» (0 = desactivado), se crea ~3 minutos después del arranque cuando toca.
- **Guardar copia de seguridad (zip)** — el mismo archivo en una ruta elegida por el usuario.
- **Restaurar archivo…** — espera un zip creado por el propio Tilettes; los archivos se descomprimen en la carpeta de trabajo y `Tilettes.exe` nunca se reemplaza.
- **Sincronizar el menú Inicio** ahora / cada N horas (0 = desactivado) — reconstruye la pestaña espejo del menú Inicio.

## Atajos de teclado y comandos

### Panel principal

| Teclas / acción | Resultado |
|---|---|
| Atajo global (por defecto Ctrl+Q) | Mostrar / activar el panel. |
| Escribe cualquier texto, o Ctrl+F | Abrir la búsqueda del panel. |
| ↓ | Saltar a la lista de resultados. |
| Enter | Abrir el resultado seleccionado (carpeta → navegar, archivo → iniciar). |
| Esc | Cerrar la búsqueda. |
| Clic en un mosaico | Inicia el elemento; una carpeta navega (o ventana emergente, según los ajustes). |
| Ctrl+clic en un mosaico de carpeta | Abre el mini explorador (si está activado). |
| Arrastrar un mosaico (modo de edición) | Moverlo; soltarlo sobre una carpeta lo mueve dentro. |
| Soltar archivos sobre el panel (modo de edición) | Se añaden como mosaicos (soltar sobre una carpeta los añade dentro). |
| Clic derecho en un mosaico | Menú nativo del Explorador más: Descripción…, Tamaño 1×1–6×6, Renombrar, Cambiar icono, Quitar, Sacar de la carpeta, Abrir en el mini explorador (carpetas). |
| Clic derecho en una pestaña | Eliminar (la última pestaña está protegida), Renombrar, Alternar libre/cuadrícula. |
| Arrastrar una pestaña | Reordenarla dentro de su fila o moverla a otra fila. |
| Clic derecho en una zona vacía del panel | Crear carpeta, Ajustes. |
| Botones ▦ / ✅ / ⚙ | Visibilidad de la cuadrícula, modo de edición, ajustes. |

### Mini explorador

| Teclas / acción | Resultado |
|---|---|
| Ctrl+L / F4 / Editar | Editar la ruta. |
| F5 | Actualizar la carpeta. |
| Backspace | Subir un nivel. |
| Alt+← / Alt+→ | Atrás / adelante. |
| Enter / doble clic | Abrir (una carpeta navega, un archivo se inicia). |
| Esc | Salir de la búsqueda → cancelar la edición de la ruta → cerrar la ventana. |
| Escribir en la lista de archivos | Inicia una búsqueda en el campo de búsqueda. |
| Abajo / Arriba (en la búsqueda) | Moverse por los resultados. |
| Ctrl+rueda del ratón | Tamaño de la fuente de la consola (se guarda). |
| Arrastrar el divisor | Altura de la consola (se guarda). |
| Botones ≡ / ☰ | Alternar el panel lateral de marcadores / la barra superior de marcadores. |
| Clic derecho en un archivo | Abrir, Mostrar en el Explorador, Copiar ruta. |
| Clic derecho en una carpeta | Abrir, Añadir a marcadores, Abrir en el Explorador. |
| Clic derecho en una zona vacía | Actualizar, Copiar ruta de la carpeta, Abrir en el Explorador, Añadir la carpeta actual a marcadores, Abrir aquí una ventana de consola. |
| Clic derecho en un marcador | Editar comando… (solo comandos), Renombrar…, Subir / Bajar, Quitar. |

### Consola

Cualquier comando de una sola línea de `cmd.exe` se puede escribir y ejecutar (Enter o el botón **Ejecutar**). El directorio de trabajo se resincroniza con la carpeta actual antes de cada comando. **+ Guardar** almacena el comando escrito como marcador (opcionalmente dentro de un grupo); los comandos guardados se ejecutan con un clic. Botones: **Limpiar** (borrar la salida), **Reiniciar** (un nuevo cmd.exe), **Nueva ventana** (una ventana de consola real en la carpeta actual). El historial de comandos está disponible con ↑ / ↓ durante la sesión.

## Limitaciones

- **Solo Windows + .NET Framework 4.8** (GDI/WinForms). Sin reconocimiento de DPI por monitor — la interfaz puede verse borrosa en pantallas con mucho escalado.
- El ámbito de búsqueda **«Todo»** indexa **solo discos locales fijos** (sin unidades USB ni de red), con un tope de **200 000 elementos por disco**; la indexación se ejecuta en segundo plano, así que los resultados crecen mientras trabaja («indexando: N» en la línea de estado).
- La **búsqueda del panel** muestra las mejores **200** coincidencias; la **búsqueda del mini explorador** devuelve hasta **400**; una lista de archivos muestra como máximo **800** entradas por directorio.
- **La consola es solo `cmd.exe`**: comandos de una sola línea; los programas interactivos/TUI (editores, paginadores con entrada por teclado) no funcionan correctamente; el búfer de salida se limpia automáticamente tras ~150 000 caracteres; la codificación sigue la página de código OEM del sistema (por ejemplo CP866).
- El **atajo global** es una letra/dígito más modificadores; el registro falla con un globo de notificación si otro programa ya lo está usando.
- **El tamaño del panel se restablece al tamaño de arranque en cada inicio** — solo se recuerda la posición (es deliberado).
- **Arrastrar y soltar y mover mosaicos requieren el modo de edición** («Permitir añadir iconos» / botón ✅).
- Los mosaicos de carpeta previsualizan como máximo **9** iconos hijos; la ventana emergente de carpeta muestra como máximo **4** columnas por fila.
- Los elementos `.lnk`/`.ico` añadidos al panel se **copian a `ico\`** para que sobrevivan si se mueven los originales.
- **Restaurar archivo** acepta solo zips creados por Tilettes («Copia de seguridad ahora» / «Guardar copia de seguridad (zip)»).
- Las esquinas redondeadas de la ventana se retiran temporalmente durante el redimensionado (técnica para evitar el parpadeo) y se restauran al soltar.
- La corrección de distribución cubre el par EN↔RU QWERTY; las demás distribuciones pasan sin cambios.
- **Instancia única**: lanzar una segunda copia simplemente muestra la ventana ya abierta.
- La salida automática de carpetas solo funciona en el modo «Misma ventana» y solo mientras se está dentro de una carpeta.

## Compilar

Requiere cualquier Windows con .NET Framework 4.x (el compilador viene incluido con el sistema operativo):

```
build.bat
```

o directamente:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

Las releases se crean automáticamente con GitHub Actions en cada etiqueta `v*`: el workflow compila las variantes del exe con la misma llamada a csc y adjunta a la release archivos exe simples (AnyCPU universal + x86 + x64 — sin zip); los archivos automáticos de código fuente (Source code) de GitHub también están en la release. También puedes compilar el exe tú mismo con `build.bat`.

## Archivos de datos (se crean junto al EXE)

| Archivo | Propósito |
|---|---|
| `settings.ini` | Todos los ajustes |
| `records.xml` | Pestañas, carpetas, accesos directos, descripciones |
| `bookmarks.xml` | Marcadores del mini explorador |
| `filetypes.xml` | Reglas por tipo de archivo |
| `ico\` | Copias de los elementos .lnk/.ico y de iconos personalizados |

## Estructura del proyecto (`src/`)

| Archivo | Propósito |
|---|---|
| `Program.cs` | Ventana principal: pestañas, mosaicos, búsqueda del panel, ventanas emergentes de carpetas, instancia única |
| `MiniExplorerForm.cs` | Mini explorador: navegación, marcadores, consola incorporada |
| `SearchCore.cs` | Indexación de discos, prefiltro por máscara de bits, puntuación difusa |
| `PanelSearch.cs` | Metadatos de búsqueda de los elementos guardados |
| `Settings.cs` / `SettingsForm.cs` | Modelo de ajustes y diálogo |
| `FileTypes.cs` / `FileTypesForm.cs` | Reglas por tipo de archivo y sus editores |
| `bookmarks.cs` | Almacenamiento de marcadores |
| `loc.cs` + `lang_*.cs` | Localización: EN fuente, RU en línea, ES/PT/DE/FR/IT/PL/ZH/JA tablas |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Comprobación de actualizaciones (GitHub Releases) y ventana de bienvenida de la primera ejecución |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Iconos, menús nativos, E/S de INI, E/S de registros, inicio automático/instancia única/carteras |

## Licencia

[MIT](LICENSE) — libre de usar, modificar y distribuir.

<a name="donate"></a>
## Donar

Si Tilettes te resulta útil, puedes apoyar el desarrollo con criptomonedas. Las redes compatibles con EVM comparten una misma dirección — envía por la red que te resulte práctica:

<a name="donate-evm"></a>
### EVM — Ethereum · Polygon · Base · Monad · HyperEVM

```
0xf84897FA0b74083c16865315A5b148f4d92e6C2a
```

<a name="donate-btc"></a>
### Bitcoin (BTC)

```
bc1qu9cf5uqc5wxqwde8mk378xwdlnjatvmhxhvat5
```

<a name="donate-sol"></a>
### Solana (SOL)

```
7ffCFnJBNVaF268FsZGKBPEWe3UNrWbasgt3aidiCw68
```

<a name="donate-sui"></a>
### Sui (SUI)

```
0x3ca194b355bb00a1f5f646786407ebbcdaee361c6f56fb92f8df9abd73b0c3b1
```

Otras formas de ayudar: informa de errores e ideas en [Issues](https://github.com/AlexNoVibe/Tilettes/issues), dale una estrella al repositorio, corre la voz.

<a name="changelog"></a>
## Registro de cambios

### v0.6.0-beta (2026-10-01)

- Rendimiento: arranque más rápido (renderizado diferido de las pestañas — solo se construye la pestaña activa), caché de iconos persistente (los iconos del shell se extraen una sola vez por vida de cada mosaico), metadatos de búsqueda recogidos una sola vez al añadir un elemento; el campo de búsqueda del mini explorador está desactivado (el código se conserva para reactivarlo).
- Los recursos compartidos de red nunca bloquean el hilo de la interfaz: los iconos y los destinos de los .lnk en rutas de red se resuelven en segundo plano.
- Una máscara activa ahora gobierna la paleta clara/oscura en todas partes (elegir Mint ya no deja ventanas oscuras); mint es el tema por defecto y todas las fuentes pasan a 14.
- Primera ejecución: en monitores con un área de trabajo inferior a 900 px la ventana y la cuadrícula predeterminadas se reducen proporcionalmente para caber en pantalla.
- Refuerzo: nombre seguro (strong name), VERSIONINFO, manifiesto explícito, la captura de la tecla Win es opcional — 0 detecciones en VirusTotal.
- Los recursos de la release son archivos exe simples por CPU (AnyCPU/x86/x64) en lugar de un zip; las notas de la release provienen de CHANGELOG.md.

### v0.5 (2026-09-30)

- Ventana de bienvenida de primera ejecución (una vez por carpeta de datos): agradecimiento, aviso de beta + enlace a issues, mini-diagrama dibujado, elección de idioma (banderas RU/EN), permiso de comprobación de actualizaciones, direcciones de donación (clic para copiar); salida mediante «Cerrar» o «Cerrar y crear mosaicos de ejemplo» (Bloc de notas / Calculadora / Explorador / Paint como mosaicos listos). Se puede volver a mostrar desde los ajustes.
- Comprobación de actualizaciones: la aplicación consulta la API pública de GitHub Releases por la última etiqueta cada N días (por defecto 3; la primera comprobación también ocurre N días después de la instalación) — estrictamente solo si el usuario lo permitió, cero peticiones de red en caso contrario. Cuando existe una versión más nueva aparece una placa verde «Actualizar» junto al botón de ajustes y abre la página de releases. Botón manual «Comprobar ahora» en los ajustes (informa del resultado en un cuadro de mensaje). La instalación automática es un marcador de posición (TODO). Gancho de prueba para la placa: `WINPANEL_MOCK_UPDATE=0.6`.
- Ajustes: nueva sección «Actualizaciones» (interruptor de comprobación, intervalo en días, botón de comprobación inmediata, marcador de posición de instalación automática, línea de donación con un menú emergente de carteras — un clic copia la dirección — y un botón para volver a mostrar la ventana de bienvenida). Campo de atajo editable: se puede escribir cualquier combinación de Ctrl/Alt/Shift/Win + letra/dígito (validada); el valor por defecto pasa a Ctrl+Q.
- Interfaz del programa localizada en **10 idiomas**: inglés (fuente), ruso, español, portugués, alemán, francés, italiano, polaco, chino (simplificado) y japonés. El idioma se elige en los ajustes o mediante las banderas dibujadas de la ventana de bienvenida; un idioma nuevo es un archivo de tabla + una línea (ver loc.cs).
- Lista real de carteras de donación agrupadas por cadena: las redes EVM (ETH · Polygon · Base · Monad · HyperEVM) comparten una dirección; más Bitcoin, Solana y Sui.
- La versión es ahora una única constante (`AppInfo.AppVersion`); el tooltip de la bandeja y la ventana de bienvenida la muestran.
- Infraestructura de GitHub: licencia MIT, FUNDING.yml (enlaces de patrocinio a las anclas de las carteras), README dividido en archivos por idioma (`README.md` EN + 9 traducciones) para facilitar su ampliación, workflow de GitHub Actions (release con un zip portátil en las etiquetas `v*`), página de destino en docs/ para GitHub Pages. Todos los mensajes de commit del historial están en inglés.

### v0.4 (2026-09-29)

- Rebranding: Tilettes / «Плиточки», nuevo icono (recurso del exe + icono de bandeja dibujado por código).
- Función de copia de seguridad completada: «Guardar copia de seguridad (zip)» empaqueta ajustes, mosaicos, iconos, marcadores, historial de búsqueda y el exe; «Restaurar archivo» descomprime en la carpeta de trabajo los zips creados por la aplicación sin reemplazar Tilettes.exe; copias completas programadas en autoBackup\.
- Reestructuración del diálogo de ajustes: redimensionable en vertical mediante un tirador inferior, tooltips en cada elemento, campos X/Y etiquetados para el tamaño de arranque y la posición de la ventana, columnas/filas de la cuadrícula en una sola fila, y un cuadro combinado de máscaras que sustituye a la casilla duplicada del tema claro.
- Correcciones de la pestaña espejo del menú Inicio (el contenido de las carpetas ya no se amontona; diseño automático simplificado).
- Correcciones de diseño para fuentes 14–20 (pestañas, altura del estado de la búsqueda, diálogos, barras del mini explorador, escalón de la esquina de la ventana).

### v0.3 (2026-09-28)

- Sincronización del menú Inicio (pestaña espejo, programada), escalonados de reducción de los interruptores de búsqueda, franja de ajustes rápidos de búsqueda, modo de edición con multiselección, menús «Mover a la pestaña», captura de la tecla Win, navegación en las ventanas emergentes de carpetas, resaltado de rutas en los resultados de búsqueda.

### v0.1 – v0.2 (2026-09-27)

- Primeras compilaciones del panel lanzador: mosaicos, pestañas, carpetas, búsqueda del panel, mini explorador con consola, reglas por tipo de archivo, bandeja/inicio automático/atajo global, personalización de la cuadrícula.

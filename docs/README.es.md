# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · **Español** · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Un panel de inicio rápido para Windows: una cuadrícula de mosaicos con accesos directos, carpetas y pestañas, búsqueda aproximada integrada y un mini explorador con consola incorporada. Un solo EXE portable, sin instalador, .NET Framework 4.8 (WinForms).

Versión actual: **v0.5** — descarga: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog (English)](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog). Estado: **beta**.

## Características

- Mosaicos de 1×1…6×6, pestañas ilimitadas arrastrables, carpetas dentro de la pestaña o en ventanas emergentes, arrastrar y soltar desde el Explorador, cuadrícula personalizada, escalado de iconos.
- Búsqueda aproximada por nombres, metadatos, rutas y descripciones, con corrección de teclado en distinta distribución.
- Mini explorador con rutas de navegación, marcadores, búsqueda de archivos y consola cmd.exe incorporada.
- Iconos por tipo de archivo y reglas de "abrir con", importación/exportación.
- Icono en la bandeja, inicio automático, atajo global, menús nativos del Explorador, ventana sin bordes redimensionable.
- Ventana de bienvenida de un solo uso y comprobación de actualizaciones vía GitHub Releases con una placa en la esquina.

## Primera ejecución y actualizaciones

- La ventana de bienvenida se muestra una sola vez (nota de beta, elección de idioma, permiso de actualización, mosaicos de ejemplo) y puede volver a mostrarse desde los ajustes.
- La comprobación de actualizaciones se ejecuta cada N días (3 por defecto) estrictamente con el consentimiento del usuario; una placa verde de «Actualizar» aparece junto al botón de ajustes cuando existe una versión más nueva; «Comprobar ahora» es una comprobación manual.

## Donar

Si Tilettes te resulta útil, puedes apoyar el desarrollo con criptomonedas. Las redes compatibles con EVM comparten una misma dirección:

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

## Compilar

```
build.bat
```

Requiere cualquier Windows con .NET Framework 4.x — el compilador viene incluido con el sistema operativo. Las versiones se crean automáticamente con GitHub Actions en cada etiqueta `v*` y contienen solo el archivo de fuentes (el workflow también verifica la compilación); compila el exe tú mismo con `build.bat`.

Documentación completa: [**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)

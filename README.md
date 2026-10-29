# MOON — lector de libros, cómics y manga

Lector de escritorio para **Windows 10/11** hecho en **C# / .NET 10 / WPF**: biblioteca (carpeta propia y discos externos), libros EPUB/MOBI/AZW3/FB2, cómics CBZ/CBR/CB7, manga en PDF, subrayado y notas, diccionario inglés → español con vocabulario para Anki, y lectura en voz alta con una voz clonada (Chatterbox, en el propio PC). Sin cuentas ni internet.

## Estado (2026-10-29) — versión 0.2.0

| Hito | Qué | Estado |
|---|---|---|
| L1 | Esqueleto, temas, instancia única | ✅ |
| L3 | Biblioteca: añadir (copia), enlazar carpetas y discos externos | ✅ (falta Open Library) |
| L5 | Lector de libros (foliate-js en WebView2) | ✅ |
| L2 + L4 | Lector de cómics y manga (CBZ, CBR, CB7, PDF como imágenes) | ✅ |
| L5b | Lectura en voz alta (Chatterbox o voz de Windows) | ✅ oída funcionando |
| L6 | Subrayado, notas, marcadores, zonas en cómics, exportar | ✅ |
| L7 | Diccionario inglés → español sin internet + vocabulario (Anki) | ✅ |
| L8 | «Leer texto» en cómics: OCR de un bocadillo → diccionario y vocabulario | ✅ |
| L9, L11 | «¿Qué es esto?» y traducción | ❌ descartados por la persona |
| L10 | PDF «como documento» (texto, subrayado, notas) | ✅ |
| L12 | 1000+ libros, atajos de teclado, instalador | ✅ |
| L13–L14 | IA (opcionales) | sin empezar |

Instalador: ver `installer/README.md` → `dist/MOON-0.2.0-instalador.exe`. Guía para quien lo instala: `docs/LEEME.md`.

## ▶ Qué falta (lo tiene que ver la persona)

1. Elegir la voz que más le guste («Voz para libros 1/2/3»).
2. Comprobar en la biblioteca la **rueda del ratón** y el **teclado** (flechas, Enter, Ctrl+F): en las pruebas automáticas no llegan a la app.
3. Probar MOON con sus libros de verdad (su CBR y su manga en PDF).
4. Opcional: Open Library (L3), IA (L13–L14).

## Para retomar con un modelo de IA

```
Vamos a continuar MOON, un lector de libros, cómics y manga en C# (.NET 10, WPF)
para Windows. Lee primero AGENTS.md, luego la sección "Qué hacer mañana" de
README.md y docs/03-ROADMAP.md. Haz pruebas CORTAS (una pasada) y no muevas el
ratón ni pulses teclas si estoy usando el PC: pregúntame antes. Nivel: medio;
explica los términos nuevos.
```

## Cómo se usa (desarrollo)

En `C:\Dev\MOON`:

```
dotnet build Moon.sln          # compilar
dotnet test Moon.sln           # 80 pruebas
src\Moon.App\bin\Debug\net10.0-windows10.0.19041.0\MOON.exe                    # MOON de verdad
src\Moon.App\bin\Debug\net10.0-windows10.0.19041.0\MOON.exe --datos C:\prueba  # copia de prueba (no toca la biblioteca real)
powershell -ExecutionPolicy Bypass -File tools\instalar-voz.ps1                 # motor de voz en otro PC (NVIDIA)
```

## Dónde guarda las cosas

| Qué | Dónde |
|---|---|
| Ajustes | `%APPDATA%\MOON\settings.json` |
| Biblioteca, notas, vocabulario | `%APPDATA%\MOON\moon.db` (SQLite) |
| Libros añadidos (copias) | `Documentos\MOON\Biblioteca` |
| Portadas | `%LOCALAPPDATA%\MOON\covers` |
| Voces (audios de 10–15 s) | `%APPDATA%\MOON\voces` |
| Motor de voz (Python + Chatterbox, ~6 GB) | `%LOCALAPPDATA%\MOON\voz` |
| Errores | `%LOCALAPPDATA%\MOON\logs\error.log` |

## Índice de la documentación

| Archivo | Contenido |
|---|---|
| `AGENTS.md` | Instrucciones para cualquier modelo de IA |
| `docs/01-VISION.md` | Qué hace, qué no, ideas futuras |
| `docs/02-ARCHITECTURE.md` | Tecnología y módulos (plan original) |
| `docs/03-ROADMAP.md` | Hitos con casillas y registro de sesiones |
| `docs/04-RESEARCH.md` | Proyectos parecidos, voces, diccionarios, licencias |
| `THIRD-PARTY-NOTICES.md` | Bibliotecas y datos usados, con sus licencias |

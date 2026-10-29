# MOON — lector de libros, cómics y manga

Lector de escritorio para **Windows 10/11** hecho en **C# / .NET 10 / WPF**: biblioteca (carpeta propia y discos externos), libros EPUB/MOBI/AZW3/FB2, cómics CBZ/CBR/CB7, manga en PDF, subrayado y notas, diccionario inglés → español con vocabulario para Anki, y lectura en voz alta con una voz clonada (Chatterbox, en el propio PC). Sin cuentas ni internet.

## Estado (2026-10-27)

| Hito | Qué | Estado |
|---|---|---|
| L1 | Esqueleto, temas, instancia única | ✅ |
| L3 | Biblioteca: añadir (copia), enlazar carpetas y discos externos | ✅ (falta Open Library) |
| L5 | Lector de libros (foliate-js en WebView2) | ✅ |
| L2 + L4 | Lector de cómics y manga (CBZ, CBR, CB7, PDF como imágenes) | ✅ |
| L6 | Subrayado, notas, marcadores, zonas en cómics, exportar | ✅ |
| L7 | Diccionario inglés → español sin internet + vocabulario (Anki) | ✅ |
| L5b | Lectura en voz alta (Chatterbox o voz de Windows) | ⚠️ **código hecho, falta oírla funcionar** |
| L8 | «Leer texto» en cómics: OCR de un bocadillo → diccionario y vocabulario | ⚠️ **motor probado con bocadillos reales, falta probar la ventana con clics** |

Todo lo marcado ✅ lo probó Claude con clics reales en una copia de prueba; **falta que lo vea la persona**.

## ▶ Qué hacer mañana (en este orden)

1. **Verificar la voz (L5b)** — *solo cuando la persona no esté usando el PC* (las pruebas mueven el ratón y suenan por los altavoces):
   - Abrir `MOON.exe --datos <carpeta de prueba>`, abrir *Don Quijote*, pulsar 🔊 **Escuchar**.
   - Comprobar: empieza a hablar (la primera vez ~30 s), resalta la frase, pasa de página sola, la velocidad cambia, el temporizador para, y **NOX** muestra el libro con ⏯ (controles multimedia de Windows).
   - El último fallo ("Fin del libro" al empezar en una portada sin texto) se corrigió **sin volver a probar**.
   - La persona debe elegir cuál de "Voz para libros 1/2/3" le gusta más.
2. **Que la persona pruebe MOON con sus libros de verdad** (su biblioteca real está vacía; las pruebas se hicieron con `--datos`):
   enlazar su disco/carpeta, abrir su CBR (*The Walking Dead*) y su manga PDF (*Saga Sword&Shield*), subrayar, diccionario, vocabulario.
3. **Probar «Leer texto» (L8)** con clics (preguntar antes): abrir *The Walking Dead*, pulsar «Leer texto», recuadrar un bocadillo, clic en una palabra, «Guardar palabra», «Guardar como zona». Si la persona lee cómics en inglés: instalar el OCR en inglés (el panel copia el comando).
4. **Siguiente hito según el ROADMAP:** L9 («¿Qué es esto?»), luego L10…L14. Ver `docs/03-ROADMAP.md`.
5. Pendientes menores: Open Library (L3), "Continuar leyendo" ya funciona; instalador de MOON (L12).

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

# AGENTS.md — Programa 2 (Lector)

Lee este archivo completo antes de tocar nada. También aplican las reglas de `../NOX/AGENTS.md` (persona usuaria, flujo, git, español, un paso por vez).

**Antes de empezar:** lee "Qué hacer mañana" en `README.md` (estado actual y siguiente paso).

## Cómo trabajar con esta persona (aprendido)
- **Pruebas cortas**: una pasada, pocos pasos. No repetir scripts largos (gastan tokens); si falla el script, corregir y seguir desde ahí.
- **No mover el ratón ni pulsar teclas si la persona está usando el PC** (las pruebas de interfaz hacen clics reales y pueden caer en otra ventana). Preguntar antes; en scripts, comprobar que la ventana de MOON está al frente y detenerse si no.
- Probar siempre en una copia: `MOON.exe --datos <carpeta>` (no toca la biblioteca real).
- PowerShell 5.1: guardar los scripts con BOM (tildes); `$s` y `$S` son la misma variable.

## Proyecto
Lector de escritorio Windows 11 llamado **MOON**: abre cómics/manga (CBZ, CBR, carpetas de imágenes), libros (EPUB) y PDF; permite subrayar, anotar, buscar palabras en diccionario, hacer OCR de una zona de la página y buscar el origen de una imagen.

## Reglas técnicas (obligatorias)
1. C# 14, **.NET 10 (LTS)**, WPF, MVVM con `CommunityToolkit.Mvvm`, DI con `Microsoft.Extensions.DependencyInjection`. TFM Windows: `net10.0-windows10.0.19041.0`.
2. Proyectos: `Moon.Core` (lógica pura, sin WPF ni Windows, probada con xUnit), `Moon.Windows` (OCR de Windows, archivos, WebView2), `Moon.App` (WPF).
3. **Las anotaciones nunca modifican el archivo original** del cómic o libro. Se guardan aparte en SQLite (ver arquitectura). Exportables a Markdown.
4. **Sin contenido con derechos de autor incluido** en la app ni scrapers de sitios de manga. La app solo lee archivos que la persona usuaria ya tiene. No eludir DRM.
5. **Privacidad primero:** todo local por defecto. Cualquier función que envíe algo a internet (búsqueda inversa de imágenes, diccionario en línea, traducción, IA) debe: estar **desactivada por defecto**, decir **qué se envía y a quién**, y pedir confirmación antes de subir una imagen.
6. **Claves de API** (si se usan) las pone la persona usuaria; guardarlas cifradas con DPAPI (`ProtectedData`), nunca en el código ni en el repositorio.
7. **Rendimiento:** las páginas grandes se decodifican en segundo plano, con `DecodePixelWidth`, y se liberan al salir de pantalla. Prelectura de 2–3 páginas. Sin cargar un cómic entero en memoria.
8. **Licencias:** no copiar código de Koodo Reader (AGPL), PaperBunkr (AGPL), mokuro/BallonsTranslator/manga-image-translator (GPL) ni de Panel Shelf (cerrado). Sí se pueden usar como referencia de *ideas*. EzManga (MIT) y comic-translate (Apache-2.0) permiten reutilizar con aviso. Registrar en `THIRD-PARTY-NOTICES.md`.
9. Colores/tamaños solo desde diccionarios de recursos (temas claro/oscuro, igual que el Programa 1).
10. Antes de cerrar un hito: `dotnet build` y `dotnet test` sin errores; actualizar el ROADMAP.

## Prototipos obligatorios antes de comprometerse (spikes)
Hacer un experimento de 1–2 horas y decidir, antes de construir sobre ello:
- **S1 — OCR de Windows** (`Windows.Media.Ocr`) desde WPF sin empaquetar: ¿funciona? Su documentación dice que requiere identidad de paquete; verificar.
- **S2 — EPUB en WebView2 + epub.js:** selección de texto, resaltado y recuperación de la posición (CFI).
- **S3 — Webtoon:** desplazamiento continuo con 200+ páginas sin agotar la memoria.
Documentar el resultado en `docs/03-ROADMAP.md`.

## Fuera de alcance de la v1
Servidor/sincronización en la nube, descarga de contenido de internet, MOBI/AZW3 con DRM, traducción automática que reescribe las imágenes (tipo manga-image-translator), versión móvil.

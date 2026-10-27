# 03 — Hoja de ruta (Programa 2)

Marca `[x]` cuando esté terminado **y verificado por la persona usuaria**. Un hito a la vez.
**Prerrequisito:** Programa 1 con M0–M4 terminados (así .NET 10 y el editor ya están instalados y la persona ya conoce el flujo).

**Orden (2026-10-27: la persona lee sobre todo libros):** L0 → L1 → L3 → **L5 (EPUB)** → L6 → L7 → **L5b (voz)** → L2 → L4 → L8–L12 → L13–L14 (opcionales).
**MVP = L1, L3, L5, L5b, L6, L7 (palabra) + tema.** Los cómics (L2, L4) llegan justo después.

---

## L0 — Prototipos (spikes) (≈4–5 h en total)
Experimentos pequeños y desechables, en `spikes/`. Documentar el resultado aquí.
- [ ] **S1 — OCR de Windows desde WPF sin empaquetar.** Leer una imagen con texto en inglés y otra en japonés. ¿Funciona? ¿Qué idiomas hay instalados? ¿Hace falta MSIX? *Resultado:* ____
- [ ] **S2 — EPUB en WebView2.** Cargar un EPUB de dominio público, seleccionar texto, recibir en C# el texto y el CFI, y volver a resaltarlo. *Resultado:* ____
- [ ] **S3 — Webtoon.** Cargar 200 imágenes grandes en un `ScrollViewer` virtualizado; medir RAM. *Resultado:* ____
- [ ] **S5 — Voces.** Leer el mismo párrafo (inglés y español) con: voces de Windows (`Windows.Media.SpeechSynthesis`), **Piper** y **Kokoro** (KokoroSharp). Medir: segundos hasta que empieza a hablar, velocidad frente a tiempo real, RAM, tamaño de descarga, y si se pueden saber los límites de cada palabra para resaltarla. Escuchar y elegir. *Resultado (2026-10-27, CPU, `spikes/S5-voces`):* **Piper** genera ~13 s de audio en 0,7–2 s (con carga del modelo), 60–110 MB por voz. **Kokoro** (KokoroSharp 0.8.4): carga 33 s la primera vez (incluye descarga de ~320 MB), luego 2–4 s por ~12 s de audio, **~1,4 GB de RAM**. Voz de Windows: Helena (es-ES), Sabina (es-MX), Zira (en-US). Licencias de voces Piper: claude-high Apache-2.0, ald Unlicense, davefx CC0, sharvard CC-BY 3.0, lessac (inglés) licencia de investigación de Blizzard 2013 → evitarla. **A la persona no le gustó ninguna.** **Chatterbox Multilingual** (MIT, Python + PyTorch 2.8 cu128 con `uv`; chatterbox-tts 0.1.7 pide torch 2.6, que no soporta RTX 50, se forzó 2.8; falta `setuptools<81` por `pkg_resources`): RTX 5070, carga 10 s, ~10 s de audio en 5–13 s (≈ tiempo real), 3,9 GB de VRAM, clona una voz con ~14 s de referencia. Algunas muestras salen más cortas que el texto (posible salto de frases): vigilar. Elección: **Chatterbox con voz clonada** de un audio que elige la persona (la voz de referencia no se distribuye con MOON). Fragmento elegido: ____
- [ ] **S4 — Búsqueda inversa.** Copiar un recorte al portapapeles y abrir Google Lens/TinEye/SauceNAO. ¿Cuál es el flujo menos incómodo? *Resultado:* ____

## L1 — Esqueleto (≈1 h)
- [x] Solución `Moon.sln` con Core, Windows, App y Tests (como el Programa 1, TFM `net10.0-windows10.0.19041.0`).
- [x] Ventana principal, temas Sistema/Oscuro/Claro (botón arriba a la derecha; también la barra de título), instancia única (abrir otra vez trae la ventana al frente).
- [x] Copiar/adaptar `Tokens.*.xaml` del Programa 1. Icono: luna blanca en círculo negro (`Assets/moon.ico`).
- *Verificado por Claude (2026-10-27): compila sin avisos, 5 pruebas, capturas en oscuro y claro, segunda apertura no crea otro proceso. Falta que lo vea la persona.*

## L2 — Lector de cómics básico (≈4 h)
- [ ] Tests: `NaturalSort`, `ReadingDirection`, `SpreadLayout`.
- [ ] `ArchivePageProvider` (CBZ/ZIP) con SharpCompress; `FolderPageProvider`.
- [ ] Vista de una página; teclas ← →, Re Pág/Av Pág; zoom con la rueda; ajustar a ancho/alto.
- [ ] Doble página y dirección RTL/LTR; modo webtoon (si S3 salió bien).
- [ ] Prelectura ±3 páginas; liberar memoria.
- [ ] Pantalla completa (F11) con barras que se ocultan.
- [ ] **Ajustes de imagen** para escaneos: brillo, contraste, gamma y "quitar amarillo" (papel viejo). Por libro, sin tocar el archivo original.

**Terminado cuando:** un CBZ de 200 páginas se lee con fluidez y la RAM no crece sin control al avanzar.

## L3 — Biblioteca y progreso (≈4 h)
Decisión (2026-10-27): **dos formas de meter libros**: "Añadir libros…" **copia** a `Documentos\MOON\Biblioteca`; "Enlazar carpeta…" **lee en su sitio** (p. ej. un disco externo). También arrastrando archivos (se copian) o carpetas (se enlazan).
- [x] SQLite (`%APPDATA%\MOON\moon.db`: tablas `sources` y `books`, versión con `PRAGMA user_version`).
- [x] Carpetas de biblioteca; escaneo en segundo plano; portadas en caché (`%LOCALAPPDATA%\MOON\covers`). EPUB: título, autor, serie y portada (EPUB 2 y 3). CBZ: `ComicInfo.xml` y primera página en orden natural.
- [x] Discos externos: si se desconecta, sus libros quedan en gris ("Desconectado") sin perder nada; si Windows le cambia la letra, se encuentra por el nombre del disco. Avisos de Windows (`WM_DEVICECHANGE`) y `FileSystemWatcher` en vez de revisar cada cierto tiempo.
- [x] Huella del archivo (tamaño + principio y final) para reconocer libros movidos o renombrados y conservar su progreso; no se relee si no cambian tamaño ni fecha.
- [x] Vista de cuadrícula con búsqueda (título, autor, serie), barra lateral por carpeta, menú: Abrir, Mostrar en el Explorador, Quitar.
- [ ] "Continuar leyendo" (cuando exista el lector, L5).
- *Verificado por Claude (2026-10-27): 31 pruebas; prueba con clics reales en una copia aparte (`MOON.exe --datos <carpeta>`): enlazar, añadir, buscar, desconectar y reconectar, tema claro. Falta que lo vea la persona.*
- [ ] **Open Library** (sin cuenta ni clave): portada, autor y año de los libros que no los traen. Solo al pulsar "Buscar datos", con caché local. Se puede desactivar.

## L4 — CBR/RAR/7Z (≈1.5 h)
- [ ] Soporte RAR/CB7 con SharpCompress; mensaje claro si un archivo está corrupto o cifrado.

## L5 — Libros EPUB (≈5 h) *(según resultado de S2)*
- [ ] `BookReaderView` con WebView2 y el lector JS.
- [ ] Paginación o desplazamiento; tamaño/tipo de letra, márgenes, tema (claro/oscuro/sepia).
- [ ] Progreso por CFI en SQLite; índice (tabla de contenidos).
- [ ] Seguridad de WebView2 (sin navegación externa).

## L5b — Lectura en voz alta (≈6 h) *(según resultado de S5; gratis y sin internet)*
- [ ] `ITtsEngine` con tres motores, todos en el PC:
  - **Voces de Windows** (ya instaladas, sin descarga; suenan más robóticas). Por defecto.
  - **Piper** (voces neuronales pequeñas, ~60 MB por voz, muy rápidas en CPU; es_ES, es_MX, inglés). Revisar la licencia de **cada voz** en su MODEL_CARD.
  - **Kokoro** (KokoroSharp: código MIT, modelo Apache-2.0, ~320 MB; la mejor calidad en inglés, español aceptable).
  - Las voces neuronales se descargan solo si la persona las elige ("Descargar voz…").
- [ ] Leer frase a frase desde la posición actual; generar la siguiente frase mientras suena la actual (sin pausas).
- [ ] **Resaltar** la frase (y si el motor lo permite, la palabra) que se está leyendo; pasar de página sola.
- [ ] Velocidad 0,5×–3×, elegir voz por idioma del libro, ⏮ frase anterior / ⏭ siguiente.
- [ ] **Temporizador para dormir** (fin del capítulo o 15/30/60 min).
- [ ] Controles multimedia de Windows (SMTC): teclas multimedia, panel de volumen y **NOX** muestran "MOON — título del libro" con ⏯ sin tocar NOX.
- [ ] Guardar la posición de escucha igual que la de lectura.
- [ ] (Más adelante) Exportar un capítulo a MP3/M4B para escucharlo fuera del PC.
- **No usamos** Edge TTS "gratis": usa un servicio de Microsoft sin permiso oficial, puede dejar de funcionar y envía el texto del libro a internet.

## L6 — Subrayado y notas (≈5 h)
- [ ] EPUB: seleccionar → resaltar (5 colores) → nota; reaplicar al abrir.
- [ ] `AnnotationPanel`: lista, salto, editar, borrar.
- [ ] Cómics: herramienta de rectángulo (`NormalizedRect`) con color y nota.
- [ ] Marcadores.
- [ ] Exportar a Markdown y CSV (con pruebas).

## L7 — Diccionario y vocabulario (≈4 h)
- [ ] `IDictionarySource` + una primera fuente (decidir idioma y fuente; ver decisiones pendientes).
- [ ] `LookupPopup` al seleccionar o hacer doble clic en una palabra.
- [ ] Guardar en vocabulario con contexto; `VocabularyView`; exportar CSV para Anki.
- [ ] Caché de consultas para no repetir peticiones; opción "solo offline".

## L8 — OCR por zona (≈4 h) *(según resultado de S1)*
- [ ] `RegionSelectorOverlay` y recorte del bitmap original.
- [ ] `IOcrEngine` con la opción que salió mejor en S1 (Windows OCR; si falla sin MSIX, **RapidOcrNet** con modelos PaddleOCR en ONNX: ver `04-RESEARCH.md` §8.3).
- [ ] Panel con texto editable → buscar palabra / traducir.
- [ ] Selección de idioma de OCR y aviso si falta el paquete.

## L9 — "¿Qué es esto?" (≈3 h)
- [ ] Recorte → portapapeles → abrir servicio de búsqueda inversa (según S4).
- [ ] Aviso de privacidad antes de subir/abrir.
- [ ] Lista "Cosas por investigar" con recortes y notas.

## L10 — PDF (≈4 h)
- [ ] Visor PDF (PDF.js en WebView2 o alternativa decidida).
- [ ] Resaltado de texto y notas con coordenadas.

## L11 — Traducción (≈2 h)
- [ ] `ITranslator` con una implementación (según decisión); desactivada por defecto.

## L12 — Pulido, ajustes y empaquetado (≈4 h)
- [ ] Ajustes completos, atajos, accesibilidad (contraste, lector de pantalla básico).
- [ ] Rendimiento con bibliotecas de 1000+ archivos.
- [ ] Instalador (Inno Setup), `THIRD-PARTY-NOTICES.md`, README de usuario.

## L13 — IA con clave propia (≈5 h) *(opcional, después de L12)*
- [ ] Clave guardada con DPAPI; apagada por defecto; aviso de qué se envía y confirmación cada vez.
- [ ] "¿Qué es esto?": recorte → IA con visión → explicación.
- [ ] "Pregúntale al libro" **sin spoilers**: solo se envía el texto ya leído (hasta la página actual) y cada respuesta cita la página.
- [ ] Explicar una frase difícil en contexto (inglés → español).

## L14 — IA en tu PC, sin internet (≈6 h) *(opcional, después de L12)*
Con **ONNX Runtime** (`Microsoft.ML.OnnxRuntime` + DirectML para usar la tarjeta gráfica). Los modelos se descargan solo si se activa la función.
- [ ] **Mejorar nitidez**: botón por página para escaneos de baja resolución (modelo tipo Real-ESRGAN ×2/×4, verificar licencia del modelo). El resultado se guarda en caché; nunca se modifica el archivo original. Medir tiempo y RAM.
- [ ] **Búsqueda por significado** en tus subrayados, notas y vocabulario (p. ej. "¿dónde subrayé algo sobre la culpa?"). Modelo pequeño de "embeddings" (~100 MB) y los vectores guardados en SQLite.

---

## Decisiones pendientes (la persona usuaria debe elegir)
- [x] **Idiomas:** inglés → español (2026-10-27). Diccionario y traducción EN→ES; OCR en inglés (sin japonés vertical).
- [x] **Se leen más libros** (2026-10-27): EPUB va antes que los cómics.
- [x] **Lectura en voz alta** gratis y en el PC (2026-10-27): L5b.
- [ ] ¿En qué idioma están los libros que quieres escuchar: inglés, español o ambos? (decide qué voces probar primero en S5).
- [x] **Nombre:** MOON (2026-10-27; antes Marginalia). Combina con NOX.
- [x] **IA con clave propia:** sí, pero **más adelante** (L13), apagada por defecto (2026-10-27).
- [x] **Anime:** nada de anime (ni reproductor, ni AniList, ni trace.moe). Solo manga, cómics y libros (2026-10-27).
- [x] **Cuentas:** ninguna (ni AniList, ni MyAnimeList, ni Anki). Todo funciona sin cuentas; el vocabulario se exporta a CSV (2026-10-27).

- [x] **Mejoras elegidas** (2026-10-27): ajustes de imagen (L2), Open Library (L3), mejorar nitidez y búsqueda por significado (L14). Descartada: traducción encima del globo.

## Registro de sesiones
- **2026-10-27 — Investigación del ecosistema** (`04-RESEARCH.md` §8): Readest, NeeView (C#/WPF, MIT: referencia para L2), Seanime, Mihon, Komga/Kavita, OCR en ONNX, IA sin spoilers. Decisiones: EN→ES, sin anime, sin cuentas, IA más adelante (L13).
- **2026-10-26 — Planificación.** Programa 2 redefinido como lector (antes: archivos + notas). Investigación en `04-RESEARCH.md`. Sin código.

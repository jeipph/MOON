# 03 — Hoja de ruta (Programa 2)

Marca `[x]` cuando esté terminado **y verificado por la persona usuaria**. Un hito a la vez.
**Prerrequisito:** Programa 1 con M0–M4 terminados (así .NET 10 y el editor ya están instalados y la persona ya conoce el flujo).

**Orden (2026-10-27: la persona lee sobre todo libros):** L0 → L1 → L3 → **L5 (EPUB)** → L6 → L7 → **L5b (voz)** → L2 → L4 → L8–L12 → L13–L14 (opcionales).
**MVP = L1, L3, L5, L5b, L6, L7 (palabra) + tema.** Los cómics (L2, L4) llegan justo después.

---

## L0 — Prototipos (spikes) (≈4–5 h en total)
Experimentos pequeños y desechables, en `spikes/`. Documentar el resultado aquí.
- [x] **S1 — OCR de Windows desde WPF sin empaquetar.** *Resultado (2026-10-29, `spikes/S1-ocr`):* **funciona sin MSIX** (`Windows.Media.Ocr`, 6–30 ms por bocadillo). En este PC solo están instalados **es-ES y es-MX**; con ellos un texto en inglés sale casi bien ("CAN'T" → "CANIT", que MOON corrige). Página entera: lee mal (el dibujo confunde). Recorte de un bocadillo de *The Walking Dead* (castellano) agrandado **×2**: "¡Oof!", "¿Qué coño ha sido todo eso?", "Joder." perfectos; ×3–×4 empeora ("Jover"). No se necesita RapidOcrNet. Japonés: descartado (la persona lee en inglés/español).
- [x] **S2 — EPUB en WebView2.** *Resultado (2026-10-27):* funciona con **foliate-js** (MIT) en WebView2 con hosts virtuales `app.moon` y `book.moon`; posición por CFI, índice, páginas y desplazamiento. Falta probar selección de texto y resaltado (L6).
- [ ] **S3 — Webtoon.** Cargar 200 imágenes grandes en un `ScrollViewer` virtualizado; medir RAM. *Resultado:* ____
- [ ] **S5 — Voces.** Leer el mismo párrafo (inglés y español) con: voces de Windows (`Windows.Media.SpeechSynthesis`), **Piper** y **Kokoro** (KokoroSharp). Medir: segundos hasta que empieza a hablar, velocidad frente a tiempo real, RAM, tamaño de descarga, y si se pueden saber los límites de cada palabra para resaltarla. Escuchar y elegir. *Resultado (2026-10-27, CPU, `spikes/S5-voces`):* **Piper** genera ~13 s de audio en 0,7–2 s (con carga del modelo), 60–110 MB por voz. **Kokoro** (KokoroSharp 0.8.4): carga 33 s la primera vez (incluye descarga de ~320 MB), luego 2–4 s por ~12 s de audio, **~1,4 GB de RAM**. Voz de Windows: Helena (es-ES), Sabina (es-MX), Zira (en-US). Licencias de voces Piper: claude-high Apache-2.0, ald Unlicense, davefx CC0, sharvard CC-BY 3.0, lessac (inglés) licencia de investigación de Blizzard 2013 → evitarla. **A la persona no le gustó ninguna.** **Chatterbox Multilingual** (MIT, Python + PyTorch 2.8 cu128 con `uv`; chatterbox-tts 0.1.7 pide torch 2.6, que no soporta RTX 50, se forzó 2.8; falta `setuptools<81` por `pkg_resources`): RTX 5070, carga 10 s, ~10 s de audio en 5–13 s (≈ tiempo real), 3,9 GB de VRAM, clona una voz con ~14 s de referencia. Algunas muestras salen más cortas que el texto (posible salto de frases): vigilar. Elección: **Chatterbox con voz clonada** de un audio que elige la persona (la voz de referencia no se distribuye con MOON). La persona aprobó la voz clonada de "VOZ PARA LIBROS" (2026-10-27) y quiere poder añadir más voces en el futuro.
- [ ] **S4 — Búsqueda inversa.** Copiar un recorte al portapapeles y abrir Google Lens/TinEye/SauceNAO. ¿Cuál es el flujo menos incómodo? *Resultado:* ____

## L1 — Esqueleto (≈1 h)
- [x] Solución `Moon.sln` con Core, Windows, App y Tests (como el Programa 1, TFM `net10.0-windows10.0.19041.0`).
- [x] Ventana principal, temas Sistema/Oscuro/Claro (botón arriba a la derecha; también la barra de título), instancia única (abrir otra vez trae la ventana al frente).
- [x] Copiar/adaptar `Tokens.*.xaml` del Programa 1. Icono: luna blanca en círculo negro (`Assets/moon.ico`).
- *Verificado por Claude (2026-10-27): compila sin avisos, 5 pruebas, capturas en oscuro y claro, segunda apertura no crea otro proceso. Falta que lo vea la persona.*

## L2 — Lector de cómics básico (≈4 h)
- [x] Tests: `NaturalSort`, `SpreadLayout` (portada sola, panorámicas solas, manga a la derecha), `ImageAdjustments`, `ComicView`, fuentes de páginas.
- [x] `IPageSource`: ZIP (System.IO.Compression), RAR/7Z (SharpCompress 0.50), carpetas; se mira el contenido y no la extensión (un ".cbr" que es ZIP también abre). **PDF como imágenes** con `Windows.Data.Pdf` (sin descargas): adelantado de L10 porque la persona lee manga en PDF.
- [x] Una página / doble / webtoon; ajustar a la página o al ancho; zoom 100–400 % (Ctrl + rueda, Ctrl +/−/0) y arrastrar para mover.
- [x] Dirección por cómic: cómic → o manga ← (si `ComicInfo.xml` dice Manga, empieza en ←); en manga la barra de páginas va al revés.
- [x] Teclas ← → (al revés en manga), Espacio/Av Pág (baja y luego pasa), Re Pág, Inicio/Fin; clic en el tercio izquierdo/derecho; rueda.
- [x] Caché de 8 páginas decodificadas al tamaño de pantalla + prelectura de ±2; en webtoon solo se cargan las páginas visibles.
- [x] Pantalla completa (F11 / Esc).
- [x] **Ajustes de imagen** por cómic (brillo, contraste, gamma, "quitar amarillo"), sin tocar el archivo; columna `comic_view` (base de datos versión 3).
- *Verificado por Claude (2026-10-27) con los archivos de la persona: The Walking Dead #001 (CBR, 42 págs.) y Saga Sword&Shield tomo 2 (PDF, 146 págs.): abrir, pasar, doble, ajustes, webtoon, manga ←, recordar la página. Memoria: ~330–375 MB con un cómic abierto. Falta que lo vea la persona.*

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
- [x] Soporte RAR/CB7 con SharpCompress (portada y `ComicInfo.xml` en la biblioteca también); mensaje claro si un archivo está dañado, cifrado o sin imágenes. Hecho junto con L2.

## L5 — Libros EPUB (≈5 h) *(según resultado de S2)*
- [x] `ReaderWindow` (una ventana por libro) con WebView2 y `Web/reader.html` + `reader.js` + foliate-js. Abre también MOBI, AZW3, FB2 (sin DRM) y CBZ.
- [x] Páginas o continuo; tamaño de letra (70–200 %, Ctrl +/−/0), letra del libro / con serifa / sin serifa, márgenes, página como MOON / clara / sepia / oscura. Iguales para todos los libros (`settings.json` → `Reader`).
- [x] Progreso por CFI en SQLite (columna `location`, base de datos versión 2); índice con el capítulo actual marcado; barra de progreso para saltar; "Continuar leyendo" en la biblioteca.
- [x] Teclas: ← → Re Pág Av Pág Espacio, rueda del ratón, F11 / Esc pantalla completa.
- [x] Seguridad: CSP bloquea los scripts del libro; no se navega fuera de `app.moon`; enlaces de internet solo con confirmación y en el navegador.
- *Verificado por Claude (2026-10-27): 34 pruebas; prueba con clics y teclas reales: abrir con doble clic, pasar páginas, opciones (sepia, 120 %, sin serifa, oscura), índice, pantalla completa, cerrar y reabrir en la misma posición. Falta que lo vea la persona. Pendiente de probar: un EPUB con scripts (deben quedar bloqueados).*

## L5b — Lectura en voz alta (≈6 h) *(según resultado de S5; gratis y sin internet)*
**Decisión (2026-10-27):** voz principal **Chatterbox** clonando un audio elegido por la persona (servicio local en Python, solo en PCs con tarjeta gráfica). Sin tarjeta gráfica: voces de Windows. **Varias voces:** carpeta de voces (`%APPDATA%\MOON\voces`, un audio de 10–15 s por voz) y elegir voz por libro. Las voces de referencia nunca se incluyen en el instalador.
- [x] **Funciona** (verificado 2026-10-29). Detalles:
  - `VoiceService`: Chatterbox con `Voice/moon_voice.py` (servidor HTTP solo en 127.0.0.1, puerto libre, se calienta al cargar, se cierra si MOON se cierra) y el Python de `%LOCALAPPDATA%\MOON\voz\.venv` (instalar con `tools\instalar-voz.ps1`); si no está, voz de Windows (`Windows.Media.SpeechSynthesis`).
  - Voces = audios de `%APPDATA%\MOON\voces` (este PC: "Voz para libros 1/2/3", fragmentos del audio de la persona; nunca van en el instalador).
  - `ReadAloudSession`: frases de foliate-js (`initTTS('sentence')`, SSML con marcas), trozos de ≤260 caracteres (`SpeechText`), 3 frases preparadas por adelantado, reproductor de Windows (`MediaPlayer`: velocidad 0,75–1,5× y controles multimedia → NOX), resaltado con CSS Custom Highlight, pasa de capítulo solo.
  - Barra de voz: ⏮ repetir frase, ⏯, ⏭ frase siguiente, voz, velocidad, temporizador (fin del capítulo, 15/30/60 min).
  - *Prueba 2026-10-27:* el botón "Escuchar" y la barra funcionan; *Don Quijote* empezó en la portada (sin texto) y MOON lo tomó por "Fin del libro" → corregido (las páginas sin texto se saltan; los errores se muestran).
  - *Prueba 2026-10-29:* la portada ya se salta bien, pero **todas las frases fallaban**: .NET (`PostAsJsonAsync`) enviaba la petición «por trozos», sin `Content-Length`, y el servidor de Python no sabía leerla (error 500). Corregido: se envía con `StringContent` (lleva el tamaño) y el servidor responde 411 con un mensaje claro si falta. Además, con 3 frases fallidas seguidas la lectura se detiene con un aviso, en vez de saltar frases hasta el final del libro (había llegado al 95 %). Los fallos de voz se apuntan en `%LOCALAPPDATA%\MOON\logs\error.log` (`ErrorLog`). Resultado: *Don Quijote* cap. VIII leído con «Voz para libros 1», resaltando la frase, pasando de página solo y con el capítulo visible en NOX. Falta que la persona elija entre las voces 1, 2 y 3.
- [ ] Guardar la posición de escucha igual que la de lectura (ahora la lectura en voz alta pasa de página y eso ya guarda la posición).
- [ ] (Más adelante) Exportar un capítulo a MP3/M4B para escucharlo fuera del PC.
- **No usamos** Edge TTS "gratis": usa un servicio de Microsoft sin permiso oficial, puede dejar de funcionar y envía el texto del libro a internet.

## L6 — Subrayado y notas (≈5 h)
- [x] EPUB: seleccionar → barra con 5 colores, "Nota…" y "Copiar" → subrayado con foliate-js (`Overlayer.highlight`); se redibuja en cada capítulo y al reabrir. Clic en un subrayado: editar nota, cambiar color o eliminar.
- [x] Panel de notas (lector de libros y de cómics): lista en orden del libro, clic = ir, doble clic o menú = editar/eliminar.
- [x] Cómics: botón "Marcar zona" → arrastrar un rectángulo (`NormalizedRect`) → nota y color; se dibuja sobre la página; clic en la zona = editar. (En webtoon no se marcan zonas.)
- [x] Marcadores (libros y cómics).
- [x] Exportar a Markdown (con datos para Obsidian) y CSV con `;` y BOM para Excel en español (con pruebas).
- Tabla `annotations` (base de datos versión 4); se borran con su libro.
- *Verificado por Claude (2026-10-27) con pruebas cortas: subrayar y marcador en Alice y siguen al reabrir; zona con nota en The Walking Dead. 55 pruebas. Falta que lo vea la persona.*

## L7 — Diccionario y vocabulario (≈4 h)
- [x] Diccionario inglés → español **sin internet**: Apertium eng-spa (GPL-2.0) convertido a `Data/en-es.tsv` (29 867 palabras, 830 KB) con `spikes/S7-diccionario` (DixToTsv). Formas base en inglés (`EnglishLemmatizer`: irregulares, plurales, -ed, -ing, comparativos, -ly) y expresiones de dos palabras ("give up").
- [x] Al seleccionar o hacer doble clic en una palabra (o hasta 3), la barra muestra la traducción por categoría, encima de los colores.
- [x] "Guardar palabra" con la frase y el libro (tabla `vocabulary`, base de datos versión 5; sin repetir la misma forma base; se conserva si se quita el libro). Ventana **Vocabulario** desde la biblioteca: eliminar, exportar para **Anki** (texto con tabuladores y cabeceras `#separator`) y CSV.
- [ ] (Más adelante, opcional y apagado) definiciones en inglés por internet; el diccionario local ya no necesita caché.
- *Verificado por Claude (2026-10-27): 72 pruebas; prueba corta: doble clic en "denial" → "nombre: denegación (f)", guardada y visible en Vocabulario. En cómics llegará con el OCR (L8). Falta que lo vea la persona.*

## L8 — OCR por zona (≈4 h) *(según resultado de S1)*
- [x] Botón **«Leer texto»** en el lector de cómics (excluye «Marcar zona»): arrastrar un recuadro → `WindowsOcrEngine` recorta la página **a resolución completa** (PDF dibujado a 3000 px, `PageImages.ReadOriginalAsync`), con 4 % de margen, y la agranda hasta ×2 con el decodificador de Windows (`OcrText.ToPixels/Scale`). Agrandar con WPF leía peor ("DIOS..." → "nos...").
- [x] `IOcrEngine` (Core) + `WindowsOcrEngine` (Moon.Windows): un motor por idioma, reutilizado. El spike S1 usa el mismo motor.
- [x] `OcrText.Clean`: une las líneas del bocadillo (y las palabras cortadas con guion), repara apóstrofos ("CANIT" → "CAN'T"), "¡" leídas como i y manchas tras ¿/¡ ("¿.Qué"), y pasa las MAYÚSCULAS de cómic a frase normal. 25 pruebas.
- [x] Panel abajo a la izquierda (`OcrPanelViewModel`): texto editable; clic, doble clic o selección = diccionario inglés → español (solo cuando la persona toca el texto, no al aparecer) y «Guardar palabra» (con la frase y la página); «Copiar»; «Guardar como zona» (zona azul con el texto de nota); Esc cierra.
- [x] Idioma del OCR en el panel (se recuerda en `settings.json` → `OcrLanguage`; por defecto inglés si está). Aviso si falta el inglés, con los pasos y un enlace que copia el comando `Add-WindowsCapability` (pide administrador: lo ejecuta la persona).
- *Verificado por Claude (2026-10-29) con clics reales en una copia de prueba (*The Walking Dead*, castellano): "¿Qué coño ha sido todo eso?" (pág. 10) y "Dios..." (pág. 11); recuadro vacío → "No se encontró texto"; «Leer texto» apaga «Marcar zona»; doble clic en "give" → "verbo: dar"; «✓ Guardada»; zona azul con su texto en Notas; Esc cierra; idioma recordado. 105 pruebas. Nota de la prueba: una ventana invisible de la app de Claude tapaba la parte derecha de la barra y algunos clics no llegaban a los botones (se pulsaron por accesibilidad); no es un fallo de MOON. Falta que lo vea la persona.*

## L9 — "¿Qué es esto?" (≈3 h)
- [ ] Recorte → portapapeles → abrir servicio de búsqueda inversa (según S4).
- [ ] Aviso de privacidad antes de subir/abrir.
- [ ] Lista "Cosas por investigar" con recortes y notas.

## L10 — PDF (≈4 h)
- [x] (Adelantado en L2) PDF como páginas de imagen en el lector de cómics, con `Windows.Data.Pdf`. Portada en la biblioteca.
- [ ] Visor PDF con texto seleccionable (PDF.js en WebView2 o alternativa decidida).
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
- **2026-10-29 — L8 (OCR por zona).** S1 resuelto: OCR de Windows sin MSIX. «Leer texto» en cómics con diccionario y vocabulario, probado con clics en una copia de prueba (se corrigieron el recorte con WPF, la búsqueda automática al aparecer el texto y "¿."). Falta: que la persona lo pruebe e instale el OCR en inglés si lee cómics en inglés.
- **2026-10-27 (tarde) — Desarrollo.** Hechos L1, L3, L5, L2+L4 (y PDF como imágenes), L6, L7; L5b programado sin verificar. Voz elegida: Chatterbox con la voz clonada ("Voz para libros 1/2/3" en `%APPDATA%\MOON\voces`). Pruebas con el CBR y el PDF de la persona y un cómic de 200 páginas (memoria estable ~300 MB). **Mañana:** ver "Qué hacer mañana" en `README.md` (primero verificar la voz cuando la persona no use el PC). Preferencias: pruebas cortas; no mover ratón/teclado si la persona está usando el PC.
- **2026-10-27 — Investigación del ecosistema** (`04-RESEARCH.md` §8): Readest, NeeView (C#/WPF, MIT: referencia para L2), Seanime, Mihon, Komga/Kavita, OCR en ONNX, IA sin spoilers. Decisiones: EN→ES, sin anime, sin cuentas, IA más adelante (L13).
- **2026-10-26 — Planificación.** Programa 2 redefinido como lector (antes: archivos + notas). Investigación en `04-RESEARCH.md`. Sin código.

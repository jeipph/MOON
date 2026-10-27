# 04 — Investigación: proyectos y herramientas similares

Fecha: 2026-10-26. Fuentes: búsqueda en GitHub y lectura de README. Lo no verificado se marca **(no confirmado)**. Una consulta final a GitHub fue bloqueada (límite de peticiones), por eso no se revisaron lectores de EPUB en WPF.

## 1. Lectores existentes (competencia y referencias)

| Proyecto | ★ | Tecnología | Licencia | Qué hace / qué aprendemos |
|---|---|---|---|---|
| **Koodo Reader** | ~28.6 k | Electron + TypeScript (inferido) | AGPL-3.0 | El más completo: EPUB, PDF, MOBI, cómics (CBR/CBZ/CB7), DjVu, TXT, etc. Subrayado en varios estilos, notas, marcadores, **diccionario, traducción, TTS, OCR (Paddle y Tesseract)**, más de 50 complementos de traducción/diccionario/TTS, diccionarios locales **MDX**, envío de palabras a **Anki**, exportación de notas a Readwise/Notion/Obsidian, sincronización con muchos servicios. No es un lector de cómics especializado. |
| **PaperBunkr** | 8 | **Avalonia + .NET 10**, SQLite (EF Core) | AGPL-3.0 | Biblioteca y lector local para Windows de cómics (CBZ, CBR, PDF) y libros (EPUB, PDF, FB2, MOBI/AZW3). Doble página, vertical continuo, RTL/LTR, brillo/contraste/gamma, resaltados y notas en libros, `ComicInfo.xml`, listas inteligentes, plugins. Versión 0.7.8-beta, una sola persona. Es lo más parecido en objetivos, pero sin OCR ni diccionario. |
| **EzManga** | 1 | **WinUI 3, .NET 8**, SharpCompress | MIT | CBZ/CBR/ZIP/RAR/7Z, carpetas, página única / doble / webtoon, RTL, zoom 50–300 %, biblioteca. Proyecto muy joven (1 commit). Útil como referencia de SharpCompress y orden natural. |
| **Panel Shelf** (Vowborn) | — | Cerrado | No indicada | Lector Windows de cómics, EPUB y PDF con OCR y "Free AI" (diccionario en contexto con **clave propia** de Gemini/Groq/OpenAI/etc.). Es la más cercana a tu idea y ya la conoces. |
| **ACBR** (binarynonsense) | 430 | JavaScript/Electron | No indicada | Lector y conversor para CBZ, CBR, CB7, EPUB, FB2, MOBI, PDF. |
| **Comigo** | 385 | Go | MIT | Lector de cómics/manga multiplataforma, también en el navegador. |
| **Kavita, Komga** | — | — | — | Servidores de biblioteca de cómics/manga (conocidos; **no revisados aquí**). Útiles si algún día quieres leer desde otros dispositivos. |
| **csBooks, AryanEbookLibrary** | 121 / 8 | — / C# | — / MIT | Lectores de libros genéricos. No revisados a fondo. |

**Hueco que aprovechamos:** casi nadie combina **lector de cómics + libros + subrayado en ambos + OCR de una zona + diccionario + vocabulario**, con privacidad por defecto y estilo minimalista. Koodo lo hace casi todo pero con enfoque en libros; Panel Shelf es el más cercano pero cerrado.

## 2. Herramientas para estudiar idiomas con manga (referencias de ideas)

| Proyecto | ★ | Licencia | Qué aporta |
|---|---|---|---|
| **manga-ocr** (kha-white) | 2.8 k | Apache-2.0 | OCR de japonés entrenado para manga: texto vertical y horizontal, furigana, varias líneas. **Modelo ~400 MB**, Python (PyTorch). Sin versión C#/ONNX confirmada. Puede inventar texto donde no hay. |
| **mokuro** | 1.8 k | GPL-3.0 | Convierte manga en HTML con texto seleccionable (OCR previo). Base de varios lectores: `mokuro-reader` (Svelte), `hanabira.org_manga_reader` (MIT, con traducciones y gramática), `chimahon` (Android). |
| **Yomitan** y diccionarios | — | — | Extensión de navegador de diccionario emergente; los diccionarios `jmdict-yomitan` (MIT) y listas de `yomitan-dictionaries` sirven de datos. **Jitendex** (diccionario japonés→inglés abierto y offline, actualizado mensualmente) es AGPL-3.0. |
| **manga_reader** (pablomerca) | 0 | No indicada | Lector de escritorio que muestra el texto OCR al pasar el cursor, diccionario (Jamdict), seguimiento de vocabulario con lematización, explicación con Gemini (clave propia). Solo Linux, necesita salida de mokuro. **Es casi tu idea, en pequeño.** |
| **comic-translate** | 3 k | Apache-2.0 | Traduce cómics: detección de globos (RT-DETR-v2), OCR (manga-ocr, Pororo, PPOCRv5; opcional Gemini/Azure), borrado de texto (LaMa), traducción con GPT/Claude/Gemini. GPU solo si se ejecuta desde código. **Fuera de la v1.** |
| **manga-image-translator**, **BallonsTranslator**, **CarrotMangaTranslator** | 10 k / 5 k / 98 | GPL-3.0 | Traducción completa de imágenes con reescritura. Pesadas y GPL. Solo para inspiración. |
| **MouseTooltipTranslator** | 1.4 k | MIT | Extensión que traduce al pasar el ratón en EPUB/PDF/OCR. Inspiración de interacción. |

## 3. Servicios y APIs que podemos conectar

| Servicio | Para qué | Coste / límites (según lo encontrado) | Observaciones |
|---|---|---|---|
| **Windows.Media.Ocr** | OCR offline en Windows | Gratis | Docs de Microsoft: solo con **identidad de paquete (MSIX)**. Idiomas = paquetes de OCR instalados (`AvailableRecognizerLanguages`). Un artículo antiguo menciona 26 idiomas (**no verificado actualmente**). Probar en S1. |
| **Tesseract** | OCR offline | Gratis, Apache-2.0 | Mejor con texto impreso limpio. |
| **Free Dictionary API (dictionaryapi.dev)** | Definiciones en inglés | Gratis, sin clave | Su mantenedor avisa de costos crecientes de servidor → posible inestabilidad. |
| **Merriam-Webster API** | Diccionario inglés | Gratis solo no comercial, 1000 consultas/día | Requiere clave. |
| **LibreTranslate / Argos Translate** | Traducción autoalojada | Gratis, AGPL-3.0, Docker | Unos 40–55 idiomas (fuente de baja calidad, no confirmado). Se usaría por HTTP desde un servidor propio. |
| **trace.moe** | Identificar escenas de anime por captura | Anónimo: cuota baja (fuentes discrepan: ~100/mes y 1 petición simultánea según una; otras dan otras cifras). Clave gratuita aumenta límites | Consultar la documentación oficial antes de implementar. |
| **SauceNAO** | Encontrar el origen de ilustraciones (más de 30 bases: Pixiv, Danbooru, DeviantArt…) | Requiere clave gratuita; límite diario **no encontrado** | Útil para fan-art; menos para cómics occidentales. |
| **Google Lens / TinEye** | Búsqueda inversa general | Gratis en navegador | Sin API pública fácil; flujo manual (pegar recorte). |
| **AniList / MangaDex / Comic Vine / Open Library** | Metadatos de series y libros | — | **No verificado aquí**; conocidos como APIs públicas. Evaluar en una versión posterior. |
| **LLM con visión (clave propia)** | "¿Qué es esto?", explicaciones, traducción en contexto | De pago por uso | Panel Shelf, Koodo y manga_reader ya usan este modelo "trae tu clave". |

## 4. Funciones que podemos poner en el Programa 2 (priorizadas)

| Prioridad | Función | Inspirada en | Dificultad |
|---|---|---|---|
| Alta | Lectura de CBZ/CBR con modos y RTL/LTR | EzManga, PaperBunkr | Media |
| Alta | Biblioteca con progreso | Todos | Media |
| Alta | EPUB con subrayado y notas | Koodo, PaperBunkr | Media-Alta (WebView2) |
| Alta | Diccionario por palabra + vocabulario | Koodo, manga_reader | Media |
| Media | **Rectángulo de anotación en cómics** | (diferencial) | Media |
| Media | **OCR por zona** | Koodo (OCR), Panel Shelf | Alta (riesgo S1) |
| Media | Exportar a Markdown/CSV (Anki) | Koodo | Baja |
| Media | Búsqueda inversa de imagen asistida | (diferencial) | Baja-Media |
| Media | PDF con resaltado | Koodo, PaperBunkr | Media |
| Baja | Traducción de frase | Koodo, MouseTooltipTranslator | Media |
| Baja | IA con visión con clave propia | Panel Shelf | Media |
| Baja | Texto a voz de libros | Koodo | Baja (TTS de Windows) |
| Baja | Ajustes de imagen (brillo, contraste, gamma) | PaperBunkr | Baja |
| Futura | Diccionarios MDX / JMdict | Koodo, Yomitan | Alta |
| Futura | Lematización japonesa | manga_reader | Alta |
| Futura | Traducción con reescritura de imagen | comic-translate | Muy alta (GPU, GPL) |
| Futura | Servidor/lectura en móvil (OPDS) | Kavita, Komga | Alta |

## 5. Trampas y riesgos

1. **OCR en Windows sin MSIX (S1).** Si no funciona, el plan B es empaquetar la app como MSIX (afecta al instalador) o usar Tesseract.
2. **OCR de manga ≠ OCR de documentos.** Texto vertical, tipografías manuscritas y fondos complicados dan resultados malos con OCR genérico. manga-ocr es mucho mejor para japonés, pero pesa ~400 MB y es Python. Siempre mostrar el texto en un cuadro **editable**.
3. **Rendimiento con imágenes grandes.** Decodificar con `DecodePixelWidth`, prelectura limitada, liberar páginas lejanas. Webtoons de cientos de páginas son lo más exigente.
4. **Orden de páginas.** Orden natural obligatorio; ignorar `__MACOSX`/`Thumbs.db`.
5. **WebView2 y EPUB.** Comunicación C#↔JS, seguridad del contenido del libro, y mantener las anotaciones ancladas (CFI) si el libro se actualiza. Prototipo S2.
6. **Privacidad al buscar imágenes y consultar palabras.** Todo lo que sale a internet puede revelar lo que lees. Desactivado por defecto, con aviso. Guardar en caché local.
7. **APIs que cambian o desaparecen.** Dictionaryapi.dev advierte costos; trace.moe cambió sus cuotas en 2025. Siempre detrás de interfaces y con "modo offline".
8. **Licencias.** Koodo y PaperBunkr: AGPL (si copiamos código, nuestra app tendría que ser AGPL). mokuro y traductores: GPL. Jitendex: AGPL (datos). JMdict/EDRDG: licencia propia de atribución (**verificar**). **Escribimos código propio.**
9. **Derechos de autor.** La app no incluye ni descarga cómics. Los usuarios leen sus archivos. Sin eludir DRM. Los enlaces para obtener cómics legalmente se dieron antes en la conversación (Manga Plus, ComicWalker, tiendas digitales).
10. **Alcance.** Cada característica sola es un proyecto. Mantener el orden del ROADMAP y no empezar OCR/IA antes de que leer sea agradable.

## 6. Enlaces
- Koodo Reader — https://github.com/koodo-reader/koodo-reader
- PaperBunkr — https://github.com/heisehis/PaperBunkr
- EzManga — https://github.com/uponatime2019/EzManga
- manga_reader — https://github.com/pablomerca/manga_reader
- manga-ocr — https://github.com/kha-white/manga-ocr
- comic-translate — https://github.com/ogkalu2/comic-translate
- mokuro — https://github.com/kha-white/mokuro
- hanabira.org_manga_reader — https://github.com/tristcoil/hanabira.org_manga_reader
- Jitendex — https://github.com/Jitendex/Jitendex
- jmdict-yomitan — https://github.com/yomidevs/jmdict-yomitan
- Windows.Media.Ocr — https://learn.microsoft.com/uwp/api/windows.media.ocr
- LibreTranslate — https://github.com/LibreTranslate/LibreTranslate
- VersOne.Epub (NuGet) — https://www.nuget.org/packages/VersOne.Epub

## 7. Segunda ronda (2026-10-27): clones y cómo funcionan

| Proyecto | Licencia | Qué aprendemos |
|---|---|---|
| **blueaxis/Poricom** (Python + PyQt5) | GPL-3.0 → solo ideas | Flujo de OCR de manga: abrir carpeta/CBZ/CBR/PDF → **dibujar un rectángulo sobre el globo** → OCR (manga-ocr para japonés, Tesseract para el resto) → texto al portapapeles. También un atajo (Alt+Q) para OCR de cualquier zona de la pantalla. Confirma el diseño de L8: rectángulo → texto editable → diccionario. |
| **mokuro** (Python) | GPL-3.0 | Pipeline en dos fases: detectar cajas de texto (comic-text-detector) y luego OCR de cada caja. Para nosotros: la v1 deja que la persona dibuje la caja (sin detector automático). |
| **fire-eggs/comicviewer** (C#, WPF, .NET 4) | ver repo | Lector WPF de CBZ/CBR/7Z. Referencia de cómo mostrar páginas de archivos comprimidos en WPF. |
| **RuliaReader/Rulia** (C#, UWP) | ver repo | Lector de manga para Windows en C#. Referencia de interfaz. |

## 8. Tercera ronda (2026-10-27): ecosistema actual y con qué conectarse

Pregunta: ¿qué existe hoy para leer libros, cómics, manga y anime, y cómo puede MOON **complementarse** con ello en vez de rehacerlo?

### 8.1 Proyectos revisados

| Proyecto | ★ | Tecnología | Licencia | Qué hace | Qué tomamos |
|---|---|---|---|---|---|
| **Readest** | ~25.7 k | Next.js + Tauri | AGPL-3.0 | El lector de libros abierto más popular de 2026: EPUB, PDF, MOBI, AZW3, FB2, CBZ. Subrayados, diccionarios locales (importa **Yomitan**), traducción (DeepL), voz, **sincroniza con KOReader**, OPDS/Calibre. IA: resúmenes "en desarrollo" | Usa **foliate-js (MIT)** para EPUB: confirma nuestra elección en S2. Importar diccionarios Yomitan. Sincronización con KOReader |
| **NeeView** | ~1.1 k | **C# + WPF** | **MIT** | Visor de imágenes y manga para Windows: zip/rar/7z/cbz/cbr/cb7 (también anidados), doble página, lupa, gestos, scripts | **Nuestra misma tecnología y licencia permisiva**: la mejor referencia de código para L2 (lector de cómics en WPF). Se puede reutilizar código citándolo |
| **Seanime** | ~4.3 k | Go + React + Electron | GPL-3.0 | Biblioteca local de **anime y manga** + lector de manga + reproductor (mpv) + **sincronización con AniList** | Idea de unir manga ↔ anime con AniList. **No** copiamos su parte de descargas/torrents/extensiones |
| **Mihon / Komikku / Kotatsu** | — | Android | Apache/GPL | Sucesores de Tachiyomi. Siguen el progreso en AniList, MyAnimeList, Kitsu, MangaUpdates… | El seguimiento de progreso es lo que la comunidad más valora. Mihon solo envía (app → AniList); Komikku sincroniza en los dos sentidos |
| **Suwayomi** | — | Kotlin (Java 17) | MPL-2.0 | Servidor que ejecuta extensiones de Tachiyomi en PC; ofrece **OPDS** | Solo como referencia de OPDS. Las extensiones dependen de sitios no oficiales: **fuera** |
| **Komga / Kavita** | — | Java / C# | MIT / GPL-3.0 | Servidores de biblioteca de cómics y libros: API REST, **OPDS**, sincronización con KOReader | Futuro: que MOON lea también desde tu propio servidor (OPDS) |
| **YACReader, MComix, CDisplayEx, Cover** | — | — | GPL / cerrado | Lectores clásicos de Windows | Lo que la gente pide en las comparativas: biblioteca, RTL, doble página, portabilidad. Queja frecuente: **el primer escaneo de bibliotecas grandes tarda horas** (generan todas las miniaturas de golpe) → generarlas poco a poco |
| **Readr** | — | iOS/Mac | MIT | "Pregunta al libro" en el margen, con citas del texto y clave de IA propia | El patrón de IA con clave propia y respuesta citada |
| **Liseur #357** | — | — | — | Propuesta (no hecha) de "pregunta sin spoilers": solo usa el texto ya leído | Exactamente la regla a seguir |
| **libre-manga-translator, CarrotMangaTranslator, OverTranslate** | — | ONNX web / escritorio | varias, GPL | Detección de globos + OCR + traducción, en local o con IA | Confirman que el OCR en local con modelos ONNX ya es práctico en 2026 |
| **asbplayer** + Yomitan + AnkiConnect | — | Extensiones | MIT | Flujo de la comunidad que aprende idiomas con anime: subtítulo → diccionario → tarjeta de Anki con un atajo | Enviar vocabulario **directo a Anki** (AnkiConnect), no solo CSV |

### 8.2 Servicios con los que conectarse

| Servicio | Para qué | Datos verificados |
|---|---|---|
| **AniList** (API GraphQL, gratis) | Ficha de la serie (portada, sinopsis, géneros), **¿tiene anime?**, actualizar "capítulo leído" en tu cuenta | 90 peticiones/min más un limitador de ráfagas. Algunas fuentes dicen 30/min "degradado" (no confirmado). Inicio de sesión OAuth. Mutación `SaveMediaListEntry(progress)` (vista en código de terceros, verificar en docs.anilist.co) |
| **trace.moe** | De un recorte de anime → qué anime, episodio y minuto | Proyecto MIT. Cuotas: ver la documentación de la API antes de implementar (la página no cargó) |
| **KOReader sync** | Progreso compartido con lectores de tinta electrónica, Readest, Kavita y Komga | Protocolo sencillo; identifica el libro por el **hash del archivo** |
| **OPDS** | Leer catálogos de Komga, Kavita, Calibre-Web y Suwayomi | Estándar abierto (v1.2 es el más soportado) |
| **AnkiConnect** | Crear tarjetas de vocabulario en Anki | Complemento de Anki; API local en `localhost:8765` |

### 8.3 OCR: novedad importante
- **RapidOcrNet** (NuGet, .NET, actualizado en mayo de 2026): modelos **PaddleOCR v5/v6 en ONNX**, en local y sin Python. Los modelos se descargan aparte. Es la alternativa más sólida si `Windows.Media.Ocr` falla sin MSIX (la documentación de Microsoft dice que necesita identidad de paquete, aunque hay quien lo usa sin ella: **S1 sigue siendo necesario**).
- Japonés de manga: manga-ocr sigue siendo el mejor, pero como complemento opcional (Python, ~400 MB).

### 8.4 Tendencias 2025-2026
1. **IA dentro del lector:** Kindle lanzó "Ask this Book" (iOS, diciembre de 2025), "Recaps" y "Story so far", todo **sin spoilers**. Polémica: los autores no pueden excluir sus libros (Authors Guild). En código abierto aún no hay una versión terminada → **hueco** para MOON, con clave propia, en local y solo con lo ya leído.
2. **Lectores en Tauri/web** (Readest) y sincronización abierta (KOReader) en lugar de nubes cerradas.
3. **OCR y traducción de manga en local** con ONNX: ya no hace falta GPU ni Python.
4. **Aprender idiomas con anime y manga** (Yomitan, asbplayer, Anki): un público grande y muy activo.
5. **Unir anime y manga** con AniList (Seanime, Mihon): seguir el progreso es lo que más pide la comunidad.

### 8.5 Lo que NO hacemos (decidido)
- Descargar o "fuentes"/extensiones de sitios de manga o anime (Mihon, Suwayomi, Seanime): riesgo legal y de mantenimiento. MOON lee **tus archivos**.
- Reproducir anime: Seanime, mpv y VLC ya lo hacen bien. MOON solo **enlaza** (AniList, trace.moe).

### 8.6 Enlaces de esta ronda
- Readest — https://github.com/readest/readest
- NeeView — https://github.com/neelabo/NeeView
- Seanime — https://github.com/5rahim/seanime
- Suwayomi — https://github.com/Suwayomi/Suwayomi-Server
- Komga — https://github.com/gotson/komga
- Mihon, seguimiento — https://mihon.app/docs/guides/tracking
- AniList, límites — https://docs.anilist.co/guide/rate-limiting
- trace.moe — https://github.com/soruly/trace.moe
- RapidOcrNet — https://www.nuget.org/packages/RapidOcrNet
- libre-manga-translator — https://github.com/mrdhnto/libre-manga-translator
- Readest + KOReader — https://github.com/readest/readest/wiki/Sync-with-Koreader-devices
- Liseur, propuesta sin spoilers — https://github.com/chmouel/liseur/issues/357
- Kindle Ask this Book — https://9to5mac.com/2025/12/15/kindle-ask-this-book-ai-powered-feature-launches-in-ios-app/
- Authors Guild — https://authorsguild.org/news/statement-on-amazon-kindle-ask-this-book-ai-feature/
- asbplayer y Anki — https://docs.asbplayer.dev/docs/guides/mining-in-depth
- Windows.Media.Ocr — https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr

### 8.7 Decisiones tomadas tras esta ronda (2026-10-27)
- **Sin anime:** quedan fuera AniList, trace.moe y KOReader/OPDS por ahora. Solo leer manga, cómics y libros.
- **Inglés → español:** diccionario y traducción EN→ES; el OCR solo necesita inglés (Windows OCR o RapidOcrNet).
- **Sin cuentas:** vocabulario exportado a CSV (Anki puede importarlo a mano); nada de AnkiConnect.
- **IA más adelante** (L13 del ROADMAP), con clave propia y "sin spoilers".

## 9. Lectura en voz alta gratis (2026-10-27)

| Opción | Coste | Sin internet | Calidad | En C# | Notas |
|---|---|---|---|---|---|
| **Voces de Windows** (`Windows.Media.SpeechSynthesis`) | Gratis, ya instaladas | Sí | Robótica pero clara | Directo (WinRT) | Da los límites de palabra → resaltar palabra a palabra |
| **Piper** (rhasspy) | Gratis | Sí | Buena; voces es_ES, es_MX, inglés | Vía **sherpa-onnx** (tiene ejemplos .NET) o `piper.exe` | ~60 MB por voz, muy rápido en CPU. Licencia distinta por voz (MODEL_CARD) |
| **Kokoro-82M** | Gratis | Sí | Muy buena en inglés; español disponible (voces Dora y Alex) | **KokoroSharp** (NuGet, MIT; modelo Apache-2.0) | ~320 MB. Da marcas de tiempo por fonema |
| NaturalVoiceSAPIAdapter | Gratis | Solo con voces online | Excelente | SAPI | Las voces naturales del Narrador **dejaron de funcionar** con el adaptador; queda la vía online |
| Edge TTS | "Gratis" | No | Excelente | — | Servicio de Microsoft sin permiso oficial; envía el texto. **No lo usamos** |

**Quién ya lo hace:**
- **Readest** (AGPL): motor por defecto Edge TTS (en la nube); también voces del sistema y servidores compatibles con OpenAI (p. ej. Kokoro local). Copiamos sus **funciones**:
  - resaltar por palabra o por frase;
  - velocidad de 0,5× a 3×;
  - temporizador para dormir;
  - pausa tras cada frase;
  - repetir A-B;
  - controles multimedia del sistema;
  - acompañar un EPUB con su audiolibro.
- **Audiblez** (Kokoro) y **ebook2audiobook** (Piper, XTTS…): convierten un EPUB entero en audiolibro, no leen en vivo. Idea para "exportar capítulo".
- **OpenReader WebUI**: lector con voz que puede usar un servidor de Kokoro.

**Ventaja que solo tenemos nosotros:** si MOON se registra en los controles multimedia de Windows (SMTC), **NOX** mostrará el libro que se escucha con ⏯, sin cambiar nada en NOX.

Enlaces:
- KokoroSharp — https://github.com/Lyrcaxis/KokoroSharp
- sherpa-onnx — https://github.com/k2-fsa/sherpa-onnx
- Piper — https://github.com/rhasspy/piper
- NaturalVoiceSAPIAdapter — https://github.com/gexgd0419/NaturalVoiceSAPIAdapter
- Readest, voz — https://readest.com/docs/listen
- ebook2audiobook — https://sourceforge.net/projects/ebook2audiobook.mirror/

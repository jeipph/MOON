# 02 — Arquitectura

## Decisión principal: WPF + WebView2 para libros
- **Cómics:** se dibujan con WPF nativo (imágenes decodificadas por nosotros). Es rápido y controlamos memoria y zoom.
- **EPUB (y PDF):** se muestran dentro de **WebView2** (el navegador Chromium que ya trae Windows 11) con una librería JavaScript de lectura. Ventaja: selección de texto, tipografía y resaltado ya resueltos. Desventaja: dos mundos (C# y JavaScript) que se comunican por mensajes.
- Alternativa descartada por ahora: dibujar el texto del EPUB a mano en WPF (mucho trabajo) o usar Avalonia (PaperBunkr lo usa; aquí preferimos WPF para compartir temas con el Programa 1).

## Tecnología (todo por verificar en el prototipo correspondiente)

| Necesidad | Opción propuesta | Licencia (verificar) | Notas |
|---|---|---|---|
| UI | WPF + MVVM (`CommunityToolkit.Mvvm`) | MIT | Igual que el Programa 1 |
| Archivos comprimidos (CBZ/CBR/CB7) | **SharpCompress** | MIT | La usa EzManga para ZIP, RAR, 7Z |
| Decodificar imágenes | WPF `BitmapDecoder`/`BitmapImage` (+ WebP: códec de Windows o `SkiaSharp`) | — | `DecodePixelWidth` para ahorrar RAM |
| EPUB | **epub.js** o **foliate-js** dentro de WebView2 | BSD-2 / MIT (verificar) | Ver prototipo S2. Para metadatos/portada en C#: `VersOne.Epub` |
| PDF | **PDF.js** en WebView2 *o* Edge/WebView2 visor nativo | Apache-2.0 (verificar) | Alternativa: PDFium. Decidir en el hito de PDF |
| Base de datos local | **SQLite** con `Microsoft.Data.Sqlite` (+ Dapper opcional) | MIT / dominio público | Biblioteca, progreso, anotaciones, vocabulario |
| OCR (opción 1) | **Windows.Media.Ocr** (OCR de Windows, sin internet) | — | **Riesgo:** su documentación dice que requiere identidad de paquete (MSIX). Prototipo S1. Los idiomas dependen de los paquetes de OCR instalados en Windows |
| OCR (opción 2) | **Tesseract** (NuGet `Tesseract`) | Apache-2.0 | Bueno en texto impreso; peor en tipografías de cómic |
| OCR (opción 3, manga japonés) | **manga-ocr** (Python, ~400 MB) | Apache-2.0 | Muy bueno con manga japonés. Sin versión C#/ONNX confirmada → solo como proceso externo opcional |
| Diccionario | Interfaz `IDictionarySource` con varias implementaciones | — | Ver más abajo |
| Traducción | Interfaz `ITranslator` | — | Ver más abajo |
| IA con visión (opcional) | API de la persona usuaria (Claude u otras) | — | Clave cifrada con DPAPI |
| Pruebas | xUnit | Apache-2.0 | |

## Estructura de carpetas

```
MOON/
├─ AGENTS.md  README.md  THIRD-PARTY-NOTICES.md
├─ docs/
├─ Moon.sln
├─ src/
│  ├─ Moon.Core/                 (net10.0, sin WPF ni Windows)
│  │  ├─ Models/        Book, ComicPage, Annotation, Highlight, VocabEntry, ReadingProgress
│  │  ├─ Abstractions/  IBookSource, IPageProvider, IAnnotationStore, IDictionarySource,
│  │  │                 ITranslator, IOcrEngine, IReverseImageSearch
│  │  ├─ Comics/        NaturalSort.cs, ReadingDirection.cs, SpreadLayout.cs (cálculo de dobles páginas)
│  │  ├─ Annotations/   NormalizedRect.cs, AnnotationExporter.cs (Markdown/CSV)
│  │  └─ Vocabulary/    Lemmatizer(simple).cs, VocabularyService.cs
│  ├─ Moon.Windows/              (net10.0-windows10.0.19041.0)
│  │  ├─ Archives/      ArchivePageProvider.cs (SharpCompress), FolderPageProvider.cs
│  │  ├─ Ocr/           WindowsOcrEngine.cs, TesseractOcrEngine.cs
│  │  ├─ Dictionary/    FreeDictionarySource.cs, LocalMdxSource.cs (futuro)
│  │  ├─ Storage/       SqliteAnnotationStore.cs, SqliteLibrary.cs, SecretStore.cs (DPAPI)
│  │  └─ Web/           ReverseSearchLauncher.cs (abre URL en el navegador)
│  └─ Moon.App/                  (WPF)
│     ├─ Windows/       MainWindow, ReaderWindow, SettingsWindow
│     ├─ Views/         LibraryView, ComicReaderView, BookReaderView(WebView2), AnnotationPanel,
│     │                 LookupPopup, VocabularyView, RegionSelectorOverlay
│     ├─ ViewModels/
│     ├─ Themes/        Tokens.Dark/Light/Sepia.xaml (mismo esquema que el Programa 1)
│     └─ Web/           reader.html, reader.js, (epub.js / foliate-js), reader.css
└─ tests/Moon.Core.Tests/
```

## Datos en disco
| Qué | Dónde |
|---|---|
| Base de datos (biblioteca, progreso, anotaciones, vocabulario) | `%APPDATA%\MOON\moon.db` (SQLite) |
| Portadas y miniaturas en caché | `%LOCALAPPDATA%\MOON\covers\` |
| Ajustes | `%APPDATA%\MOON\settings.json` |
| Claves de API (cifradas con DPAPI) | `%APPDATA%\MOON\secrets.bin` |
| Exportaciones | carpeta elegida por la persona (ej. `Documentos\MOON\export\`) |

Los archivos originales **no se tocan**. Se identifican por **hash** (SHA-256 de los primeros MB + tamaño) además de la ruta, para que las anotaciones sobrevivan si el archivo se mueve.

## Modelo de datos (SQLite, borrador)
```
Book(id, hash, path, title, series, number, type[comic|epub|pdf], addedAt, coverPath)
Progress(bookId, position, percent, updatedAt)           -- position: página o CFI
Annotation(id, bookId, kind[highlight|note|bookmark|region], color, createdAt,
           anchor TEXT, text TEXT, note TEXT)
   anchor:
     - comic/imagen:  {"page":12,"x":0.31,"y":0.18,"w":0.22,"h":0.10}   (valores 0–1, no píxeles)
     - EPUB:          CFI (ej. "epubcfi(/6/14!/4/2/1:0)")
     - PDF:           {"page":3,"quads":[...]}
VocabEntry(id, bookId, word, lemma, definition, context, page, createdAt, reviewedAt, status)
Lookup(id, bookId, source, query, result, createdAt)     -- historial/caché de consultas
```
**Por qué coordenadas 0–1:** el rectángulo sigue en su sitio aunque cambie el zoom o el tamaño de ventana.

## Cómo funciona cada pieza

### Lector de cómics
1. `ArchivePageProvider` abre el archivo con SharpCompress y lista las entradas de imagen en **orden natural**; ignora `__MACOSX`, `Thumbs.db` y entradas que no sean imágenes.
2. Cada página se decodifica **bajo demanda** en un hilo de fondo (`Task.Run`), con `DecodePixelWidth` según el tamaño de pantalla. Se mantiene una ventana de ±3 páginas en memoria.
3. `SpreadLayout` (Core, con pruebas) decide cuándo mostrar doble página y cuándo una página ancha va sola; respeta RTL/LTR.
4. Modo webtoon: `ScrollViewer` con **virtualización** (solo se crean las páginas visibles); altura estimada hasta decodificar.
5. Zoom con la rueda y arrastre con el ratón; atajos de teclado.

### Lector de libros (EPUB)
1. `BookReaderView` carga `reader.html` en WebView2 y le pasa el archivo (por un servidor virtual local o `SetVirtualHostNameToFolderMapping`).
2. `reader.js` usa la librería de EPUB, pagina o desplaza, y envía a C# mensajes JSON con `window.chrome.webview.postMessage`: `{type:"selection", text, cfi}`, `{type:"progress", cfi, percent}`.
3. C# responde con `PostWebMessageAsJson`: `{type:"applyHighlights", items:[{cfi,color}]}`.
4. Las anotaciones se guardan en SQLite desde C# (JavaScript no escribe a disco).
5. Seguridad: desactivar navegación externa, bloquear scripts del libro que no sean del lector, no permitir acceso a archivos fuera de la carpeta del libro.

### Subrayar y anotar
- `IAnnotationStore` (Core) con implementación SQLite.
- Cómics/imágenes: una capa `Canvas` transparente sobre la página; al arrastrar con la herramienta de resaltado se crea un `NormalizedRect` que se guarda y se dibuja.
- Exportar: `AnnotationExporter` genera Markdown por libro:
  ```
  # Título del libro
  ## Subrayados
  > "texto subrayado" — p. 12  #amarillo
  Nota: ...
  ```

### Buscar una palabra (diccionario)
Flujo: selección → `LookupService.LookupAsync(word, language)` → recorre las fuentes configuradas en orden (primero locales, luego en línea) → muestra la primera con resultado → botón "Guardar en vocabulario".
Fuentes posibles (cada una implementa `IDictionarySource`):
| Fuente | Tipo | Notas |
|---|---|---|
| Diccionarios locales `.mdx` | Offline | Koodo Reader los admite; formato conocido pero requiere lector propio (por investigar) |
| JMdict / Jitendex (japonés→inglés) | Offline | Licencia CC BY-SA / AGPL en el caso de Jitendex → usar como **datos descargados por la persona**, no incluidos |
| Free Dictionary API (dictionaryapi.dev) | En línea, sin clave | Inglés. Su mantenedor advierte que el servicio es costoso de mantener → puede caer; no depender de él |
| Wiktionary (API de MediaWiki) | En línea | Cubre muchos idiomas; formato de respuesta complejo. **No verificado en esta investigación** |
| RAE (español) | — | **No se confirmó una API pública gratuita.** Verificar en el hito de diccionarios |
| IA (clave propia) | En línea | Definición en contexto, explicación de frases; opcional |

### Traducción
`ITranslator` con implementaciones opcionales: LibreTranslate/Argos (servidor local propio, AGPL, se usa solo por HTTP), un traductor en línea con clave de la persona, o una IA con su clave. Por defecto: ninguna activa.

### OCR de una región del cómic (idea clave)
1. La persona pulsa la herramienta "Leer zona" y arrastra un rectángulo sobre la página (`RegionSelectorOverlay`).
2. Se recorta esa zona del bitmap original (no de la pantalla) y se amplía/binariza si hace falta.
3. `IOcrEngine.RecognizeAsync(bitmap, language)` devuelve texto con líneas y palabras.
4. El texto se muestra en un panel editable (el OCR falla a veces) y desde ahí se busca la palabra o se traduce.
5. Idioma: se elige en la barra. Con `Windows.Media.Ocr`, solo los idiomas instalados (`OcrEngine.AvailableRecognizerLanguages`); si falta, ofrecer instalar el paquete de OCR de Windows (no confirmado el método exacto: **prototipo S1**).
6. Si Windows OCR no sirve para manga (texto vertical, tipografías), alternativas: Tesseract (texto horizontal) o manga-ocr como proceso externo (japonés).

### "¿Qué es esta imagen?"
- `ReverseSearchLauncher`: construye la URL de búsqueda y abre el navegador. **Limitación técnica:** casi todos estos servicios necesitan que subas la imagen o que esté en una URL pública; abrir una página con la imagen ya subida no es trivial. Plan: copiar el recorte al portapapeles y abrir la página del servicio para que la persona lo pegue (Ctrl+V). Es manual pero privado y sin claves.
- Opcional: **trace.moe** (capturas de anime) y **SauceNAO** (ilustraciones) tienen API; ambas con clave/cuota gratuita limitada (la cuota de trace.moe cambió en 2025: ver `04-RESEARCH.md`). Se implementan detrás de `IReverseImageSearch`.
- Opcional: **IA con visión** con la clave propia: se envía el recorte y la pregunta "¿qué es esto?". Aviso de privacidad antes del primer uso.

### Vocabulario
`VocabularyService`: guarda la palabra, su lema (forma base; para inglés una lematización simple, para japonés se necesita un analizador morfológico → futuro), la frase de contexto, el libro y la página. Vista de lista con filtro y estados (nueva/aprendiendo/sabida). Exportación CSV pensada para Anki.

### Ajustes y temas
Igual que el Programa 1: tokens en `Themes/Tokens.*.xaml`, `AppSettings` en JSON. Añadir tema **Sepia** para lectura prolongada.

## Pruebas
- xUnit en Core: orden natural, `SpreadLayout`, `NormalizedRect`, `AnnotationExporter`, lematizador.
- `FakeBookSource` y `FakeOcrEngine` para probar la interfaz sin archivos reales.
- Cómics de prueba: crear un CBZ propio con imágenes de dominio público/generadas (no usar material con derechos).

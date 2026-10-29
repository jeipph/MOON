# MOON — guía rápida

Lector de **libros, cómics y manga** para Windows 10/11. Funciona sin internet y sin cuentas.

## Qué abre
- **Libros:** EPUB, MOBI, AZW3, FB2 y PDF «como documento» (texto seleccionable).
- **Cómics y manga:** CBZ, CBR, CB7 y PDF «como imágenes».
  En un PDF, clic derecho → elige cómo abrirlo; MOON lo recuerda.

## Tu biblioteca
- **Añadir:** arrastra archivos a la ventana (se copian a `Documentos\MOON\Biblioteca`).
- **Enlazar carpeta o disco externo:** los libros se quedan donde están; al conectar el disco aparecen solos.
- Teclado: **Ctrl+F** buscar · **Esc** borrar búsqueda · **F5** revisar carpetas ·
  **flechas** moverse entre libros · **Enter** abrir · **Mayús+F10** menú del libro.

## Leyendo
- **Subrayar y notas:** selecciona texto y elige color; las notas se exportan desde su panel.
- **Diccionario inglés → español:** clic en una palabra; «Guardar palabra» la manda al vocabulario
  (se puede exportar para **Anki**).
- **Cómics:** «Leer texto» recuadra un bocadillo y lo pasa a texto (OCR de Windows) para usar el diccionario.
  Para cómics en inglés, instala el idioma inglés del OCR (el panel copia el comando).

## Leer en voz alta
- Pulsa 🔊 **Escuchar** en un libro. Sin nada más, usa la voz de Windows.
- **Voz natural (opcional):** necesita tarjeta gráfica NVIDIA y unos 6 GB. En PowerShell:
  `powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\MOON\tools\instalar-voz.ps1"`
  y pon audios tuyos de 10–15 s (.wav o .mp3) en `%APPDATA%\MOON\voces`.
- Si tienes **NOX**, muestra el libro que suena con sus controles.

## Dónde se guardan tus cosas
- Biblioteca, notas y ajustes: `%APPDATA%\MOON` · Portadas: `%LOCALAPPDATA%\MOON\covers`
- Errores (para avisar de un fallo): `%LOCALAPPDATA%\MOON\logs\error.log`
- Al desinstalar **no** se borran.

## Aviso al instalar
El instalador no tiene firma digital: Windows puede mostrar «Windows protegió tu PC».
Pulsa **Más información → Ejecutar de todas formas**.

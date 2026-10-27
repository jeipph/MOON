# 01 — Visión y funciones

## En una frase
Un lector cómodo y minimalista (blanco/negro, tema cambiable) donde abres un cómic, manga o libro, subrayas, anotas, y cuando ves una palabra o imagen que no conoces, la seleccionas y la app te dice qué es.

## Casos de uso
1. Leo un manga CBZ de derecha a izquierda; veo una frase que no entiendo → dibujo un rectángulo sobre el globo → la app lee el texto (OCR) → muestra definición/traducción → guardo la palabra.
2. Leo un EPUB; subrayo un párrafo en amarillo y escribo una nota; luego exporto mis subrayados a Markdown.
3. Veo un dibujo/personaje/objeto que no conozco → lo selecciono → "¿Qué es esto?" → abre una búsqueda inversa de imagen o consulta a una IA (con mi propia clave) tras mi confirmación.
4. Tengo una biblioteca de cientos de archivos; veo portadas, continúo donde lo dejé.

## Funciones, agrupadas

### A. Lectura (núcleo)
- Formatos de cómic: **CBZ, ZIP, CBR, RAR, CB7/7Z** y carpetas de imágenes (JPG, PNG, WebP).
- Libros: **EPUB**. PDF.
- Modos de cómic: una página, doble página, desplazamiento vertical continuo (webtoon); dirección izquierda→derecha y derecha→izquierda; ajuste a ancho/alto; zoom 50–300 %; orden natural de páginas (Página 2 antes que 10).
- Modo libro: tamaño y tipo de letra, interlineado, márgenes, tema claro/oscuro/sepia.
- Pantalla completa con barras que se ocultan; atajos de teclado; recordar la última página.

### B. Biblioteca
- Carpetas vigiladas, portadas generadas, búsqueda, favoritos, colecciones, estado (leyendo/terminado).
- Metadatos: leer `ComicInfo.xml` de los CBZ (título, serie, número); editar a mano.
- Progreso guardado por archivo.

### C. Subrayar y anotar
- **Libros/EPUB y PDF:** seleccionar texto → resaltar en 4–5 colores → añadir nota.
- **Cómics:** resaltar un **rectángulo** (globo de texto, personaje, objeto) y ponerle nota/etiqueta.
- Marcadores de página.
- Panel lateral con todas las anotaciones del libro, con salto a la página.
- **Exportar** a Markdown (carpeta compatible con Obsidian/Morrow) y CSV.

### D. Buscar palabras
- Seleccionar o hacer doble clic en una palabra (libro/PDF) o en el texto reconocido por OCR (cómic) → ventana flotante con definición.
- Fuentes de diccionario conectables (ver arquitectura): offline primero, en línea opcional.
- **Lista de vocabulario**: guardar palabra + frase de contexto + página + libro; revisar; exportar a CSV (compatible con Anki).
- Traducción de la frase seleccionada (opcional).

### E. Buscar imágenes o cosas desconocidas
- Seleccionar una zona de la página (recorte).
- Acciones: copiar al portapapeles; abrir en **búsqueda inversa** (Google Lens, TinEye, SauceNAO para ilustraciones, trace.moe para capturas de anime) en el navegador; consultar a una **IA con visión** usando la clave de la persona usuaria; guardar el recorte con nota en la biblioteca de "Cosas por investigar".
- Siempre con aviso de qué se sube y confirmación.

### F. Comodidad
- Tema claro/oscuro/sistema/sepia; filtro de luz cálida y brillo/contraste/gamma/"quitar amarillo" para páginas escaneadas (idea de PaperBunkr y YACReader).
- Portada y autor de libros desde **Open Library** (sin cuenta), solo cuando se pide.

### G. IA en tu PC, sin internet (opcional, al final)
- **Mejorar nitidez** de páginas de baja resolución (escalado con IA, estilo Upscayl).
- **Búsqueda por significado** en subrayados, notas y vocabulario.
- **Lectura en voz alta** de libros, gratis y sin internet: voces de Windows, o voces neuronales descargables (Piper, Kokoro). Resalta lo que se lee, velocidad, temporizador para dormir y controles multimedia (se ve en NOX). Ver ROADMAP L5b.
- Recordatorio de racha y estadísticas de lectura — opcional.

## MVP (v1) sugerido
1. Abrir CBZ/CBR/carpetas en los tres modos y dirección RTL/LTR.
2. Biblioteca simple con portadas y progreso.
3. Abrir EPUB con subrayado y notas.
4. Diccionario para una palabra seleccionada (una fuente) + lista de vocabulario.
5. Tema claro/oscuro.
Después: OCR por zona, anotaciones en cómics, PDF, búsqueda inversa, exportaciones.

## Ideas aparcadas (de la versión anterior del Programa 2)
Estas venían de OrbitDrop y Morrow y pueden formar un futuro **Programa 3**:
- Rueda radial al arrastrar archivos + Shift para convertirlos (imágenes, audio/video, PDF, ZIP).
- Menú contextual del Explorador.
- Notas Markdown con bases de datos y grafo.
- Servidor MCP para que Claude lea las notas.
Cómo funcionan por dentro OrbitDrop y Morrow: ver el análisis inicial de la conversación (OrbitDrop: C#/WPF, Win32/COM, WIC, Media Foundation, PDFsharp 6.2.4; Morrow: Electron, Tiptap, archivos `.md`, MCP).

## Principios
Minimalista, cómodo para leer horas, privado, rápido con archivos grandes, nunca modificar el original.

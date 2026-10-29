# Avisos de terceros — MOON

MOON usa estas bibliotecas. Todas permiten usarlas en un programa propio; aquí se citan como piden sus licencias.

| Biblioteca | Para qué | Licencia | Copyright |
|---|---|---|---|
| [foliate-js](https://github.com/johnfactotum/foliate-js) (commit `78914ae`, en `src/Moon.App/Web/foliate-js`) | Mostrar EPUB, MOBI, AZW3, FB2 y CBZ | MIT | © 2022 John Factotum |
| [zip.js](https://github.com/gildas-lormeau/zip.js) (2.7.x, dentro de foliate-js `vendor/zip.js`) | Leer archivos ZIP en el lector | BSD-3-Clause | © 2023 Gildas Lormeau |
| [PDF.js](https://github.com/mozilla/pdf.js) 5.5.207 (dentro de foliate-js `vendor/pdfjs`, con sus `cmaps` y `standard_fonts`) | Mostrar PDF con texto seleccionable | Apache-2.0 | © Mozilla Foundation |
| [fflate](https://github.com/101arrowz/fflate) (0.8.x, dentro de foliate-js `vendor/fflate.js`) | Descomprimir fuentes de libros Kindle | MIT | © 2023 Arjun Barrett |
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) 1.0.4314.35 | El "navegador" interno donde se ve la página | Licencia BSD de Microsoft (ver el paquete) | © Microsoft Corporation |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) 0.50.7 | Abrir cómics CBR (RAR) y CB7 (7Z) | MIT | © 2014 Adam Hathcock |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) 10.0.14 | Base de datos de la biblioteca | MIT | © .NET Foundation and Contributors |
| [Apertium eng-spa](https://github.com/apertium/apertium-eng-spa) (diccionario bilingüe, convertido a `Data/en-es.tsv`) | Traducciones inglés → español | GPL-2.0 (solo el archivo de datos; MOON lo lee pero no lo modifica) | © Proyecto Apertium y colaboradores |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MVVM | MIT | © .NET Foundation and Contributors |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.14 | Unir las piezas de la app | MIT | © .NET Foundation and Contributors |
| [.NET 10](https://github.com/dotnet/runtime) (incluido en el instalador) | Para que MOON funcione sin instalar .NET | MIT | © .NET Foundation and Contributors |

Los textos completos de las licencias están en cada repositorio enlazado (foliate-js incluye el suyo en `Web/foliate-js/LICENSE`).

Prototipos (carpeta `spikes/`, no forman parte de MOON): Piper (MIT), KokoroSharp (MIT; modelo Kokoro-82M Apache-2.0), Chatterbox (MIT, con marca de agua Perth de Resemble AI).

Voz natural (opcional, **no va en el instalador**): `tools/instalar-voz.ps1` descarga [Chatterbox](https://github.com/resemble-ai/chatterbox) (MIT, con marca de agua Perth de Resemble AI) y PyTorch (BSD-3-Clause) en el PC de cada persona. Las voces de referencia las pone cada persona; MOON no trae ninguna.

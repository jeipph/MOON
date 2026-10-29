# Cómo generar el instalador de MOON

Requisito (una vez): Inno Setup 6, instalado para el usuario con
`winget install --id JRSoftware.InnoSetup -e --scope user` (no pide administrador).

Desde `C:\Dev\MOON`, en una terminal:

```
# 1) Publicar MOON con .NET incluido (funciona en PCs sin .NET instalado)
dotnet publish src/Moon.App -c Release -r win-x64 --self-contained true -o installer/out/app -p:SatelliteResourceLanguages=es -p:DebugType=none

# 2) Crear el instalador
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\moon.iss
```

Resultado: `dist\MOON-<versión>-instalador.exe` (un solo archivo para enviar).

Para una versión nueva: cambiar `<Version>` en `src/Moon.App/Moon.App.csproj` y `AppVersion` en `installer/moon.iss`
(los dos iguales). **No cambiar `AppId`**: así el instalador nuevo actualiza el que ya está instalado.

Notas:
- El instalador no tiene firma digital: Windows SmartScreen avisará ("Windows protegió tu PC").
  Hay que pulsar **Más información → Ejecutar de todas formas**. Es normal en programas personales.
- `installer/out/` y `dist/` no se guardan en git (se regeneran).

# Instala el motor de voz natural de MOON (Chatterbox) para el usuario actual.
# Necesita una tarjeta gráfica NVIDIA y unos 6 GB libres. Sin esto, MOON usa la voz de Windows.
#
#   powershell -ExecutionPolicy Bypass -File tools\instalar-voz.ps1
#
# Qué hace:
#   1. Instala "uv" (gestor de Python) para tu usuario, si no lo tienes.
#   2. Crea %LOCALAPPDATA%\MOON\voz\.venv con Python 3.11, PyTorch 2.8 (CUDA 12.8) y Chatterbox.
#   3. Crea la carpeta de voces %APPDATA%\MOON\voces (pon ahí audios de 10–15 s, .wav o .mp3).
$ErrorActionPreference = 'Stop'

$uv = (Get-Command uv -ErrorAction SilentlyContinue).Source
if (-not $uv) {
    Write-Host 'Instalando uv…'
    winget install --id astral-sh.uv -e --scope user --accept-source-agreements --accept-package-agreements --disable-interactivity
    $uv = "$env:LOCALAPPDATA\Microsoft\WinGet\Links\uv.exe"
}

$voz = Join-Path $env:LOCALAPPDATA 'MOON\voz'
New-Item -ItemType Directory -Force $voz | Out-Null
Set-Location $voz
& $uv venv --python 3.11 .venv
# chatterbox-tts pide PyTorch 2.6, que no funciona en las tarjetas RTX 50: se usa la 2.8.
'torch==2.8.0', 'torchaudio==2.8.0' | Set-Content overrides.txt
& $uv pip install --python .venv\Scripts\python.exe chatterbox-tts 'setuptools<81' --override overrides.txt `
    --index-strategy unsafe-best-match --extra-index-url https://download.pytorch.org/whl/cu128
& .venv\Scripts\python.exe -c "import torch; print('Tarjeta gráfica:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'NO (irá muy lento)')"

$voces = Join-Path $env:APPDATA 'MOON\voces'
New-Item -ItemType Directory -Force $voces | Out-Null
Write-Host "Listo. Pon tus voces (audios de 10–15 s) en: $voces"

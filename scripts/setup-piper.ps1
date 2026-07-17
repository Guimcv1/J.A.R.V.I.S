# ═══════════════════════════════════════════════════════════════════════════════
#  setup-piper.ps1 — Instala o Piper TTS para o Jarvis (Windows x64)
#
#  Uso (PowerShell como Administrador, ou com política de execução ajustada):
#    Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
#    .\scripts\setup-piper.ps1
#
#  Dependência de reprodução de áudio: ffplay (parte do ffmpeg)
#    winget install ffmpeg
# ═══════════════════════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"

# ── Configuração ─────────────────────────────────────────────────────────────

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

$PiperDir  = Join-Path $ProjectRoot "Jarvis.Console\bin\Debug\net10.0\piper"
$ModelsDir = Join-Path $PiperDir "models"

$PiperVersion = "2023.11.14-2"
$PiperUrl     = "https://github.com/rhasspy/piper/releases/download/$PiperVersion/piper_windows_amd64.zip"

$VoiceName    = "en_GB-alan-medium"
$VoiceQuality = "medium"
$HFBase       = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0"
$VoiceUrl     = "$HFBase/en/en_GB/$VoiceName/$VoiceQuality/$VoiceName.onnx"
$ConfigUrl    = "$HFBase/en/en_GB/$VoiceName/$VoiceQuality/$VoiceName.onnx.json"

# ── Helpers ───────────────────────────────────────────────────────────────────

function Write-Info    { param($msg) Write-Host "[INFO] $msg"  -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "[OK]   $msg"  -ForegroundColor Green }
function Write-Warn    { param($msg) Write-Host "[AVISO] $msg" -ForegroundColor Yellow }

# ── Setup ─────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         Jarvis — Setup do Piper TTS              ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

New-Item -ItemType Directory -Force -Path $PiperDir  | Out-Null
New-Item -ItemType Directory -Force -Path $ModelsDir | Out-Null

# ── 1. Baixar binário Piper ───────────────────────────────────────────────────

$PiperExe = Join-Path $PiperDir "piper.exe"

if (Test-Path $PiperExe) {
    Write-Success "Binário Piper já encontrado em $PiperExe"
} else {
    Write-Info "Baixando Piper $PiperVersion para Windows..."
    $TmpZip = Join-Path $env:TEMP "piper_windows.zip"
    Invoke-WebRequest -Uri $PiperUrl -OutFile $TmpZip -UseBasicParsing
    Write-Info "Extraindo..."
    Expand-Archive -Path $TmpZip -DestinationPath $PiperDir -Force
    Remove-Item $TmpZip

    # O zip do piper às vezes cria uma subpasta "piper/"
    $SubPiperExe = Join-Path $PiperDir "piper\piper.exe"
    if ((Test-Path $SubPiperExe) -and (-not (Test-Path $PiperExe))) {
        Move-Item (Join-Path $PiperDir "piper\*") $PiperDir
        Remove-Item (Join-Path $PiperDir "piper") -Recurse -ErrorAction SilentlyContinue
    }

    Write-Success "Piper instalado em $PiperExe"
}

# ── 2. Baixar modelo de voz ───────────────────────────────────────────────────

$OnnxFile = Join-Path $ModelsDir "$VoiceName.onnx"
$JsonFile = Join-Path $ModelsDir "$VoiceName.onnx.json"

if ((Test-Path $OnnxFile) -and (Test-Path $JsonFile)) {
    Write-Success "Modelo de voz já encontrado: $VoiceName"
} else {
    Write-Info "Baixando modelo '$VoiceName' (~60MB)..."
    Invoke-WebRequest -Uri $VoiceUrl   -OutFile $OnnxFile -UseBasicParsing
    Invoke-WebRequest -Uri $ConfigUrl  -OutFile $JsonFile -UseBasicParsing
    Write-Success "Modelo instalado em $ModelsDir"
}

# ── 3. Verificar ffplay ───────────────────────────────────────────────────────

$FfplayAvailable = $null -ne (Get-Command ffplay -ErrorAction SilentlyContinue)
if (-not $FfplayAvailable) {
    Write-Warn "ffplay não encontrado — necessário para reprodução de áudio no Windows."
    Write-Warn "Instale com: winget install ffmpeg"
    Write-Warn "Ou baixe em: https://ffmpeg.org/download.html"
}

# ── 4. Teste de síntese ───────────────────────────────────────────────────────

Write-Info "Testando síntese de voz..."
$TestWav = Join-Path $env:TEMP "jarvis_test.wav"

"Jarvis online. Piper text to speech is working correctly." `
    | & $PiperExe --model $OnnxFile --output_file $TestWav 2>$null

if ((Test-Path $TestWav) -and (Get-Item $TestWav).Length -gt 0) {
    Write-Success "Síntese OK!"
    if ($FfplayAvailable) {
        Write-Info "Reproduzindo áudio de teste..."
        & ffplay -autoexit -nodisp -loglevel quiet $TestWav
    } else {
        Write-Warn "Arquivo de teste salvo em: $TestWav (reproduza manualmente)"
    }
    Remove-Item $TestWav -ErrorAction SilentlyContinue
} else {
    Write-Host "[ERRO] Síntese falhou — arquivo WAV não gerado." -ForegroundColor Red
    exit 1
}

# ── Resumo ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Instalação concluída com sucesso!               ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Binário : $PiperExe"
Write-Host "  Modelo  : $OnnxFile"
Write-Host ""

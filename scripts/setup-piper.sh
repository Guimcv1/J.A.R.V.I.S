#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════════
#  setup-piper.sh — Instala o Piper TTS para o Jarvis (Linux x86_64)
#
#  O que este script faz:
#   1. Baixa o binário piper para scripts/../piper/
#   2. Baixa o modelo de voz en_GB-alan-medium (britânico masculino, grave)
#   3. Testa a instalação gerando um áudio de exemplo
#
#  Uso:
#   chmod +x scripts/setup-piper.sh
#   ./scripts/setup-piper.sh
#
#  Dependência de reprodução de áudio: aplay (pacote alsa-utils)
#   Arch:   sudo pacman -S alsa-utils
#   Ubuntu: sudo apt install alsa-utils
# ═══════════════════════════════════════════════════════════════════════════════

set -euo pipefail

# ── Configuração ─────────────────────────────────────────────────────────────

# Raiz do projeto (pasta pai de scripts/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

PIPER_DIR="$PROJECT_ROOT/Jarvis.Console/bin/Debug/net10.0/piper"
MODELS_DIR="$PIPER_DIR/models"

# Versão do Piper a baixar
PIPER_VERSION="2023.11.14-2"
PIPER_URL="https://github.com/rhasspy/piper/releases/download/${PIPER_VERSION}/piper_linux_x86_64.tar.gz"

# ── Configuração de Vozes ─────────────────────────────────────────────────────

HUGGINGFACE_BASE="https://huggingface.co/rhasspy/piper-voices/resolve/main"

# Voz 1: Inglês Britânico (en_GB-alan-medium)
VOICE_EN="en_GB-alan-medium"
URL_EN_ONNX="${HUGGINGFACE_BASE}/en/en_GB/alan/medium/${VOICE_EN}.onnx"
URL_EN_JSON="${URL_EN_ONNX}.json"

# Voz 2: Português Brasileiro (pt_BR-faber-medium)
VOICE_PT="pt_BR-faber-medium"
URL_PT_ONNX="${HUGGINGFACE_BASE}/pt/pt_BR/faber/medium/${VOICE_PT}.onnx"
URL_PT_JSON="${URL_PT_ONNX}.json"

# ── Helpers ───────────────────────────────────────────────────────────────────

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[AVISO]${NC} $*"; }
error()   { echo -e "${RED}[ERRO]${NC} $*" >&2; }

download() {
    local url="$1" dest="$2"
    if command -v curl &>/dev/null; then
        curl -fsSL --progress-bar -o "$dest" "$url"
    elif command -v wget &>/dev/null; then
        wget -q --show-progress -O "$dest" "$url"
    else
        error "Instale curl ou wget para continuar."
        exit 1
    fi
}

# ── Setup ─────────────────────────────────────────────────────────────────────

echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║         Jarvis — Setup do Piper TTS              ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════╝${NC}"
echo ""

mkdir -p "$PIPER_DIR" "$MODELS_DIR"

# ── 1. Baixar e extrair o binário Piper ──────────────────────────────────────

if [[ -f "$PIPER_DIR/piper" ]]; then
    success "Binário Piper já encontrado em $PIPER_DIR/piper"
else
    info "Baixando Piper ${PIPER_VERSION}..."
    TMP_TARBALL="$(mktemp /tmp/piper_XXXXXX.tar.gz)"

    download "$PIPER_URL" "$TMP_TARBALL"

    info "Extraindo..."
    tar -xzf "$TMP_TARBALL" -C "$PIPER_DIR" --strip-components=1
    rm -f "$TMP_TARBALL"

    chmod +x "$PIPER_DIR/piper"
    success "Piper instalado em $PIPER_DIR/piper"
fi

# ── 2. Baixar modelos de voz ───────────────────────────────────────────────────

baixar_modelo() {
    local name="$1" url_onnx="$2" url_json="$3"
    local onnx_file="$MODELS_DIR/${name}.onnx"
    local json_file="$MODELS_DIR/${name}.onnx.json"

    if [[ -f "$onnx_file" && -f "$json_file" ]]; then
        success "Modelo de voz já encontrado: ${name}"
    else
        info "Baixando modelo de voz '${name}'..."
        download "$url_onnx" "$onnx_file"
        download "$url_json" "$json_file"
        success "Modelo instalado: ${name}"
    fi
}

baixar_modelo "$VOICE_EN" "$URL_EN_ONNX" "$URL_EN_JSON"
baixar_modelo "$VOICE_PT" "$URL_PT_ONNX" "$URL_PT_JSON"

# Usa a voz PT como padrão para o teste
ONNX_FILE="$MODELS_DIR/${VOICE_PT}.onnx"

# ── 3. Teste de síntese ───────────────────────────────────────────────────────

info "Testando síntese de voz..."

TEST_WAV="$(mktemp /tmp/jarvis_test_XXXXXX.wav)"

echo "Jarvis online. Piper text to speech is working correctly." \
    | "$PIPER_DIR/piper" \
        --model "$ONNX_FILE" \
        --output_file "$TEST_WAV" \
    2>/dev/null

if [[ -f "$TEST_WAV" && -s "$TEST_WAV" ]]; then
    success "Síntese OK — reproduzindo áudio de teste..."
    if command -v aplay &>/dev/null; then
        aplay "$TEST_WAV" 2>/dev/null || warn "aplay falhou — o arquivo WAV existe mas não foi possível reproduzir."
    else
        warn "aplay não encontrado. Instale com: sudo pacman -S alsa-utils"
        warn "O arquivo de teste foi salvo em: $TEST_WAV"
    fi
    rm -f "$TEST_WAV"
else
    error "Síntese falhou — o arquivo WAV não foi gerado."
    exit 1
fi

# ── Resumo ────────────────────────────────────────────────────────────────────

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  Instalação concluída com sucesso!               ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════╝${NC}"
echo ""
echo "  Binário : $PIPER_DIR/piper"
echo "  Modelo  : $ONNX_FILE"
echo ""
echo "  Para usar outro modelo de voz, edite VOICE_NAME neste script"
echo "  ou passe o nome do modelo no construtor do PiperTtsService."
echo ""

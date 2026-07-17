# Jarvis — Assistente Local Autônomo 🧠

Jarvis é um assistente pessoal local e autônomo, desenhado para rodar **inteiramente no seu próprio computador** (sem dependência de APIs da nuvem para o processamento de linguagem). Ele é capaz não apenas de conversar com você através de comandos de texto e voz, mas também de realizar tarefas sistêmicas de controle no seu desktop de forma independente usando *Agentic Tool Calling*.

Foi idealizado para servir de ponte entre um modelo de linguagem e as ações físicas do computador (abrir aplicativos, controlar janelas, criar e ler arquivos).

---

## ✨ Características Principais

* **100% Multiplataforma**: A base do projeto foi arquitetada em C# (.NET 10.0), com suporte de ponta a ponta sem branches no código para funcionar tanto em **Linux, Windows e macOS**.
* **Agente Central (Tool Calling)**: Em vez de codificar fluxos engessados (ex: `Se "abre o gedit" -> inicia gedit`), o Jarvis utiliza uma interface flexível (`IJarvisTool`) mapeada como function calls. O próprio LLM infere *quando*, *como* e com quais argumentos usar as ferramentas instaladas.
* **Voz Nativa**: TTS (Text-to-Speech) integrado offline usando o [Piper](https://github.com/rhasspy/piper). A voz configurada tem padrão britânico (`en_GB-alan-medium`) trazendo fidelidade alta, execução neural com performance fluída até mesmo em processadores ou GPUs de pouca memória VRAM.
* **Resiliência e Fallbacks**: Múltiplos níveis de serviço. Caso um serviço primário não inicie (ex: o Piper TTS falhar), o Jarvis detecta o problema e troca suavemente para métodos de contingência (`espeak-ng`) sem quebrar a execução do seu loop natural.
* **Design Sandboxed**: Leitura e escrita de arquivos limitados à pasta de workspace (`~/Jarvis/workspace/`).

---

## 🚀 Ferramentas Atuais do Agente

O LLM orquestrador é capacitado por *Tools*. Abaixo estão as nativas implementadas até o momento:
* **Web Search**: Consulta em tempo real (Custom Search Google).
* **Processos**: Abrir executáveis usando chamadas nativas do SO.
* **Janelas**: Listar janelas ativas e matar/fechar programas no sistema operacional (via EWMH/wmctrl no Linux e expansões P/Invoke planejadas para o Windows).
* **Arquivos**: Escrever (`overwrite`/`append`) e ler textos e dados gerados usando uma Sandbox isolada.

---

## 🛠️ Dependências do Sistema & Instalação

Como o projeto integra diretamente com recursos visuais e de áudio, ele possui **dependências em ferramentas utilitárias do Sistema Operacional**.

A instalação do TTS (Text-to-Speech) do **Piper** é feita através dos scripts de instalação automática na pasta `scripts/`. Ele baixará automaticamente o binário correto para o seu sistema e instalará o modelo local neural `en_GB-alan-medium`.

### 🐧 Linux (Arch / Ubuntu / Debian)
**O que ele usa:**
* `alsa-utils` (`aplay`) para disparar a voz no áudio do sistema.
* `wmctrl` para mapeamento EWMH e gerenciamento/fechamento das janelas gráficas.

**Instalação das dependências do SO:**
```bash
# Se você usar Arch Linux / Manjaro:
sudo pacman -S alsa-utils wmctrl

# Se você usar Ubuntu / Debian:
sudo apt install alsa-utils wmctrl
```

**Instalando o TTS Automaticamente:**
```bash
chmod +x scripts/setup-piper.sh
./scripts/setup-piper.sh
```

### 🪟 Windows (Powershell)
**O que ele usa:**
* `ffmpeg` (`ffplay.exe`) como o mecanismo de saída de áudio para rodar o arquivo sintético `.wav` gerado em disco.

**Instalação das dependências do SO:**
Execute em um prompt como administrador:
```powershell
winget install ffmpeg
```

**Instalando o TTS Automaticamente:**
Abra o Powershell e conceda a permissão necessária, depois execute:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
.\scripts\setup-piper.ps1
```

### 🍏 macOS
No Mac, o Piper deve ser compilado e direcionado usando o binário ou dependências suportadas pelas ferramentas CoreAudio. A arquitetura .NET e o orquestrador Ollama rodarão nativamente sem grandes mudanças.

---

## ⚙️ Configuração Inicial do Projeto

Antes de inicializar o Entry Point Principal (`Jarvis.Console`), lembre-se de que a IA Local do **Ollama** precisa estar ligada rodando o modelo (recomendado o `qwen2.5:7b-instruct` por ser leve e suportar function calling muito bem).

1. No terminal, inicie a Engine de inferência:
   ```bash
   ollama serve
   ```
2. Adicione as chaves de API necessárias e defina o modelo que o Jarvis enviará na Request:
   ```bash
   # Variáveis de Configuração obrigatórias (no bash/zsh/powershell)
   export GOOGLE_API_KEY="SUA_CHAVE_AQUI"
   export GOOGLE_CX="SUA_CHAVE_AQUI"
   export OLLAMA_MODEL="qwen2.5:7b-instruct" # ou "llama3" caso prefira
   ```

3. Compile e rode o Console!
   ```bash
   dotnet run --project Jarvis.Console
   ```

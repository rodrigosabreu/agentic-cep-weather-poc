#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_EXE="$SCRIPT_DIR/backend/AgenticCepWeather.Api"
PID_FILE="$SCRIPT_DIR/.pid"
LOG_FILE="$SCRIPT_DIR/logs/backend.log"

mkdir -p "$SCRIPT_DIR/logs"

if [ ! -f "$BACKEND_EXE" ]; then
    echo "ERRO: Executável do backend não encontrado em $BACKEND_EXE"
    echo "Execute o script de build primeiro."
    exit 1
fi

if [ -f "$PID_FILE" ]; then
    OLD_PID=$(cat "$PID_FILE")
    if kill -0 "$OLD_PID" 2>/dev/null; then
        echo "Backend já está rodando (PID: $OLD_PID)"
        exit 0
    else
        echo "Removendo PID file órfão..."
        rm "$PID_FILE"
    fi
fi

if [ ! -f "$SCRIPT_DIR/.env" ]; then
    echo "AVISO: Arquivo .env não encontrado. Copie .env.example para .env e configure as variáveis."
    if [ -f "$SCRIPT_DIR/.env.example" ]; then
        cp "$SCRIPT_DIR/.env.example" "$SCRIPT_DIR/.env"
        echo "Arquivo .env criado a partir do exemplo. EDITE-O antes de continuar!"
        exit 1
    fi
fi

export $(grep -v '^#' "$SCRIPT_DIR/.env" | xargs) 2>/dev/null || true
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

echo "Iniciando backend..."
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 nohup "$BACKEND_EXE" --urls "${ASPNETCORE_URLS:-http://0.0.0.0:5000}" >> "$LOG_FILE" 2>&1 &
BACKEND_PID=$!

echo $BACKEND_PID > "$PID_FILE"

sleep 3

if kill -0 "$BACKEND_PID" 2>/dev/null; then
    echo "Backend iniciado com sucesso (PID: $BACKEND_PID)"
    echo "Logs: tail -f $LOG_FILE"
    echo "Health check: curl http://localhost:5000/api/health"
else
    echo "ERRO: Backend falhou ao iniciar. Verifique os logs:"
    cat "$LOG_FILE"
    rm "$PID_FILE"
    exit 1
fi
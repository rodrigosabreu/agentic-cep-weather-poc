#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_FILE="$SCRIPT_DIR/.pid"
STOP_TIMEOUT=15

if [ ! -f "$PID_FILE" ]; then
    echo "PID file não encontrado. Backend pode não estar rodando."
    exit 0
fi

PID=$(cat "$PID_FILE")

if ! kill -0 "$PID" 2>/dev/null; then
    echo "Processo $PID não está mais rodando. Removendo PID file."
    rm "$PID_FILE"
    exit 0
fi

echo "Parando backend (PID: $PID)..."
kill -TERM "$PID"

for i in $(seq 1 $STOP_TIMEOUT); do
    if ! kill -0 "$PID" 2>/dev/null; then
        echo "Backend parado graciosamente."
        rm "$PID_FILE"
        exit 0
    fi
    sleep 1
done

echo "Timeout de $STOP_TIMEOUT segundos atingido. Forçando encerramento..."
kill -KILL "$PID" 2>/dev/null || true
sleep 1

if kill -0 "$PID" 2>/dev/null; then
    echo "ERRO: Não foi possível parar o processo $PID"
    exit 1
else
    echo "Backend forçado a parar."
    rm "$PID_FILE"
    exit 0
fi
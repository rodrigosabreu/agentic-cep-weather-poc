#!/bin/bash
set -e

PROJECT_ROOT="/opt/data/projects/agentic-cep-weather-poc"
BACKEND_DIR="$PROJECT_ROOT/backend"
FRONTEND_DIR="$PROJECT_ROOT/frontend"
PUBLISH_DIR="$PROJECT_ROOT/publish"

echo "=== Build Agentic CEP Weather POC ==="

echo ">> Limpando diretório de publicação anterior..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

echo ">> Restaurando dependências do backend..."
cd "$BACKEND_DIR"
dotnet restore AgenticCepWeather.sln

echo ">> Compilando backend..."
dotnet build AgenticCepWeather.sln -c Release --no-restore

echo ">> Publicando backend (self-contained linux-x64)..."
# Copy frontend build to wwwroot before publish so static files are embedded
if [ -d "$FRONTEND_DIR/dist" ]; then
    echo ">> Copiando frontend para wwwroot..."
    rm -rf "$BACKEND_DIR/src/AgenticCepWeather.Api/wwwroot"
    mkdir -p "$BACKEND_DIR/src/AgenticCepWeather.Api/wwwroot"
    cp -r "$FRONTEND_DIR/dist/"* "$BACKEND_DIR/src/AgenticCepWeather.Api/wwwroot/"
fi

dotnet publish src/AgenticCepWeather.Api/AgenticCepWeather.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -p:PublishTrimmed=false \
    -o "$PUBLISH_DIR/backend" \
    --no-build

echo ">> Build do backend concluído!"

if [ -d "$FRONTEND_DIR" ] && [ -f "$FRONTEND_DIR/package.json" ]; then
    echo ">> Instalando dependências do frontend..."
    cd "$FRONTEND_DIR"
    npm ci

    echo ">> Build do frontend..."
    npm run build

    echo ">> Copiando build do frontend para publicação..."
    mkdir -p "$PUBLISH_DIR/frontend"
    cp -r dist/* "$PUBLISH_DIR/frontend/"
    echo ">> Build do frontend concluído!"
else
    echo ">> Frontend não encontrado, pulando..."
fi

echo ">> Copiando scripts de inicialização..."
cp "$PROJECT_ROOT/scripts/start.sh" "$PUBLISH_DIR/" 2>/dev/null || true
cp "$PROJECT_ROOT/scripts/stop.sh" "$PUBLISH_DIR/" 2>/dev/null || true
cp "$PROJECT_ROOT/scripts/env.example" "$PUBLISH_DIR/.env.example" 2>/dev/null || true

chmod +x "$PUBLISH_DIR"/*.sh 2>/dev/null || true

echo "=== Build completo! Artefatos em: $PUBLISH_DIR ==="
echo ""
echo "Estrutura:"
find "$PUBLISH_DIR" -type f | head -30
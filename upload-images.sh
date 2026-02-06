#!/bin/bash

GITHUB_USER="tempoodev"

echo "Ejecutando Aspire Push y detectando servicios..."

# Ejecutar y capturar
OUTPUT=$(aspire do push 2>&1)
echo "$OUTPUT"

# 1. Extraer el Tag
ASPIRE_TAG=$(echo "$OUTPUT" | grep -oE "aspire-deploy-[0-9]{14}" | head -n 1)

if [ -z "$ASPIRE_TAG" ]; then
    echo "Error: No se pudo encontrar el tag."
    exit 1
fi

# 2. Detectar Servicios (Filtra líneas de éxito y coge la 3ª palabra)
SERVICES=$(echo "$OUTPUT" | grep "Successfully tagged" | awk '{print $3}' | sort -u)

if [ -z "$SERVICES" ]; then
    echo "No se detectaron servicios."
    exit 1
fi

echo -e "\nTag: $ASPIRE_TAG"
echo "Servicios detectados: $SERVICES"

# 3. Bucle de subida
for NAME in $SERVICES; do
    echo -e "\n--- Procesando: $NAME ---"
    LOCAL_IMAGE="${NAME}:${ASPIRE_TAG}"
    REMOTE_IMAGE="ghcr.io/${GITHUB_USER}/${NAME}:latest"
    
    podman tag "$LOCAL_IMAGE" "$REMOTE_IMAGE"
    podman push "$REMOTE_IMAGE"
done

echo -e "\n¡Todo listo!"
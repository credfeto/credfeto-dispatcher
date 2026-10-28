#!/bin/sh
# Generates a self-signed, passwordless development certificate (server.pfx)
# for Credfeto.Dispatcher.Server with CN=defi.local and SANs localhost and
# 127.0.0.1. Refuses to overwrite an existing server.pfx unless --force is given.
set -eu

die() {
    if [ -t 2 ]; then
        printf '\n\033[31m✗\033[0m %s\n' "$*" >&2
    else
        printf '\n✗ %s\n' "$*" >&2
    fi
    exit 1
}

success() {
    if [ -t 1 ]; then
        printf '\n\033[32m✓\033[0m %s\n' "$*"
    else
        printf '\n✓ %s\n' "$*"
    fi
}

info() {
    if [ -t 1 ]; then
        printf '\n\033[32m→\033[0m %s\n' "$*"
    else
        printf '\n→ %s\n' "$*"
    fi
}

usage() {
    die "Usage: $0 [--force]"
}

FORCE=0
case "$#" in
    0) ;;
    1)
        case "$1" in
            -f | --force) FORCE=1 ;;
            *) usage ;;
        esac
        ;;
    *) usage ;;
esac

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/.." && pwd)
TARGET="$REPO_ROOT/src/Credfeto.Dispatcher.Server/server.pfx"
VALIDITY_DAYS=365

command -v openssl >/dev/null 2>&1 || die "openssl is not installed or not on PATH"

if [ -e "$TARGET" ] && [ "$FORCE" -ne 1 ]; then
    die "$TARGET already exists - re-run with --force to overwrite it"
fi

WORK_DIR=$(mktemp -d)

cleanup() {
    rm -rf "$WORK_DIR"
}
trap cleanup EXIT

KEY_FILE="$WORK_DIR/server.key"
CERT_FILE="$WORK_DIR/server.crt"
PFX_FILE="$WORK_DIR/server.pfx"

info "Generating self-signed certificate for CN=defi.local (valid ${VALIDITY_DAYS} days)..."
openssl req -x509 \
    -newkey rsa:2048 \
    -nodes \
    -sha256 \
    -days "$VALIDITY_DAYS" \
    -subj "/CN=defi.local" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
    -addext "extendedKeyUsage=serverAuth" \
    -keyout "$KEY_FILE" \
    -out "$CERT_FILE" ||
    die "openssl failed to generate the certificate (OpenSSL 1.1.1 or later is required)"

info "Packaging passwordless PKCS#12 bundle..."
openssl pkcs12 -export \
    -inkey "$KEY_FILE" \
    -in "$CERT_FILE" \
    -passout pass: \
    -out "$PFX_FILE" ||
    die "openssl failed to export the PKCS#12 bundle"

mv -f "$PFX_FILE" "$TARGET" || die "Could not write $TARGET"

success "Wrote $TARGET"

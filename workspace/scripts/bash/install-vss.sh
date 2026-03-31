#!/bin/bash
set -e

# .workspace/scripts/bash/install-vss.sh
# 自动下载并安装 sqlite-vss 扩展二进制文件

VERSION="v0.1.2"
OS="$(uname -s)"
ARCH="$(uname -m)"

case "${OS}" in
    Linux*)     PLATFORM="loadable-linux";;
    Darwin*)    PLATFORM="loadable-macos";;
    *)          echo "Unsupported OS: $OS"; exit 1;;
esac

# 架构适配
if [[ "${ARCH}" == "arm64" || "${ARCH}" == "aarch64" ]]; then
    PLATFORM="${PLATFORM}-aarch64"
else
    PLATFORM="${PLATFORM}-x86_64"
fi

URL="https://github.com/asg017/sqlite-vss/releases/download/${VERSION}/sqlite-vss-${VERSION}-${PLATFORM}.tar.gz"
TEMP_FILE=".workspace/vss_temp.tar.gz"

DIRS=(
    "ClawSharp.CLI/bin/Debug/net10.0"
    "ClawSharp.Lib.Tests/bin/Debug/net10.0"
)

echo "=== Detected System: $OS ($ARCH) ==="
echo "Downloading from: $URL"

if command -v curl >/dev/null 2>&1; then
    curl -L "$URL" -o "$TEMP_FILE"
elif command -v wget >/dev/null 2>&1; then
    wget -O "$TEMP_FILE" "$URL"
else
    echo "Error: curl or wget is required."
    exit 1
fi

for TARGET_DIR in "${DIRS[@]}"; do
    echo "Extracting to: $TARGET_DIR"
    mkdir -p "$TARGET_DIR"
    tar -xz -f "$TEMP_FILE" -C "$TARGET_DIR"
done

rm "$TEMP_FILE"

echo "✓ sqlite-vss successfully installed to related project directories."

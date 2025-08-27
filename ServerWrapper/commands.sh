exit 0

for size in 16 32 48 64 128 256 512 1024; do inkscape "assets/icon.svg" --export-type=png --export-filename="assets/icons/${size}x${size}.png" -w ${size} -h ${size}; done
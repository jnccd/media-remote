import sharp from 'sharp';
import fs from 'fs';
import path from 'path';

async function generateIcons() {
  const png2iconsModule = await import('png2icons');

  // Paths
  const svg = path.resolve('assets/icon.svg');
  const outputDir = path.resolve('assets/generated-icons');
  if (!fs.existsSync(outputDir)) fs.mkdirSync(outputDir, { recursive: true });

  // Standard Electron icon sizes
  const sizes = [16, 32, 48, 64, 128, 256, 512, 1024];

  // Generate PNGs for all sizes
  const pngFiles: string[] = [];
  for (const size of sizes) {
    const pngPath = path.join(outputDir, `${size}x${size}.png`);
    pngFiles.push(pngPath);
    await sharp(svg).resize(size, size).png().toFile(pngPath);
  }

  console.log('✅ Generated PNG icons:', pngFiles);

  const pngBuffer = fs.readFileSync(pngFiles[sizes.length - 3]);

  // Create Windows .ico
  const icoBuffer = png2iconsModule.createICO(
    pngBuffer,
    png2iconsModule.BICUBIC,
    0,
    true,
  );
  if (!icoBuffer) {
    throw new Error('Failed to generate .ico file from PNG');
  }
  fs.writeFileSync(path.join(outputDir, 'icon.ico'), icoBuffer);

  // Create macOS .icns
  const icnsBuffer = png2iconsModule.createICNS(
    pngBuffer,
    png2iconsModule.BICUBIC,
    10,
  );
  if (!icnsBuffer) {
    throw new Error('Failed to generate .icns file from PNG');
  }
  fs.writeFileSync(path.join(outputDir, 'icon.icns'), icnsBuffer);

  console.log('✅ Generated all Electron icons in', outputDir);
}

generateIcons();

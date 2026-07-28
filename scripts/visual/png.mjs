/**
 * Minimal zero-dependency PNG decoder for the visual-regression diff:
 * enough to decode Unity's EncodeToPNG output (8-bit RGB/RGBA, non-interlaced,
 * single IHDR/IDAT stream) into raw pixel bytes for tolerance comparison.
 *
 * GPU rasterization is not perfectly byte-stable: curved specular edges can
 * jitter by 1 LSB between otherwise identical runs. The diff therefore uses
 * SHA equality as a fast path and falls back to a per-pixel tolerance.
 */
import { inflateSync } from 'node:zlib';

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** Decodes a PNG buffer to { width, height, channels, pixels } (8-bit). */
export function decodePng(buffer) {
  if (!buffer.subarray(0, 8).equals(PNG_SIGNATURE)) {
    throw new Error('not a PNG');
  }

  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  let interlace = 0;
  const idatParts = [];

  let offset = 8;
  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString('ascii', offset + 4, offset + 8);
    const data = buffer.subarray(offset + 8, offset + 8 + length);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') {
      idatParts.push(data);
    } else if (type === 'IEND') {
      break;
    }
    offset += 12 + length;
  }

  if (bitDepth !== 8 || interlace !== 0 || (colorType !== 2 && colorType !== 6)) {
    throw new Error(
      `unsupported PNG (bitDepth ${bitDepth}, colorType ${colorType}, interlace ${interlace})`
    );
  }

  const channels = colorType === 6 ? 4 : 3;
  const raw = inflateSync(Buffer.concat(idatParts));
  const stride = width * channels;
  const pixels = Buffer.allocUnsafe(height * stride);

  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const lineIn = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const lineOut = pixels.subarray(y * stride, (y + 1) * stride);
    const prior = y > 0 ? pixels.subarray((y - 1) * stride, y * stride) : null;

    for (let x = 0; x < stride; x++) {
      const rawByte = lineIn[x];
      const left = x >= channels ? lineOut[x - channels] : 0;
      const up = prior ? prior[x] : 0;
      const upLeft = prior && x >= channels ? prior[x - channels] : 0;
      let value;
      switch (filter) {
        case 0:
          value = rawByte;
          break;
        case 1:
          value = rawByte + left;
          break;
        case 2:
          value = rawByte + up;
          break;
        case 3:
          value = rawByte + ((left + up) >> 1);
          break;
        case 4: {
          const p = left + up - upLeft;
          const pa = Math.abs(p - left);
          const pb = Math.abs(p - up);
          const pc = Math.abs(p - upLeft);
          value = rawByte + (pa <= pb && pa <= pc ? left : pb <= pc ? up : upLeft);
          break;
        }
        default:
          throw new Error(`unsupported PNG filter ${filter} on row ${y}`);
      }
      lineOut[x] = value & 0xff;
    }
  }

  return { width, height, channels, pixels };
}

/**
 * Tolerance comparison of two decoded PNGs. Returns
 * { equal, changedPixels, maxDelta, totalPixels } where `equal` means every
 * differing pixel is within maxChannelDelta AND the changed-pixel count is
 * within maxChangedFraction of the image.
 */
export function comparePixels(a, b, { maxChannelDelta = 2, maxChangedFraction = 0.0001 } = {}) {
  if (a.width !== b.width || a.height !== b.height || a.channels !== b.channels) {
    return {
      equal: false,
      changedPixels: -1,
      maxDelta: -1,
      totalPixels: a.width * a.height,
      reason: 'dimension mismatch'
    };
  }

  const totalPixels = a.width * a.height;
  let changedPixels = 0;
  let maxDelta = 0;
  for (let i = 0; i < totalPixels; i++) {
    let pixelChanged = false;
    for (let c = 0; c < a.channels; c++) {
      const delta = Math.abs(a.pixels[i * a.channels + c] - b.pixels[i * b.channels + c]);
      if (delta > 0) {
        pixelChanged = true;
        if (delta > maxDelta) maxDelta = delta;
      }
    }
    if (pixelChanged) changedPixels++;
  }

  const equal =
    maxDelta <= maxChannelDelta && changedPixels <= Math.ceil(totalPixels * maxChangedFraction);
  return { equal, changedPixels, maxDelta, totalPixels };
}

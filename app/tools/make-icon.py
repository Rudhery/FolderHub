"""
Gera Assets/folderhub.ico a partir do design "Folder Hub - icone".

Tres barras em escada (100% / 72% / 44%) sobre um bloco arredondado com
gradiente 160deg #2b3037 -> #171a1e e um fio de borda branca a 9%.

Sem dependencias: escreve o PNG na mao (zlib) e empacota no .ico.
Rode da raiz do projeto:  python tools/make-icon.py
"""
import math
import os
import struct
import zlib

TILE_TOP = (0x2B, 0x30, 0x37)
TILE_BOTTOM = (0x17, 0x1A, 0x1E)
BAR = (0xDB, 0xE4, 0xEC)
BORDER_ALPHA = 0.09
GRADIENT_DEG = 160

# Valores do design, por tamanho. Os pequenos sao ajustados na mao para
# encaixar na grade de pixels; os grandes escalam a arte de 200px.
# A pilha de barras e centralizada na vertical, entao o recuo de cima sai
# do proprio calculo (justify-content: center no design).
#          size  radius  pad_h   gap  bar_h  alfa das 3 barras
SPECS = [
    (16,    4.0,   3.0,   2.0,   2.0,  (1.0, 0.70, 0.50)),
    (24,    5.5,   4.5,   2.5,   3.0,  (1.0, 0.65, 0.45)),
    (32,    7.0,   6.0,   3.0,   4.0,  (1.0, 0.60, 0.38)),
    (48,   11.0,  10.0,   4.0,   6.0,  (1.0, 0.55, 0.32)),
    (64,   14.0,  13.5,   5.5,   7.5,  (1.0, 0.52, 0.29)),
    (96,   21.0,  21.0,   8.0,  11.0,  (1.0, 0.50, 0.26)),
    (128,  28.0,  28.0,  10.7,  14.7,  (1.0, 0.50, 0.26)),
    (256,  56.3,  56.3,  20.5,  28.2,  (1.0, 0.50, 0.26)),
]

BAR_WIDTHS_SMALL = (1.00, 0.70, 0.44)   # 16px
BAR_WIDTHS = (1.00, 0.72, 0.44)


def rrect_hit(x, y, left, top, right, bottom, r):
    """Ponto dentro de um retangulo arredondado?"""
    if x < left or x > right or y < top or y > bottom:
        return False
    r = min(r, (right - left) / 2, (bottom - top) / 2)
    if r <= 0:
        return True
    dx = max(left + r - x, x - (right - r), 0.0)
    dy = max(top + r - y, y - (bottom - r), 0.0)
    if dx > 0.0 and dy > 0.0:
        return (dx * dx + dy * dy) <= r * r
    return True


def gradient_at(x, y, size):
    """Gradiente linear CSS de 160deg dentro de um quadrado."""
    rad = math.radians(GRADIENT_DEG)
    dx, dy = math.sin(rad), -math.cos(rad)
    length = abs(size * dx) + abs(size * dy)
    t = 0.5 + ((x - size / 2) * dx + (y - size / 2) * dy) / length
    t = min(1.0, max(0.0, t))
    return tuple(TILE_TOP[i] + (TILE_BOTTOM[i] - TILE_TOP[i]) * t for i in range(3))


def render(size, radius, pad_h, gap, bar_h, alphas, ss=4):
    inner_w = size - 2 * pad_h
    widths = BAR_WIDTHS_SMALL if size <= 16 else BAR_WIDTHS

    stack_h = 3 * bar_h + 2 * gap
    y0 = (size - stack_h) / 2

    bars = []
    for i in range(3):
        top = y0 + i * (bar_h + gap)
        bars.append((pad_h, top, pad_h + inner_w * widths[i], top + bar_h,
                     bar_h / 2, alphas[i]))

    w = size * ss
    acc = [[0.0, 0.0, 0.0, 0.0] for _ in range(size * size)]

    for py in range(w):
        y = (py + 0.5) / ss
        oy = py // ss
        for px in range(w):
            x = (px + 0.5) / ss

            if not rrect_hit(x, y, 0, 0, size, size, radius):
                continue

            r, g, b = gradient_at(x, y, size)

            # fio de borda: entre a silhueta e ela mesma encolhida em 1px
            if not rrect_hit(x, y, 1, 1, size - 1, size - 1, radius - 1):
                r += (255 - r) * BORDER_ALPHA
                g += (255 - g) * BORDER_ALPHA
                b += (255 - b) * BORDER_ALPHA

            for (bx0, by0, bx1, by1, br, alpha) in bars:
                if rrect_hit(x, y, bx0, by0, bx1, by1, br):
                    r += (BAR[0] - r) * alpha
                    g += (BAR[1] - g) * alpha
                    b += (BAR[2] - b) * alpha
                    break

            cell = acc[oy * size + (px // ss)]
            cell[0] += r
            cell[1] += g
            cell[2] += b
            cell[3] += 1.0

    n = float(ss * ss)
    rows = []
    for y in range(size):
        row = bytearray([0])  # filtro 0
        for x in range(size):
            r, g, b, cover = acc[y * size + x]
            if cover <= 0.0:
                row += b"\x00\x00\x00\x00"
                continue
            row += bytes((
                max(0, min(255, int(round(r / cover)))),
                max(0, min(255, int(round(g / cover)))),
                max(0, min(255, int(round(b / cover)))),
                max(0, min(255, int(round(255.0 * cover / n)))),
            ))
        rows.append(bytes(row))
    return b"".join(rows)


def chunk(tag, data):
    return (struct.pack(">I", len(data)) + tag + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))


def png(size, raw):
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def main():
    images = []
    for (size, radius, pad_h, gap, bar_h, alphas) in SPECS:
        ss = 4 if size <= 128 else 2
        images.append((size, png(size, render(size, radius, pad_h, gap, bar_h, alphas, ss))))
        print("  ok", size)

    out = bytearray(struct.pack("<HHH", 0, 1, len(images)))
    offset = 6 + 16 * len(images)
    for size, data in images:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in images:
        out += data

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    dest = os.path.join(root, "src", "FolderHub", "Assets", "folderhub.ico")
    with open(dest, "wb") as f:
        f.write(out)
    print("escrito:", dest, len(out), "bytes")


if __name__ == "__main__":
    main()

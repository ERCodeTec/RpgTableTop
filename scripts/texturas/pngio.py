"""Leitura e escrita de PNG sem dependências: RGBA, RGB, cinza e paleta (8 bits ou menos)."""
import struct
import zlib


def ler(conteudo: bytes):
    """Pixels (r, g, b, a) linha a linha."""
    pos = 8
    dados = b''
    plte = trns = None
    while pos < len(conteudo):
        n, = struct.unpack('>I', conteudo[pos:pos + 4])
        tipo_bloco = conteudo[pos + 4:pos + 8]
        bloco = conteudo[pos + 8:pos + 8 + n]
        pos += 12 + n
        if tipo_bloco == b'IHDR':
            w, h, bd, tipo, _, _, entrelacado = struct.unpack('>IIBBBBB', bloco)
        elif tipo_bloco == b'PLTE':
            plte = bloco
        elif tipo_bloco == b'tRNS':
            trns = bloco
        elif tipo_bloco == b'IDAT':
            dados += bloco
    if entrelacado:
        raise ValueError('PNG entrelaçado não é suportado')

    canais = {6: 4, 2: 3, 3: 1, 0: 1, 4: 2}[tipo]
    bpp = max(1, canais * bd // 8)
    stride = (w * canais * bd + 7) // 8
    bruto = zlib.decompress(dados)
    anterior = bytearray(stride)
    i = 0
    pixels = []
    for _ in range(h):
        filtro = bruto[i]
        i += 1
        linha = bytearray(bruto[i:i + stride])
        i += stride
        for x in range(stride):
            a = linha[x - bpp] if x >= bpp else 0
            b = anterior[x]
            c = anterior[x - bpp] if x >= bpp else 0
            if filtro == 1:
                linha[x] = (linha[x] + a) & 255
            elif filtro == 2:
                linha[x] = (linha[x] + b) & 255
            elif filtro == 3:
                linha[x] = (linha[x] + (a + b) // 2) & 255
            elif filtro == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                linha[x] = (linha[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        anterior = linha
        saida = []
        for x in range(w):
            if tipo == 6:
                saida.append(tuple(linha[x * 4:x * 4 + 4]))
            elif tipo == 2:
                saida.append(tuple(linha[x * 3:x * 3 + 3]) + (255,))
            elif tipo == 4:
                saida.append((linha[x * 2],) * 3 + (linha[x * 2 + 1],))
            elif tipo == 0:
                saida.append((linha[x],) * 3 + (255,))
            else:
                k = linha[x] if bd == 8 else (linha[(x * bd) // 8] >> (8 - bd - (x * bd) % 8)) & ((1 << bd) - 1)
                alfa = trns[k] if trns and k < len(trns) else 255
                saida.append(tuple(plte[k * 3:k * 3 + 3]) + (alfa,))
        pixels.append(saida)
    return pixels


def gravar(caminho: str, pixels) -> None:
    """Grava em RGB (o alfa, se houver, é descartado: as texturas são opacas)."""
    h, w = len(pixels), len(pixels[0])
    bruto = b''.join(b'\0' + bytes(v for p in linha for v in p[:3]) for linha in pixels)

    def bloco(tipo, conteudo):
        return struct.pack('>I', len(conteudo)) + tipo + conteudo + struct.pack('>I', zlib.crc32(tipo + conteudo) & 0xffffffff)

    with open(caminho, 'wb') as arquivo:
        arquivo.write(
            b'\x89PNG\r\n\x1a\n'
            + bloco(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
            + bloco(b'IDAT', zlib.compress(bruto, 9))
            + bloco(b'IEND', b'')
        )


def recortar(pixels, x, y, w, h):
    return [linha[x:x + w] for linha in pixels[y:y + h]]


def opaco(pixels) -> bool:
    return all(p[3] == 255 for linha in pixels for p in linha)


def compor(frente, fundo):
    """`frente` sobre `fundo`, pelo alfa."""
    return [
        [tuple(round(f[i] * f[3] / 255 + b[i] * (1 - f[3] / 255)) for i in range(3)) + (255,) for f, b in zip(lf, lb)]
        for lf, lb in zip(frente, fundo)
    ]


def chapado(cor, lado=16):
    return [[tuple(cor) + (255,)] * lado for _ in range(lado)]


def recolorir(pixels, mapa):
    """Troca cada cor pela do mapa cuja origem é a mais próxima."""
    def perto(p):
        return min(mapa, key=lambda origem: sum((a - b) ** 2 for a, b in zip(origem, p[:3])))
    return [[tuple(mapa[perto(p)]) + (255,) for p in linha] for linha in pixels]

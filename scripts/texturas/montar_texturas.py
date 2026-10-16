#!/usr/bin/env python3
"""
Monta as texturas prontas das estruturas a partir dos pacotes da Kenney (CC0) em recursos/kenney.

Para cada textura do CATALOGO: tira o ladrilho do pacote (arquivo solto ou recorte de uma folha),
aplica o ajuste se houver (fundo sólido, recolorir), grava em frontend/public/texturas/<chave>.png
e escreve o catálogo que o servidor lê (backend/src/Vtt.Domain/Mapas/texturas-prontas.json), com
as categorias:

- tema: o ambiente (masmorra, caverna, natureza...). Agrupa as texturas na tela e escolhe o
  conjunto que o gerador de mapas usa.
- estruturas: os tipos de estrutura em que a textura faz sentido. O seletor do Mestre mostra só as
  do tipo selecionado.
- papeis: onde o gerador de mapas pode usar a textura (piso das salas, corredor, parede, rocha,
  porta, pilar, obstaculo, perigo). Sem papel, a textura fica só para o Mestre escolher à mão.

Uso: python3 scripts/texturas/montar_texturas.py   (a partir da raiz do projeto)
"""
import json
import os
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngio import chapado, compor, gravar, ler, opaco, recolorir, recortar  # noqa: E402

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PACOTES = os.path.join(RAIZ, 'recursos', 'kenney')
DESTINO = os.path.join(RAIZ, 'frontend', 'public', 'texturas')
CATALOGO_JSON = os.path.join(RAIZ, 'backend', 'src', 'Vtt.Domain', 'Mapas', 'texturas-prontas.json')

TEMAS = {
    'masmorra': 'Masmorra',
    'castelo': 'Castelo',
    'caverna': 'Caverna',
    'vila': 'Vila',
    'interior': 'Interiores',
    'natureza': 'Natureza',
    'neve': 'Neve',
    'deserto': 'Deserto',
    'vulcao': 'Vulcão',
}

NOME_DO_PACOTE = {
    'tiny-dungeon': 'Tiny Dungeon',
    'tiny-town': 'Tiny Town',
    'tiny-ski': 'Tiny Ski',
    'tiny-battle': 'Tiny Battle',
    'tiny-farm': 'Tiny Farm',
    'roguelike-rpg-pack': 'Roguelike RPG Pack',
    'roguelike-caves-dungeons': 'Roguelike Caves & Dungeons',
    'roguelike-indoors': 'Roguelike Indoors',
    'pixel-platformer-blocks': 'Pixel Platformer Blocks',
    'rpg-urban-pack': 'RPG Urban Pack',
    'retro-textures-fantasy': 'Retro Textures Fantasy',
}

# Tipos de estrutura: P parede, I pilar, D porta, O obstáculo, C chão.
TIPOS = {'P': 'Parede', 'I': 'Pilar', 'D': 'Porta', 'O': 'Obstaculo', 'C': 'Chao'}
PAPEIS = {'piso', 'corredor', 'parede', 'rocha', 'porta', 'pilar', 'obstaculo', 'perigo'}

RPG = 'Spritesheet/roguelikeSheet_transparent.png'
CAVES = 'Spritesheet/roguelikeDungeon_transparent.png'
INDOOR = 'Tilesheets/roguelikeIndoor_transparent.png'


# Fundos para tirar a transparência de grades e janelas.
ESCURO = (26, 28, 34)
MURALHA = ('retro-textures-fantasy', 'PNG/wall_brick_stone_center.png')


def tile(n):
    return f'Tiles/tile_{n:04d}.png'


def retro(nome):
    return f'PNG/{nome}.png'


# (chave, nome, tema, tipos, papéis, pacote, origem, ajuste)
# origem: caminho no zip, ou (caminho da folha, x, y) para um recorte de 16x16.
# ajuste: None, ('fundo', cor ou (pacote, origem)) para tirar a transparência, ('cores', {de: para})
# ou ('tingir', (r, g, b)) para multiplicar cada canal.
CATALOGO = [
    # ---- Masmorra
    ('parede-pedra', 'Pedra', 'masmorra', 'PI', 'parede pilar', 'tiny-dungeon', tile(40), None),
    ('parede-grade', 'Pedra com grade', 'masmorra', 'P', '', 'tiny-dungeon', tile(28), None),
    ('parede-metal', 'Chapas de metal', 'masmorra', 'PD', '', 'tiny-dungeon', tile(37), None),
    ('parede-de-blocos', 'Blocos com friso', 'masmorra', 'PI', 'parede', 'roguelike-caves-dungeons', (CAVES, 272, 51), None),
    ('parede-de-cantaria', 'Cantaria', 'masmorra', 'PI', 'parede pilar', 'retro-textures-fantasy', retro('wall_stone'), None),
    ('parede-de-cantaria-relevo', 'Cantaria (relevo)', 'masmorra', 'PI', '', 'retro-textures-fantasy', retro('wall_stone_depth'), None),
    ('grade-de-prisao', 'Grade de prisão', 'masmorra', 'PD', '', 'roguelike-caves-dungeons', (CAVES, 425, 187), None),
    ('piso-lajotas', 'Lajotas', 'masmorra', 'C', 'piso', 'tiny-dungeon', tile(42), None),
    ('piso-terra', 'Terra', 'masmorra', 'C', 'corredor', 'tiny-dungeon', tile(12), None),
    ('piso-de-pedra', 'Piso de pedra', 'masmorra', 'C', 'piso corredor', 'retro-textures-fantasy', retro('floor_stone'), None),
    ('piso-com-grelha', 'Piso com grelha', 'masmorra', 'C', '', 'retro-textures-fantasy', retro('floor_stone_grate'), None),
    ('pedra-rachada', 'Pedra rachada', 'masmorra', 'C', 'corredor', 'roguelike-rpg-pack', (RPG, 442, 323), None),
    ('ossos', 'Chão de ossos', 'masmorra', 'C', '', 'roguelike-caves-dungeons', (CAVES, 170, 238), None),
    ('caixotes', 'Caixotes', 'masmorra', 'O', 'obstaculo', 'roguelike-rpg-pack', (RPG, 595, 187), None),
    ('porta-madeira', 'Porta de madeira', 'masmorra', 'D', 'porta', 'tiny-dungeon', tile(46), None),
    ('porta-reforcada', 'Porta reforçada', 'masmorra', 'D', 'porta', 'roguelike-caves-dungeons', (CAVES, 374, 255), None),
    ('portao-de-grade', 'Portão de grade', 'masmorra', 'PD', '', 'retro-textures-fantasy', retro('door_metal_gate'), ('fundo', ESCURO)),

    # ---- Castelo
    ('parede-rustica', 'Pedra rústica', 'castelo', 'PI', 'parede', 'tiny-town', tile(49), None),
    ('blocos-de-pedra', 'Blocos de pedra', 'castelo', 'PI', 'pilar', 'roguelike-rpg-pack', (RPG, 374, 272), None),
    ('blocos-de-marmore', 'Blocos de mármore', 'castelo', 'PI', 'pilar', 'pixel-platformer-blocks', 'Tiles/Marble/tile_0040.png', None),
    ('blocos-de-ardosia', 'Blocos de ardósia', 'castelo', 'PI', '', 'pixel-platformer-blocks', 'Tiles/Rock/tile_0043.png', None),
    ('muralha-de-pedra', 'Muralha de pedra', 'castelo', 'PI', 'parede', 'retro-textures-fantasy', retro('wall_brick_stone_center'), None),
    ('muralha-de-pedra-relevo', 'Muralha de pedra (relevo)', 'castelo', 'PI', '', 'retro-textures-fantasy', retro('wall_brick_stone_center_depth'), None),
    ('muralha-com-estandarte', 'Muralha com estandarte', 'castelo', 'P', '', 'retro-textures-fantasy', retro('wall_brick_stone_center_banner'), None),
    ('tijolinhos-de-pedra', 'Tijolinhos de pedra', 'castelo', 'PI', 'parede', 'retro-textures-fantasy', retro('wall_brick_small_stone'), None),
    ('tijolinhos-de-pedra-relevo', 'Tijolinhos de pedra (relevo)', 'castelo', 'PI', '', 'retro-textures-fantasy', retro('wall_brick_small_stone_depth'), None),
    ('calcada-cinza', 'Calçada cinza', 'castelo', 'C', 'corredor', 'roguelike-rpg-pack', (RPG, 102, 51), None),
    ('lajes-de-pedra', 'Lajes de pedra', 'castelo', 'C', 'piso', 'roguelike-caves-dungeons', (CAVES, 340, 187), None),
    ('ladrilhos-de-pedra', 'Ladrilhos de pedra', 'castelo', 'C', 'piso', 'retro-textures-fantasy', retro('floor_stone_pattern'), None),
    ('ladrilhos-de-pedra-relevo', 'Ladrilhos de pedra (relevo)', 'castelo', 'C', '', 'retro-textures-fantasy', retro('floor_stone_pattern_depth'), None),
    ('ladrilhos-pequenos', 'Ladrilhos pequenos', 'castelo', 'C', 'corredor', 'retro-textures-fantasy', retro('floor_stone_pattern_small'), None),
    ('ladrilhos-pequenos-relevo', 'Ladrilhos pequenos (relevo)', 'castelo', 'C', '', 'retro-textures-fantasy', retro('floor_stone_pattern_small_depth'), None),
    ('lajotas-castanhas-grandes', 'Lajotas castanhas grandes', 'castelo', 'C', 'piso', 'retro-textures-fantasy', retro('floor_tiles_tan_large'), None),
    ('lajotas-castanhas', 'Lajotas castanhas', 'castelo', 'C', 'corredor', 'retro-textures-fantasy', retro('floor_tiles_tan_small'), None),
    ('lajotas-castanhas-gastas', 'Lajotas castanhas gastas', 'castelo', 'C', '', 'retro-textures-fantasy', retro('floor_tiles_tan_small_damaged'), None),
    ('porta-de-ferro', 'Porta de ferro', 'castelo', 'D', 'porta', 'roguelike-caves-dungeons', (CAVES, 425, 255), None),
    ('porta-com-argola', 'Porta com argola', 'castelo', 'D', 'porta', 'retro-textures-fantasy', retro('door_wood_handle'), None),
    ('portao-com-cadeado', 'Portão com cadeado', 'castelo', 'PD', '', 'retro-textures-fantasy', retro('door_metal_gate_lock'), ('fundo', ESCURO)),
    ('janela-em-arco', 'Janela em arco', 'castelo', 'P', '', 'retro-textures-fantasy', retro('window_tall_rounded'), ('fundo', MURALHA)),
    ('janela-em-arco-acesa', 'Janela em arco acesa', 'castelo', 'P', '', 'retro-textures-fantasy', retro('window_tall_rounded_lit'), ('fundo', MURALHA)),
    ('janela-de-ferro', 'Janela de ferro', 'castelo', 'P', '', 'retro-textures-fantasy', retro('window_square_metal_fortified'), ('fundo', ('retro-textures-fantasy', retro('floor_stone')))),

    # ---- Caverna
    ('rocha-de-caverna', 'Rocha de caverna', 'caverna', 'CP', 'piso rocha', 'roguelike-caves-dungeons', (CAVES, 153, 34), None),
    ('rocha-rachada', 'Rocha rachada', 'caverna', 'CPI', 'pilar', 'roguelike-caves-dungeons', (CAVES, 187, 51), None),
    ('rocha-bruta', 'Rocha bruta', 'caverna', 'PI', 'parede pilar', 'retro-textures-fantasy', retro('wall_rock'), None),
    ('rocha-escorada', 'Rocha escorada', 'caverna', 'P', '', 'retro-textures-fantasy', retro('wall_rock_structure'), None),
    ('ardosia', 'Ardósia', 'caverna', 'CP', 'piso', 'pixel-platformer-blocks', 'Tiles/Rock/tile_0064.png', None),
    ('escoras-de-mina', 'Escoras de mina', 'caverna', 'P', '', 'roguelike-caves-dungeons', (CAVES, 238, 119), None),
    ('terra-de-caverna', 'Terra de caverna', 'caverna', 'C', 'corredor', 'roguelike-caves-dungeons', (CAVES, 153, 119), None),
    ('cascalho', 'Cascalho', 'caverna', 'C', 'corredor', 'roguelike-rpg-pack', (RPG, 119, 0), None),
    ('pedregulho', 'Pedregulho', 'caverna', 'C', 'piso', 'roguelike-rpg-pack', (RPG, 153, 0), None),
    ('musgo', 'Musgo', 'caverna', 'C', '', 'roguelike-rpg-pack', (RPG, 187, 187), None),
    ('veio-de-carvao', 'Veio de carvão', 'caverna', 'POI', 'obstaculo', 'roguelike-caves-dungeons', (CAVES, 170, 170), None),
    ('veio-de-ouro', 'Veio de ouro', 'caverna', 'POI', 'obstaculo', 'roguelike-caves-dungeons', (CAVES, 187, 204), None),
    ('veio-de-esmeralda', 'Veio de esmeralda', 'caverna', 'POI', '', 'roguelike-caves-dungeons', (CAVES, 187, 221), None),
    ('cristais-de-ametista', 'Cristais de ametista', 'caverna', 'POI', 'obstaculo', 'roguelike-caves-dungeons', (CAVES, 170, 204), None),
    ('agua-de-caverna', 'Água de caverna', 'caverna', 'C', 'perigo', 'roguelike-caves-dungeons', (CAVES, 17, 187), None),
    ('agua-acida', 'Água ácida', 'caverna', 'C', 'perigo', 'roguelike-caves-dungeons', (CAVES, 102, 187), None),
    ('lodo-magico', 'Lodo mágico', 'caverna', 'C', '', 'roguelike-rpg-pack', (RPG, 51, 374), None),

    # ---- Vila
    ('parede-tijolo', 'Tijolos', 'vila', 'PI', 'parede', 'tiny-town', tile(53), None),
    ('parede-madeira', 'Madeira', 'vila', 'P', 'parede', 'tiny-town', tile(73), None),
    ('tijolo-marrom', 'Tijolo marrom', 'vila', 'PI', 'pilar', 'roguelike-rpg-pack', (RPG, 119, 68), None),
    ('tabuas', 'Tábuas', 'vila', 'PO', '', 'roguelike-rpg-pack', (RPG, 799, 221), None),
    ('parede-de-celeiro', 'Parede de celeiro', 'vila', 'P', '', 'tiny-farm', tile(115), None),
    ('parede-de-tabuas', 'Parede de tábuas', 'vila', 'P', 'parede', 'retro-textures-fantasy', retro('wall_timber'), None),
    ('enxaimel-alto', 'Enxaimel', 'vila', 'P', 'parede', 'retro-textures-fantasy', retro('wall_timber_structure'), None),
    ('enxaimel-alto-cruzado', 'Enxaimel cruzado', 'vila', 'P', '', 'retro-textures-fantasy', retro('wall_timber_structure_cross'), None),
    ('enxaimel-alto-diagonal', 'Enxaimel diagonal', 'vila', 'P', '', 'retro-textures-fantasy', retro('wall_timber_structure_diagonal'), None),
    ('enxaimel-alto-horizontal', 'Enxaimel horizontal', 'vila', 'P', '', 'retro-textures-fantasy', retro('wall_timber_structure_horizontal'), None),
    ('enxaimel-alto-vertical', 'Enxaimel vertical', 'vila', 'P', '', 'retro-textures-fantasy', retro('wall_timber_structure_vertical'), None),
    ('enxaimel-com-barro', 'Enxaimel com barro', 'vila', 'P', '', 'retro-textures-fantasy', retro('timber_square_clay'), None),
    ('enxaimel-com-barro-diagonal', 'Enxaimel com barro (diagonal)', 'vila', 'P', '', 'retro-textures-fantasy', retro('timber_square_clay_diagonal'), None),
    ('tabuas-emolduradas', 'Tábuas emolduradas', 'vila', 'PO', '', 'retro-textures-fantasy', retro('timber_square_planks'), None),
    ('tabuas-pregadas', 'Tábuas pregadas', 'vila', 'PO', '', 'retro-textures-fantasy', retro('timber_square_planks_boarded'), None),
    ('tabuas-em-x', 'Tábuas em X', 'vila', 'PO', 'obstaculo', 'retro-textures-fantasy', retro('timber_square_planks_cross'), None),
    ('tabuas-diagonais', 'Tábuas diagonais', 'vila', 'PO', '', 'retro-textures-fantasy', retro('timber_square_planks_diagonal'), None),
    ('telhado-cinza', 'Telhado de cerâmica cinza', 'vila', 'PO', '', 'retro-textures-fantasy', retro('roof_clay_grey_center'), None),
    ('telhado-vermelho', 'Telhado de cerâmica vermelha', 'vila', 'PO', '', 'retro-textures-fantasy', retro('roof_clay_red_center'), None),
    ('telhado-de-palha', 'Telhado de palha', 'vila', 'PO', '', 'retro-textures-fantasy', retro('roof_thatch_center'), None),
    ('janela-alta', 'Janela alta', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_tall_divided'), None),
    ('janela-alta-acesa', 'Janela alta acesa', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_tall_divided_lit'), None),
    ('janela-quadrada', 'Janela quadrada', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_square_divided'), None),
    ('janela-quadrada-acesa', 'Janela quadrada acesa', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_square_divided_lit'), None),
    ('janela-fechada', 'Janela fechada', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_square_closed'), None),
    ('janela-tapada', 'Janela tapada', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_square_divided_boarded'), None),
    ('janela-redonda', 'Janela redonda', 'vila', 'P', '', 'retro-textures-fantasy', retro('window_round_divided'), ('fundo', ('retro-textures-fantasy', retro('timber_square_clay')))),
    ('assoalho', 'Assoalho', 'vila', 'C', 'piso', 'roguelike-rpg-pack', (RPG, 136, 34), None),
    ('assoalho-de-tabuas', 'Assoalho de tábuas', 'vila', 'C', 'piso', 'retro-textures-fantasy', retro('floor_wood_planks'), None),
    ('assoalho-gasto', 'Assoalho gasto', 'vila', 'C', 'corredor', 'retro-textures-fantasy', retro('floor_wood_planks_damaged'), None),
    ('assoalho-relevo', 'Assoalho (relevo)', 'vila', 'C', '', 'retro-textures-fantasy', retro('floor_wood_planks_depth'), None),
    ('tabuado-largo', 'Tabuado largo', 'vila', 'C', 'piso', 'retro-textures-fantasy', retro('floor_wood_planks_wide'), None),
    ('tabuado-largo-gasto', 'Tabuado largo gasto', 'vila', 'C', '', 'retro-textures-fantasy', retro('floor_wood_planks_wide_damaged'), None),
    ('tabuado-largo-relevo', 'Tabuado largo (relevo)', 'vila', 'C', '', 'retro-textures-fantasy', retro('floor_wood_planks_wide_depth'), None),
    ('calcada-de-pedra', 'Calçada de pedra', 'vila', 'C', 'corredor', 'roguelike-rpg-pack', (RPG, 85, 51), None),
    ('caixotes-reforcados', 'Caixotes reforçados', 'vila', 'O', 'obstaculo', 'tiny-farm', tile(91), None),
    ('porta-de-carvalho', 'Porta de carvalho', 'vila', 'D', 'porta', 'retro-textures-fantasy', retro('door_wood'), None),
    ('porta-com-janela', 'Porta com janela', 'vila', 'D', 'porta', 'retro-textures-fantasy', retro('door_wood_window'), None),
    ('porta-com-janela-acesa', 'Porta com janela acesa', 'vila', 'D', '', 'retro-textures-fantasy', retro('door_wood_window_lit'), None),

    # ---- Interiores
    ('estante-de-livros', 'Estante de livros', 'interior', 'PO', 'obstaculo', 'roguelike-rpg-pack', (RPG, 765, 51), None),
    ('prateleira-de-pocoes', 'Prateleira de poções', 'interior', 'PO', 'obstaculo', 'roguelike-indoors', (INDOOR, 85, 221), None),
    ('armario', 'Armário', 'interior', 'PO', 'obstaculo', 'roguelike-indoors', (INDOOR, 17, 221), None),
    ('assoalho-vermelho', 'Assoalho avermelhado', 'interior', 'C', 'piso', 'roguelike-indoors', (INDOOR, 408, 0), None),
    ('tapete-vermelho', 'Tapete vermelho', 'interior', 'C', 'piso', 'roguelike-indoors', (INDOOR, 85, 153), None),
    ('tapete-verde', 'Tapete verde', 'interior', 'C', '', 'roguelike-indoors', (INDOOR, 204, 153), None),
    ('lajota-bege', 'Lajota bege', 'interior', 'C', 'corredor', 'rpg-urban-pack', tile(109), None),
    ('azulejos-azuis-grandes', 'Azulejos azuis grandes', 'interior', 'C', 'piso', 'retro-textures-fantasy', retro('floor_tiles_blue_large'), None),
    ('azulejos-azuis', 'Azulejos azuis', 'interior', 'C', '', 'retro-textures-fantasy', retro('floor_tiles_blue_small'), None),
    ('azulejos-azuis-gastos', 'Azulejos azuis gastos', 'interior', 'C', '', 'retro-textures-fantasy', retro('floor_tiles_blue_small_damaged'), None),

    # ---- Natureza
    ('grama', 'Grama', 'natureza', 'C', 'piso', 'tiny-town', tile(1), None),
    ('grama-florida', 'Grama florida', 'natureza', 'C', 'piso', 'tiny-town', tile(2), None),
    ('grama-viva', 'Grama viva', 'natureza', 'C', 'piso', 'roguelike-rpg-pack', (RPG, 85, 0), None),
    ('gramado', 'Gramado', 'natureza', 'C', '', 'retro-textures-fantasy', retro('floor_ground_grass'), None),
    ('flores-vermelhas', 'Flores vermelhas', 'natureza', 'C', '', 'roguelike-rpg-pack', (RPG, 0, 102), None),
    ('flores-brancas', 'Flores brancas', 'natureza', 'C', '', 'roguelike-rpg-pack', (RPG, 0, 153), None),
    ('flores-azuis', 'Flores azuis', 'natureza', 'C', '', 'roguelike-rpg-pack', (RPG, 0, 204), None),
    ('lavoura', 'Lavoura', 'natureza', 'C', '', 'tiny-farm', tile(24), None),
    ('caminho-pedras', 'Caminho de pedras', 'natureza', 'C', 'corredor', 'tiny-town', tile(43), None),
    ('trilha', 'Trilha', 'natureza', 'C', 'corredor', 'tiny-town', tile(39), None),
    ('terra-batida', 'Terra batida', 'natureza', 'C', 'corredor', 'roguelike-rpg-pack', (RPG, 102, 0), None),
    ('terra-umida', 'Terra úmida', 'natureza', 'C', '', 'retro-textures-fantasy', retro('floor_ground_dirt'), None),
    ('agua', 'Água', 'natureza', 'C', 'perigo', 'tiny-battle', tile(37), None),
    ('agua-rasa', 'Água rasa', 'natureza', 'C', 'perigo', 'roguelike-rpg-pack', (RPG, 0, 0), None),
    ('agua-profunda', 'Água profunda', 'natureza', 'C', 'perigo', 'roguelike-rpg-pack', (RPG, 187, 136), None),
    ('agua-limpa', 'Água limpa', 'natureza', 'C', '', 'retro-textures-fantasy', retro('floor_ground_water'), None),
    ('agua-de-pantano', 'Água de pântano', 'natureza', 'C', '', 'retro-textures-fantasy', retro('floor_ground_water_green'), None),
    ('rochas-na-agua', 'Rochas na água', 'natureza', 'CO', '', 'roguelike-rpg-pack', (RPG, 918, 391), None),
    ('floresta', 'Floresta', 'natureza', 'POI', 'parede rocha obstaculo', 'tiny-town', tile(19), ('fundo', (44, 94, 46))),
    ('floresta-outono', 'Floresta de outono', 'natureza', 'POI', 'obstaculo', 'tiny-town', tile(22), ('fundo', (110, 58, 34))),
    ('copas-de-arvore', 'Copas de árvore', 'natureza', 'POI', 'pilar obstaculo', 'rpg-urban-pack', tile(262), None),
    ('copas-de-outono', 'Copas de outono', 'natureza', 'POI', '', 'rpg-urban-pack', tile(343), None),

    # ---- Neve
    ('neve', 'Neve', 'neve', 'C', 'piso', 'tiny-ski', tile(3), None),
    ('gelo', 'Gelo', 'neve', 'C', 'corredor', 'tiny-ski', tile(26), None),
    ('gelo-cristalino', 'Gelo cristalino', 'neve', 'CPI', 'parede pilar', 'roguelike-caves-dungeons', (CAVES, 34, 238), None),
    ('rocha-nevada', 'Rocha nevada', 'neve', 'PI', 'rocha', 'pixel-platformer-blocks', 'Tiles/Marble/tile_0064.png', None),
    ('pinheiro-neve', 'Pinheiros na neve', 'neve', 'POI', 'obstaculo', 'tiny-ski', tile(6), ('fundo', ('tiny-ski', tile(3)))),

    # ---- Deserto
    ('piso-areia', 'Areia', 'deserto', 'C', 'corredor', 'tiny-dungeon', tile(49), None),
    ('areia-clara', 'Areia clara', 'deserto', 'C', 'piso', 'roguelike-rpg-pack', (RPG, 136, 0), None),
    ('areia-do-deserto', 'Areia do deserto', 'deserto', 'CP', 'corredor rocha', 'roguelike-rpg-pack', (RPG, 187, 238), None),
    ('areia-grossa', 'Areia grossa', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_ground_sand'), None),
    ('calcada-de-arenito', 'Calçada de arenito', 'deserto', 'C', 'piso', 'roguelike-rpg-pack', (RPG, 119, 51), None),
    ('arenito-rachado', 'Arenito rachado', 'deserto', 'C', '', 'roguelike-rpg-pack', (RPG, 323, 323), None),
    ('lajes-de-arenito', 'Lajes de arenito', 'deserto', 'C', 'piso', 'retro-textures-fantasy', retro('floor_stone_sand_random'), None),
    ('lajes-de-arenito-relevo', 'Lajes de arenito (relevo)', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_stone_sand_random_depth'), None),
    ('arenito-com-moldura', 'Arenito com moldura', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_stone_sand_inset'), None),
    ('arenito-com-grelha', 'Arenito com grelha', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_stone_sand_grate'), None),
    ('lajotas-de-areia-grandes', 'Lajotas de areia grandes', 'deserto', 'C', 'piso', 'retro-textures-fantasy', retro('floor_tiles_sand_large'), None),
    ('lajotas-de-areia', 'Lajotas de areia', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_tiles_sand_small'), None),
    ('lajotas-de-areia-gastas', 'Lajotas de areia gastas', 'deserto', 'C', '', 'retro-textures-fantasy', retro('floor_tiles_sand_small_damaged'), None),
    ('blocos-de-arenito', 'Blocos de arenito', 'deserto', 'PI', 'pilar', 'roguelike-rpg-pack', (RPG, 255, 272), None),
    ('blocos-de-arenito-claro', 'Blocos de arenito claro', 'deserto', 'PI', '', 'pixel-platformer-blocks', 'Tiles/Sand/tile_0040.png', None),
    ('tijolos-de-arenito', 'Tijolos de arenito', 'deserto', 'PI', 'parede', 'retro-textures-fantasy', retro('wall_brick_sand_center'), None),
    ('tijolos-de-arenito-relevo', 'Tijolos de arenito (relevo)', 'deserto', 'PI', '', 'retro-textures-fantasy', retro('wall_brick_sand_center_depth'), None),
    ('tijolos-de-arenito-estandarte', 'Arenito com estandarte', 'deserto', 'P', '', 'retro-textures-fantasy', retro('wall_brick_sand_center_banner'), None),
    ('tijolinhos-de-arenito', 'Tijolinhos de arenito', 'deserto', 'PI', 'parede', 'retro-textures-fantasy', retro('wall_brick_small_sand'), None),
    ('tijolinhos-de-arenito-relevo', 'Tijolinhos de arenito (relevo)', 'deserto', 'PI', '', 'retro-textures-fantasy', retro('wall_brick_small_sand_depth'), None),

    # ---- Vulcão
    ('lava', 'Lava', 'vulcao', 'C', 'perigo', 'tiny-battle', tile(37), ('cores', {
        (117, 227, 255): (214, 74, 28), (159, 235, 255): (248, 148, 42), (217, 247, 255): (255, 220, 110)})),
    ('lava-borbulhante', 'Lava borbulhante', 'vulcao', 'C', 'perigo', 'roguelike-rpg-pack', (RPG, 51, 323), None),
    ('terra-vermelha', 'Terra vermelha', 'vulcao', 'C', 'piso', 'pixel-platformer-blocks', 'Tiles/Stone/tile_0064.png', None),
    ('rocha-vulcanica', 'Rocha vulcânica', 'vulcao', 'CPOI', 'corredor obstaculo', 'pixel-platformer-blocks', 'Tiles/Stone/tile_0070.png', None),
    ('basalto', 'Basalto', 'vulcao', 'CPOI', 'parede rocha pilar', 'pixel-platformer-blocks', 'Tiles/Rock/tile_0064.png', ('tingir', (0.5, 0.42, 0.42))),
]


class Pacotes:
    """Lê os arquivos direto dos zips, guardando as folhas já decodificadas."""

    def __init__(self):
        self.zips = {}
        self.folhas = {}

    def arquivo(self, pacote, caminho):
        if pacote not in self.zips:
            self.zips[pacote] = zipfile.ZipFile(os.path.join(PACOTES, f'{pacote}.zip'))
        return ler(self.zips[pacote].read(caminho))

    def ladrilho(self, pacote, origem):
        if isinstance(origem, str):
            return self.arquivo(pacote, origem)
        folha, x, y = origem
        if (pacote, folha) not in self.folhas:
            self.folhas[(pacote, folha)] = self.arquivo(pacote, folha)
        return recortar(self.folhas[(pacote, folha)], x, y, 16, 16)


def main():
    pacotes = Pacotes()
    catalogo = []
    chaves = set()
    usados = set()
    for chave, nome, tema, tipos, papeis, pacote, origem, ajuste in CATALOGO:
        assert chave not in chaves, f'chave repetida: {chave}'
        assert tema in TEMAS, f'{chave}: tema desconhecido {tema}'
        assert set(papeis.split()) <= PAPEIS, f'{chave}: papel desconhecido {papeis}'
        chaves.add(chave)

        pixels = pacotes.ladrilho(pacote, origem)
        if ajuste and ajuste[0] == 'fundo':
            fundo = ajuste[1]
            fundo = chapado(fundo, len(pixels)) if isinstance(fundo[0], int) else pacotes.ladrilho(*fundo)
            pixels = compor(pixels, fundo)
        elif ajuste and ajuste[0] == 'cores':
            pixels = recolorir(pixels, ajuste[1])
        elif ajuste and ajuste[0] == 'tingir':
            pixels = [[tuple(round(p[i] * ajuste[1][i]) for i in range(3)) + (p[3],) for p in linha] for linha in pixels]
        if not opaco(pixels):
            # O material do mapa não usa transparência: o que é transparente aparece preto.
            raise SystemExit(f'{chave}: o ladrilho tem transparência; defina um fundo')

        gravar(os.path.join(DESTINO, f'{chave}.png'), pixels)
        usados.add(pacote)
        catalogo.append({
            'chave': chave,
            'nome': nome,
            'tema': tema,
            'estruturas': [TIPOS[t] for t in tipos],
            'papeis': papeis.split(),
            'largura': len(pixels[0]),
            'altura': len(pixels),
            'pacote': NOME_DO_PACOTE[pacote],
        })

    sobrando = sorted(f[:-4] for f in os.listdir(DESTINO) if f.endswith('.png') and f[:-4] not in chaves)
    if sobrando:
        print('Aviso: PNGs fora do catálogo em', DESTINO, sobrando)

    with open(CATALOGO_JSON, 'w', encoding='utf-8') as arquivo:
        json.dump({'temas': [{'id': k, 'nome': v} for k, v in TEMAS.items()], 'texturas': catalogo}, arquivo, ensure_ascii=False, indent=1)
        arquivo.write('\n')

    escrever_creditos(catalogo, usados)
    print(f'{len(catalogo)} texturas em {len(TEMAS)} temas, de {len(usados)} pacotes.')


def escrever_creditos(catalogo, usados):
    linhas = [
        '# Créditos das texturas prontas',
        '',
        'Todos os ladrilhos são de pacotes da [Kenney](https://kenney.nl), em domínio público',
        '(Creative Commons Zero, CC0). O crédito não é obrigatório; fica aqui por cortesia.',
        'Os pacotes completos estão em `recursos/kenney`; esta pasta é gerada por',
        '`scripts/texturas/montar_texturas.py`.',
        '',
        '| Pacote | Texturas |',
        '|---|---|',
    ]
    por_pacote = {}
    for textura in catalogo:
        por_pacote.setdefault(textura['pacote'], []).append(textura['chave'])
    for pacote in sorted(usados, key=lambda p: NOME_DO_PACOTE[p]):
        nome = NOME_DO_PACOTE[pacote]
        linhas.append(f'| [{nome}](https://kenney.nl/assets/{pacote}) | {", ".join(por_pacote[nome])} |')
    linhas += [
        '',
        'Ajustes feitos aqui:',
        '',
        '- floresta, floresta-outono e pinheiro-neve: as árvores ganharam um fundo sólido, sem transparência.',
        '- portões de grade: fundo escuro atrás das grades; janelas em arco, redonda e de ferro: fundo de muralha, enxaimel ou pedra.',
        '- lava: é o ladrilho de água do Tiny Battle com as cores trocadas por laranja e amarelo.',
        '- basalto: é a ardósia do Pixel Platformer Blocks escurecida.',
        '',
    ]
    with open(os.path.join(DESTINO, 'CREDITOS.md'), 'w', encoding='utf-8') as arquivo:
        arquivo.write('\n'.join(linhas))


if __name__ == '__main__':
    main()

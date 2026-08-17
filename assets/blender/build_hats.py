# ========================================================
# BUILD_HATS.PY — génère les chapeaux de skin de GoofyFPS
# ========================================================
# Usage :
#   blender --background --python assets/blender/build_hats.py
#
# Le script reconstruit TOUS les chapeaux en primitives, aux dimensions
# exactes du personnage du jeu, puis exporte chaque chapeau dans
# assets/models/hat/NN-nom.glb et sauvegarde assets/blender/hats.blend
# (avec une capsule de référence, pour retoucher à la main si besoin).
#
# DIMENSIONS DU PERSONNAGE (src/Cosmetics.cs) :
#   - capsule : rayon 0.5, de centre-1.0 à centre+1.0 (hauteur totale 2 m)
#   - la TÊTE = le dôme du haut : demi-sphère de rayon 0.5
#
# REPÈRE D'UN CHAPEAU (convention du jeu) :
#   - ORIGINE (0,0,0)  = le CENTRE du dôme de la tête
#   - Blender : +Z = le haut, -Y = DEVANT le joueur
#     (l'export glTF +Y up convertit vers le repère raylib : +Y haut, +Z devant)
#   - 1 unité Blender = 1 mètre du jeu, échelle 1:1
#
# Rappel utile : rayon du dôme à la hauteur h = sqrt(0.25 - h²)
#   h=0.20 -> 0.46 | h=0.30 -> 0.40 | h=0.40 -> 0.30 | h=0.45 -> 0.22
#
# LUMIÈRE : le jeu dessine les chapeaux avec le shader par défaut (sans
# éclairage), comme le corps des joueurs. Pour garder du relief, on "cuit"
# un éclairage lambert par FACE dans les couleurs de sommets (COLOR_0),
# que raylib multiplie automatiquement avec la couleur du matériau.

import bpy
import bmesh
import math
import os
from mathutils import Vector, Quaternion

DIR = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(DIR, "..", "models", "hat"))
os.makedirs(OUT, exist_ok=True)

# --------------------------------------------------------
# SCÈNE VIDE
# --------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# --------------------------------------------------------
# PETITS OUTILS
# --------------------------------------------------------
def mat(nom, rgb, rough=0.6, metal=0.0):
    """Matériau Principled simple. rgb = couleur telle qu'affichée en jeu."""
    m = bpy.data.materials.get(nom)
    if m:
        return m
    m = bpy.data.materials.new(nom)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*rgb, 1.0)
    return m


def _finir(obj, m):
    obj.data.materials.append(m)
    return obj


def cube(nom, w, d, h, loc, m, rot=None, bevel=0.02):
    """Un pavé w(X) x d(Y) x h(Z), avec biseau optionnel."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.name = nom
    o.scale = (w, d, h)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rot:
        o.rotation_euler = rot
    if bevel > 0:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
        b.limit_method = 'ANGLE'
    return _finir(o, m)


def cyl(nom, r, depth, loc, m, vertices=28, rot=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=depth, location=loc)
    o = bpy.context.object
    o.name = nom
    if rot:
        o.rotation_euler = rot
    return _finir(o, m)


def cone(nom, r1, r2, depth, loc, m, vertices=28, rot=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc)
    o = bpy.context.object
    o.name = nom
    if rot:
        o.rotation_euler = rot
    return _finir(o, m)


def sphere(nom, r, loc, m, seg=20, rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=loc)
    o = bpy.context.object
    o.name = nom
    return _finir(o, m)


def torus(nom, major, minor, loc, m, rot=None, mseg=32, nseg=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg, location=loc)
    o = bpy.context.object
    o.name = nom
    if rot:
        o.rotation_euler = rot
    return _finir(o, m)


def cyl_entre(nom, p1, p2, r, m, vertices=14):
    """Un cylindre qui relie deux points (pour les cordons, antennes...)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=d.length,
                                        location=(p1 + p2) / 2)
    o = bpy.context.object
    o.name = nom
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return _finir(o, m)


def prisme(nom, pts2d, y0, y1, m):
    """Un polygone (x,z) extrudé entre y0 et y1 (pour la flèche)."""
    me = bpy.data.meshes.new(nom)
    bm = bmesh.new()
    v0 = [bm.verts.new((x, y0, z)) for (x, z) in pts2d]
    v1 = [bm.verts.new((x, y1, z)) for (x, z) in pts2d]
    bm.faces.new(v0)
    bm.faces.new(list(reversed(v1)))
    n = len(pts2d)
    for i in range(n):
        a, b = i, (i + 1) % n
        bm.faces.new([v0[a], v0[b], v1[b], v1[a]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(nom, me)
    scene.collection.objects.link(o)
    return _finir(o, m)


def couper_dessous(obj, zmin_local):
    """Supprime la partie du maillage sous zmin (repère local) et rebouche le trou."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < zmin_local], context='VERTS')
    bord = [e for e in bm.edges if e.is_boundary]
    if bord:
        try:
            bmesh.ops.holes_fill(bm, edges=bord, sides=0)
        except Exception:
            pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()


def lisser(obj, angle_deg=40):
    """Ombrage lisse, mais les arêtes plus vives que 'angle' restent nettes.
    Indispensable sur les grosses coques rondes : sans ça, une sphère basse
    définition montre toutes ses facettes (les autres pièces restent à facettes,
    c'est le look voulu). À appeler APRÈS couper_dessous, qui réécrit le maillage."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle_deg))
    except (AttributeError, RuntimeError):
        bpy.ops.object.shade_smooth()
    return obj


def coll_chapeau(nom, objets):
    """Range les objets d'un chapeau dans leur collection."""
    c = bpy.data.collections.new(nom)
    scene.collection.children.link(c)
    for o in objets:
        for uc in o.users_collection:
            uc.objects.unlink(o)
        c.objects.link(o)
    return c


# --------------------------------------------------------
# CUISSON DE L'ÉCLAIRAGE DANS LES COULEURS DE SOMMETS
# (le jeu n'éclaire pas les chapeaux : on cuit le lambert dans le maillage)
# --------------------------------------------------------
# ATTENTION : cette lumière doit rester IDENTIQUE à LUMIERE_SKIN dans
# src/Cosmetics.cs (qui l'applique au corps et au visage), sinon le chapeau
# serait éclairé d'un côté et la tête de l'autre. Ici en repère Blender ;
# là-bas en repère jeu : Blender (x, y, z) -> jeu (x, z, -y).
LUMIERE = Vector((0.4, -0.55, 0.73)).normalized()  # soleil haut / avant-droite

def cuire_ombrage(objets):
    for obj in objets:
        if obj.type != 'MESH':
            continue
        me = obj.data
        attr = me.color_attributes.new(name="Shade", type='FLOAT_COLOR', domain='CORNER')
        m3 = obj.matrix_world.to_3x3().inverted_safe().transposed()
        # Les normales "de coin" respectent le réglage lisse/plat de chaque face :
        # une face plate garde la sienne, une face lissée prend celle du sommet.
        try:
            normales = [Vector(cn.vector) for cn in me.corner_normals]
        except (AttributeError, RuntimeError):
            normales = None
        for poly in me.polygons:
            for li in poly.loop_indices:
                nrm = (m3 @ (normales[li] if normales else poly.normal)).normalized()
                s = min(1.0, 0.60 + 0.40 * max(0.0, nrm.dot(LUMIERE)) + 0.07 * max(0.0, nrm.z))
                attr.data[li].color = (s, s, s, 1.0)
        me.color_attributes.active_color = attr


# --------------------------------------------------------
# EXPORT GLB (sélection de la collection, +Y up, modificateurs appliqués)
# --------------------------------------------------------
def exporter(coll, fichier):
    bpy.ops.object.select_all(action='DESELECT')
    for o in coll.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = coll.objects[0]
    chemin = os.path.join(OUT, fichier)
    base = dict(filepath=chemin, export_format='GLB', use_selection=True, export_apply=True)
    try:
        bpy.ops.export_scene.gltf(**base, export_vertex_color='ACTIVE')
    except TypeError:
        bpy.ops.export_scene.gltf(**base)
    print(f"[HATS] exporté : {chemin}")


# ========================================================
# LA CAPSULE DE RÉFÉRENCE (jamais exportée, juste pour l'échelle)
# ========================================================
gris = mat("REF-gris", (0.55, 0.55, 0.55), rough=0.9)
ref = []
ref.append(sphere("REF-tete", 0.5, (0, 0, 0), gris, seg=24, rings=16))
ref.append(cyl("REF-corps", 0.5, 1.0, (0, 0, -0.5), gris, vertices=24))
ref.append(sphere("REF-bas", 0.5, (0, 0, -1.0), gris, seg=24, rings=16))
coll_chapeau("_REFERENCE_PERSONNAGE", ref)


# ========================================================
# 1. LUNETTES DE SOLEIL (teintées noir)
# ========================================================
noir_mat = mat("lunettes-monture", (0.05, 0.05, 0.06), rough=0.35)
verre = mat("lunettes-verre", (0.07, 0.07, 0.09), rough=0.05)

objs = []
Z_YEUX = 0.10
for signe, tag in ((-1, "G"), (1, "D")):
    # monture + verre, légèrement pivotés pour épouser la courbure du crâne
    rot = (0, 0, signe * math.radians(15))
    objs.append(cube(f"monture-{tag}", 0.21, 0.035, 0.16, (signe * 0.135, -0.462, Z_YEUX), noir_mat, rot=rot, bevel=0.012))
    objs.append(cube(f"verre-{tag}", 0.175, 0.05, 0.125, (signe * 0.135, -0.472, Z_YEUX), verre, rot=rot, bevel=0.010))
    # branches : deux segments qui suivent le crâne (une seule barre droite
    # passerait DANS la tête, la corde d'un cercle s'enfonce de ~7 cm)
    for (a1, a2) in ((28, 58), (58, 86)):
        r_tete = 0.505
        p1 = (signe * r_tete * math.sin(math.radians(a1)), -r_tete * math.cos(math.radians(a1)), Z_YEUX)
        p2 = (signe * r_tete * math.sin(math.radians(a2)), -r_tete * math.cos(math.radians(a2)), Z_YEUX)
        milieu = ((p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2, Z_YEUX)
        longueur = math.dist(p1, p2) + 0.02
        angle = math.atan2(p2[1] - p1[1], p2[0] - p1[0])
        objs.append(cube(f"branche-{tag}-{a1}", longueur, 0.02, 0.032, milieu, noir_mat, rot=(0, 0, angle), bevel=0))
objs.append(cube("pont", 0.07, 0.03, 0.035, (0, -0.487, Z_YEUX + 0.01), noir_mat, bevel=0))
c1 = coll_chapeau("hat-01-lunettes-soleil", objs)


# ========================================================
# 2. CHAPEAU DE SORCIER (violet)
# ========================================================
violet = mat("sorcier-violet", (0.30, 0.12, 0.50), rough=0.65)
or_band = mat("sorcier-or", (0.85, 0.65, 0.18), rough=0.35, metal=0.8)

objs = []
objs.append(cyl("sorcier-bord", 0.62, 0.035, (0, 0, 0.29), violet, vertices=32))
objs.append(cone("sorcier-corps", 0.42, 0.06, 0.62, (0, 0, 0.61), violet, vertices=32))
# la pointe tordue (pivotée vers l'avant autour de sa base, posée sur le sommet du cône)
angle_pointe = math.radians(35)
base_pointe = Vector((0, 0, 0.92))
centre_pointe = base_pointe + Vector((0, -0.15 * math.sin(angle_pointe), 0.15 * math.cos(angle_pointe)))
objs.append(cone("sorcier-pointe", 0.06, 0.006, 0.30, centre_pointe, violet, vertices=20, rot=(angle_pointe, 0, 0)))
objs.append(cyl("sorcier-bande", 0.445, 0.09, (0, 0, 0.345), or_band, vertices=32))
objs.append(cube("sorcier-boucle", 0.10, 0.025, 0.10, (0, -0.45, 0.345), or_band, bevel=0.008))
# deux "étoiles" dorées sur le cône
objs.append(sphere("sorcier-etoile1", 0.032, (0.19, -0.19, 0.55), or_band, seg=10, rings=8))
objs.append(sphere("sorcier-etoile2", 0.030, (-0.10, -0.17, 0.68), or_band, seg=10, rings=8))
c2 = coll_chapeau("hat-02-chapeau-sorcier", objs)


# ========================================================
# 3. CASQUETTE À L'ENVERS + CHAÎNE (style rappeur)
# ========================================================
marine = mat("casquette-marine", (0.10, 0.14, 0.38), rough=0.75)
or_chaine = mat("chaine-or", (0.90, 0.72, 0.20), rough=0.25, metal=1.0)

objs = []
# la coque : une sphère aplatie coupée au niveau du crâne
bpy.ops.mesh.primitive_uv_sphere_add(segments=26, ring_count=16, radius=0.545, location=(0, 0, 0.06))
coque = bpy.context.object
coque.name = "casquette-coque"
coque.scale = (1, 1, 0.75)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
couper_dessous(coque, 0.04)  # z local 0.04 = z monde 0.10
_finir(coque, marine)
objs.append(coque)
# la visière... à L'ENVERS, donc vers l'ARRIÈRE (+Y)
objs.append(cube("casquette-visiere", 0.44, 0.36, 0.035, (0, 0.60, 0.175), marine, rot=(math.radians(10), 0, 0), bevel=0.02))
# le bouton du sommet
objs.append(cyl("casquette-bouton", 0.035, 0.025, (0, 0, 0.475), marine, vertices=14))
# LA CHAÎNE : un collier de perles dorées autour du cou, qui pend sur l'avant
N_MAILLONS = 18
R_CHAINE = 0.53
TILT = math.radians(14)
for k in range(N_MAILLONS):
    a = 2 * math.pi * k / N_MAILLONS
    x = R_CHAINE * math.sin(a)
    y = -R_CHAINE * math.cos(a) * math.cos(TILT)
    z = -0.42 - R_CHAINE * math.cos(a) * math.sin(TILT)
    objs.append(sphere(f"maillon-{k}", 0.033, (x, y, z), or_chaine, seg=10, rings=8))
# deux petits maillons de rallonge vers le pendentif
objs.append(sphere("maillon-r1", 0.028, (0, -0.545, -0.62), or_chaine, seg=10, rings=8))
objs.append(sphere("maillon-r2", 0.028, (0, -0.55, -0.68), or_chaine, seg=10, rings=8))
# le pendentif : un "$" doré en relief sur le torse
bpy.ops.object.text_add(location=(0, -0.55, -0.80))
dollar = bpy.context.object
dollar.name = "pendentif-dollar"
dollar.data.body = "$"
dollar.data.size = 0.30
dollar.data.extrude = 0.018
dollar.data.align_x = 'CENTER'
dollar.data.align_y = 'CENTER'
dollar.rotation_euler = (math.radians(90), 0, 0)
bpy.ops.object.convert(target='MESH')
dollar = bpy.context.object
if len(dollar.data.polygons) == 0:
    # au cas où la police ne suit pas : un médaillon rond à la place
    bpy.data.objects.remove(dollar)
    dollar = cyl("pendentif-medaille", 0.10, 0.03, (0, -0.55, -0.78), or_chaine, vertices=20, rot=(math.radians(90), 0, 0))
else:
    _finir(dollar, or_chaine)
objs.append(dollar)
c3 = coll_chapeau("hat-03-casquette-chaine", objs)


# ========================================================
# 4. MASQUE DE ROBOT (heaume d'acier terni, d'après la photo de référence)
# ========================================================
# Ce n'est PLUS le casque cubique gris-bleu du premier jet : la référence de
# Camille est un heaume de scaphandrier, tout en rondeur. Les traits qui le
# rendent reconnaissable de loin, par ordre d'importance :
#   1. une grosse coque ovoïde, plus large que la tête (ça pèse lourd)
#   2. deux hublots cerclés, montés sur des bossages
#   3. une crête à nervures sur le sommet
#   4. une mentonnière à barreaux qui avance sous les hublots
#   5. deux gros boulons qui sortent des joues
#   6. des ouïes obliques sur les flancs arrière
# Le jeu n'a pas de reflets spéculaires (les skins sont dessinés sans shader
# d'éclairage) : le "métal" ne peut donc venir que du CONTRASTE de valeur.
# D'où trois gris : la coque, les arêtes usées (plus clair), les creux (sombre).
acier = mat("robot-acier", (0.44, 0.43, 0.41), rough=0.5, metal=0.85)
acier_use = mat("robot-acier-use", (0.60, 0.59, 0.56), rough=0.35, metal=0.9)
acier_f = mat("robot-acier-fonce", (0.15, 0.15, 0.14), rough=0.7, metal=0.4)
oeil_noir = mat("robot-oeil", (0.04, 0.04, 0.05), rough=0.2)

DOME_R = (0.62, 0.66, 0.62)  # demi-axes de la coque (X, Y, Z)
DOME_Z = 0.05                # hauteur de son centre (le dôme de la tête est centré sur 0)


def pose_dome(a_h, a_v, sortie=0.0, roulis=0.0):
    """Où poser une pièce sur la coque du casque, et comment l'orienter.
      a_h    : angle horizontal, en radians (0 = plein devant = -Y, + = vers la droite)
      a_v    : angle vertical, en radians (0 = hauteur des hublots, + = vers le sommet)
      sortie : de combien la pièce sort de la coque
      roulis : rotation de la pièce autour de sa propre normale, en degrés
    Retourne (position, euler) tels que l'axe Z local de la pièce SORT de la coque
    et son axe Y local pointe vers le sommet : un cube(w, d, h) posé là a donc
    w en travers, d le long de la coque et h en épaisseur qui dépasse."""
    d = Vector((math.sin(a_h) * math.cos(a_v), -math.cos(a_h) * math.cos(a_v), math.sin(a_v)))
    p = Vector((DOME_R[0] * d.x, DOME_R[1] * d.y, DOME_Z + DOME_R[2] * d.z)) + d.normalized() * sortie
    q = d.to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), math.radians(roulis))
    return tuple(p), q.to_euler()


objs = []

# --- LA COQUE : un ovoïde un peu plus long que large, et LISSE ---
bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=24, radius=1.0, location=(0, 0, DOME_Z))
coque_robot = bpy.context.object
coque_robot.name = "robot-coque"
coque_robot.scale = DOME_R
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
couper_dessous(coque_robot, -0.45)  # z LOCAL : plus bas, c'est noyé dans le corps de toute façon
lisser(coque_robot)  # après la coupe : elle réécrit le maillage
_finir(coque_robot, acier)
objs.append(coque_robot)

# --- LES HUBLOTS : bossage, cerclage, trou noir ---
for signe in (-1, 1):
    ah, av = signe * math.radians(21), math.radians(3)
    p, rot = pose_dome(ah, av)
    objs.append(cyl(f"robot-bossage-{signe}", 0.135, 0.035, p, acier, vertices=24, rot=rot))
    p, _ = pose_dome(ah, av, sortie=0.012)
    objs.append(torus(f"robot-cerclage-{signe}", 0.105, 0.028, p, acier_use, rot=rot, mseg=22))
    p, _ = pose_dome(ah, av, sortie=0.022)
    objs.append(cyl(f"robot-hublot-{signe}", 0.082, 0.02, p, oeil_noir, vertices=22, rot=rot))

# --- L'ARCADE : sur la photo ce n'est pas un sourcil en relief, c'est une simple
# ligne de tôlerie. D'où des lames SOMBRES posées presque à fleur de coque.
for signe in (-1, 1):
    p, rot = pose_dome(signe * math.radians(22), math.radians(17), sortie=0.002, roulis=signe * -15)
    objs.append(cube(f"robot-arcade-{signe}", 0.24, 0.026, 0.014, p, acier_f, rot=rot, bevel=0))

# --- L'ARÊTE DU NEZ : une bande en léger relief entre et sous les hublots ---
p, rot = pose_dome(0, math.radians(-12), sortie=0.006)
objs.append(cube("robot-nez", 0.075, 0.20, 0.022, p, acier_use, rot=rot, bevel=0.006))

# --- LA CRÊTE : une nageoire couchée sur le sommet, à nervures ---
# Son profil est dessiné dans le plan (Y, Z) : le dessous épouse la coque, le
# dessus alterne bosses et creux — ce sont les nervures de la photo. On le
# construit avec 'prisme' (qui extrude un profil (X,Z) le long de Y), puis on
# pivote d'un quart de tour pour amener le profil dans le plan (Y, Z).
# Les nervures restent DISCRÈTES : une crête trop dentelée fait stégosaure,
# alors que la photo montre une nageoire basse à peine ondulée.
CRETE_A = [math.radians(a) for a in (88, 83, 78, 73, 68, 63, 58, 53, 45)]
CRETE_H = (0.020, 0.058, 0.046, 0.064, 0.048, 0.064, 0.044, 0.052, 0.016)


def _profil_crete(a, sortie):
    """Le point de l'arc du sommet à l'angle 'a', décalé de 'sortie' vers l'extérieur.
    Renvoie (Y, Z) — le quart de tour final amènera ce Y sur le bon axe."""
    return (-(DOME_R[1] + sortie) * math.cos(a), DOME_Z + (DOME_R[2] + sortie) * math.sin(a))


profil = [_profil_crete(a, -0.035) for a in CRETE_A]  # le dessous, noyé dans la coque
profil += [_profil_crete(a, h) for a, h in zip(reversed(CRETE_A), reversed(CRETE_H))]  # le dessus nervuré
crete = prisme("robot-crete", profil, -0.032, 0.032, acier_use)
crete.rotation_euler = (0, 0, math.radians(90))
objs.append(crete)

# --- LES OUÏES : trois fentes sombres et obliques sur chaque flanc arrière,
# posées à fleur de coque (sur la photo ce sont des fentes, pas des ailettes).
for signe in (-1, 1):
    for i, deg_v in enumerate((28, 18, 8)):
        p, rot = pose_dome(signe * math.radians(92), math.radians(deg_v), sortie=0.001, roulis=signe * 12)
        objs.append(cube(f"robot-ouie-{signe}-{i}", 0.16, 0.032, 0.014, p, acier_f, rot=rot, bevel=0))

# --- LA MENTONNIÈRE : cinq barreaux clairs, quatre creux sombres entre eux ---
# (le jeu n'a pas d'ombres portées : les creux DOIVENT être peints en sombre,
# sinon la grille disparaît complètement)
for i, deg in enumerate((-17.25, -5.75, 5.75, 17.25)):
    p, rot = pose_dome(math.radians(deg), math.radians(-24), sortie=0.002)
    objs.append(cube(f"robot-creux-{i}", 0.062, 0.17, 0.014, p, acier_f, rot=rot, bevel=0))
for i, deg in enumerate((-23, -11.5, 0, 11.5, 23)):
    p, rot = pose_dome(math.radians(deg), math.radians(-24), sortie=0.010)
    objs.append(cube(f"robot-barreau-{i}", 0.055, 0.18, 0.038, p, acier_use, rot=rot, bevel=0.008))

# --- LES BOULONS : manchon, tête, et son logement sombre ---
for signe in (-1, 1):
    ah, av = signe * math.radians(62), math.radians(-26)
    p, rot = pose_dome(ah, av, sortie=0.015)
    objs.append(cyl(f"robot-manchon-{signe}", 0.085, 0.10, p, acier, vertices=18, rot=rot))
    p, _ = pose_dome(ah, av, sortie=0.085)
    objs.append(cyl(f"robot-boulon-{signe}", 0.065, 0.05, p, acier_use, vertices=18, rot=rot))
    p, _ = pose_dome(ah, av, sortie=0.115)
    objs.append(cyl(f"robot-logement-{signe}", 0.028, 0.02, p, acier_f, vertices=12, rot=rot))

# --- LE COL : le jonc qui ferme le bas du casque, tout autour du cou ---
objs.append(torus("robot-col", 0.52, 0.045, (0, 0, -0.32), acier_use, mseg=32))

c4 = coll_chapeau("hat-04-masque-robot", objs)


# ========================================================
# 5. CHAPEAU DE SAMOURAÏ (jingasa beige, cordon rouge, or)
# ========================================================
paille = mat("samurai-paille", (0.72, 0.60, 0.38), rough=0.9)
laque_r = mat("samurai-laque", (0.55, 0.08, 0.08), rough=0.4)
or_sam = mat("samurai-or", (0.85, 0.65, 0.18), rough=0.35, metal=0.8)

objs = []
# le grand cône plat (la base est remplie : vue de dessous, le chapeau est fermé)
objs.append(cone("samurai-cone", 0.78, 0.015, 0.36, (0, 0, 0.48), paille, vertices=36))
# le liseré laqué rouge du bord + l'anneau doré à mi-pente
objs.append(torus("samurai-lisere", 0.76, 0.020, (0, 0, 0.315), laque_r, mseg=36))
objs.append(torus("samurai-anneau", 0.30, 0.018, (0, 0, 0.55), or_sam, mseg=28))
# le pommeau doré du sommet
objs.append(sphere("samurai-pommeau", 0.035, (0, 0, 0.67), or_sam, seg=12, rings=8))
# le cordon de menton rouge (deux brins noués sous le menton)
for signe in (-1, 1):
    objs.append(cyl_entre(f"samurai-cordon-{signe}", (signe * 0.30, -0.28, 0.30), (0, -0.44, -0.15), 0.012, laque_r))
objs.append(sphere("samurai-noeud", 0.032, (0, -0.44, -0.15), laque_r, seg=10, rings=8))
objs.append(cone("samurai-gland", 0.028, 0.006, 0.09, (0, -0.45, -0.22), laque_r, vertices=10))
c5 = coll_chapeau("hat-05-chapeau-samurai", objs)


# ========================================================
# 6. HAUT-DE-FORME (la forme "actuelle" du jeu, en vrai modèle 3D)
# ========================================================
noir_hdf = mat("hdf-noir", (0.07, 0.07, 0.08), rough=0.35)
rouge_hdf = mat("hdf-rouge", (0.60, 0.10, 0.10), rough=0.5)

objs = []
objs.append(cyl("hdf-bord", 0.42, 0.05, (0, 0, 0.425), noir_hdf, vertices=32))
objs.append(cyl("hdf-tube", 0.26, 0.46, (0, 0, 0.68), noir_hdf, vertices=32))
objs.append(cyl("hdf-ruban", 0.272, 0.10, (0, 0, 0.51), rouge_hdf, vertices=32))
c6 = coll_chapeau("hat-06-haut-de-forme", objs)


# ========================================================
# 7. LA GROSSE FLÈCHE (rouge, pointée sur le joueur)
# ========================================================
rouge_f = mat("fleche-rouge", (0.78, 0.07, 0.07), rough=0.5)

# contour (x, z), pointe en bas au-dessus du crâne
contour = [
    (0.00, 0.80),   # la pointe
    (0.30, 1.12),
    (0.13, 1.12),
    (0.13, 1.60),
    (-0.13, 1.60),
    (-0.13, 1.12),
    (-0.30, 1.12),
]
fleche = prisme("fleche", contour, -0.045, 0.045, rouge_f)
c7 = coll_chapeau("hat-07-fleche", [fleche])


# ========================================================
# CUISSON DE L'OMBRAGE + EXPORTS
# ========================================================
chapeaux = [
    (c1, "01-lunettes-soleil.glb"),
    (c2, "02-chapeau-sorcier.glb"),
    (c3, "03-casquette-chaine.glb"),
    (c4, "04-masque-robot.glb"),
    (c5, "05-chapeau-samurai.glb"),
    (c6, "06-haut-de-forme.glb"),
    (c7, "07-fleche.glb"),
]

for coll, _ in chapeaux:
    cuire_ombrage(coll.objects)

for coll, fichier in chapeaux:
    exporter(coll, fichier)

# Sauvegarde du .blend (avec la capsule de référence) pour retouches manuelles
blend = os.path.join(DIR, "hats.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend)
print(f"[HATS] scène sauvegardée : {blend}")
print(f"[HATS] {len(chapeaux)} chapeaux exportés dans {OUT}")

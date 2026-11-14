# Tempulli i Humbur - ARTA REALE 2 (personazh origjinal i ekipit)
# Trupi MPFB + asete CC0 të MakeHuman: lëkurë fotografike, sy, vetulla, qerpikë, dhëmbë,
# gërshet real, bluzë liri + pantallona kanavace (tekstura e ripikturuar), çizme.
# Shenjat e Artës: gërsheti, brezi i kuq, çanta lëkure, mbrojtëset e parakrahëve.
# Skeleti "mixamo_unity", animacionet Run (1-21), Jump (31-51), Slide (61-81). Eksport FBX për Unity.
#
# Përdorimi:  blender -b -P arta_reale2.py -- <dosja_e_aseteve_MakeHuman> <dosja_e_teksturave> <dosja_dalese>
import bpy, bmesh, addon_utils, importlib, math, os, sys, shutil
from mathutils import Vector, Quaternion, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ASSETS = os.path.abspath(argv[0] if len(argv) > 0 else os.path.join(os.path.expanduser("~"), "Downloads", "makehuman_system_assets"))
TEXDIR = os.path.abspath(argv[1] if len(argv) > 1 else os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.abspath(argv[2] if len(argv) > 2 else os.path.join(os.path.expanduser("~"), "Desktop"))
OUT_FBX = os.path.join(OUT_DIR, "arta.fbx")
OUT_BLEND = os.path.join(OUT_DIR, "arta_reale.blend")
TEX_OUT = os.path.join(OUT_DIR, "arta_tekstura")
os.makedirs(TEX_OUT, exist_ok=True)

addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)
M = "bl_ext.user_default.mpfb.services."
HS = importlib.import_module(M + "humanservice").HumanService
TS = importlib.import_module(M + "targetservice").TargetService

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)
scene = bpy.context.scene
scene.render.fps = 30

# ---------- 1. trupi ----------
macro = TS.get_default_macro_info_dict()
macro.update({"gender": 0.0, "age": 0.5, "muscle": 0.6, "weight": 0.45, "proportions": 0.85,
              "height": 0.56, "cupsize": 0.45, "firmness": 0.6})
macro["race"] = {"caucasian": 0.85, "asian": 0.05, "african": 0.10}
body = HS.create_human(mask_helpers=True, detailed_helpers=True, macro_detail_dict=macro)
rig = HS.add_builtin_rig(body, "mixamo_unity", import_weights=True)

def A(p): return os.path.join(ASSETS, p)
parts = {}
for key, f, t in [("eyes", "eyes/high-poly/high-poly.mhclo", "Eyes"),
                  ("brows", "eyebrows/eyebrow001/eyebrow001.mhclo", "Eyebrows"),
                  ("lashes", "eyelashes/eyelashes01/eyelashes01.mhclo", "Eyelashes"),
                  ("teeth", "teeth/teeth_base/teeth_base.mhclo", "Teeth"),
                  ("hair", "hair/braid01/braid01.mhclo", "Hair"),
                  ("suit", "clothes/female_casualsuit01/female_casualsuit01.mhclo", "Clothes"),
                  ("shoes", "clothes/shoes03/shoes03.mhclo", "Clothes")]:
    parts[key] = HS.add_mhclo_asset(A(f), body, asset_type=t, subdiv_levels=0, material_type="MAKESKIN")

# ---------- 2. fiksoj formën dhe fshij pjesët e fshehura ----------
def bake_shape(ob):
    me = ob.data
    if not me.shape_keys: return
    mods = [(m, m.show_viewport) for m in ob.modifiers]
    for m, _ in mods: m.show_viewport = False
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    coords = [v.co.copy() for v in ob.evaluated_get(dg).data.vertices]
    for m, s in mods: m.show_viewport = s
    ob.shape_key_clear()
    for v, c in zip(me.vertices, coords): v.co = c
    me.update()
for ob in [body] + list(parts.values()):
    bake_shape(ob)

me = body.data
# mbaj vetëm lëkurën ("body"); hiq ndihmësit (kubet e nyjeve, rrobat ndihmëse) dhe pjesët nën rroba
gbody = body.vertex_groups["body"].index
hide = {v.index for v in me.vertices if not any(e.group == gbody and e.weight > 0.5 for e in v.groups)}
for g in body.vertex_groups:
    if g.name.startswith("Delete."):
        gi = g.index
        for v in me.vertices:
            for e in v.groups:
                if e.group == gi and e.weight > 0.5: hide.add(v.index)
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.verts[i] for i in hide], context="VERTS")
bm.to_mesh(me); bm.free()
for m in [m for m in body.modifiers if m.type == "MASK"]:
    body.modifiers.remove(m)
for g in [g for g in body.vertex_groups if not g.name.startswith("mixamorig:")]:
    body.vertex_groups.remove(g)

body.name = "Arta_Trupi"; rig.name = "Arta"; rig.data.name = "Arta_Skeleti"
names = {"eyes": "Arta_Syte", "brows": "Arta_Vetullat", "lashes": "Arta_Qerpiket", "teeth": "Arta_Dhembet",
         "hair": "Arta_Floket", "suit": "Arta_Rrobat", "shoes": "Arta_Cizmet"}
for k, ob in parts.items():
    ob.name = names[k]
    for m in [m for m in ob.modifiers if m.type != "ARMATURE"]: ob.modifiers.remove(m)
    for p in ob.data.polygons: p.use_smooth = True
for p in me.polygons: p.use_smooth = True

# ---------- 3. materialet (Principled + tekstura, që Unity t'i lexojë nga FBX) ----------
def tex_copy(path, name=None):
    dst = os.path.join(TEX_OUT, name or os.path.basename(path))
    if os.path.abspath(path) != os.path.abspath(dst): shutil.copyfile(path, dst)
    return dst

def pbr(name, diffuse=None, normal=None, rough=0.7, alpha=False, sss=0.0, spec=0.5, color=None, normal_strength=1.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in bsdf.inputs: bsdf.inputs["Specular IOR Level"].default_value = spec
    if color: bsdf.inputs["Base Color"].default_value = (*color, 1)
    if diffuse:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(diffuse); t.location = (-500, 300)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha:
            nt.links.new(t.outputs["Alpha"], bsdf.inputs["Alpha"])
            if hasattr(m, "blend_method"): m.blend_method = "HASHED"
            if hasattr(m, "surface_render_method"): m.surface_render_method = "DITHERED"
    if normal:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(normal); t.image.colorspace_settings.name = "Non-Color"
        t.location = (-500, -150)
        nm = nt.nodes.new("ShaderNodeNormalMap"); nm.location = (-220, -150); nm.inputs["Strength"].default_value = normal_strength
        nt.links.new(t.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if sss > 0 and "Subsurface Weight" in bsdf.inputs:
        bsdf.inputs["Subsurface Weight"].default_value = sss
        bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        bsdf.inputs["Subsurface Scale"].default_value = 0.01
    m.use_backface_culling = not alpha
    return m

def setmat(ob, m):
    ob.data.materials.clear(); ob.data.materials.append(m)
    for p in ob.data.polygons: p.material_index = 0

# lëkura: pak e nxirë nga dielli (Arta është eksploruese), me skuqje të lehtë në faqe e hundë
from PIL import Image, ImageEnhance
import numpy as np
sk = np.asarray(Image.open(A("skins/young_caucasian_female/young_lightskinned_female_diffuse.png")).convert("RGB")).astype(np.float32) / 255
sk = sk * np.array([0.93, 0.80, 0.68], np.float32) + np.array([0.035, 0.02, 0.0], np.float32)
skin_path = os.path.join(TEX_OUT, "arta_lekura_diffuse.png")
Image.fromarray((np.clip(sk, 0, 1) * 255).astype(np.uint8)).save(skin_path)
setmat(body, pbr("Arta_Lekura", skin_path, rough=0.5, sss=0.18, spec=0.35))
setmat(parts["eyes"], pbr("Arta_Syte", tex_copy(A("eyes/materials/brown_eye.png"), "arta_syte_diffuse.png"), rough=0.05, spec=0.8))
setmat(parts["brows"], pbr("Arta_Vetullat", tex_copy(A("eyebrows/eyebrow001/eyebrow001.png"), "arta_vetullat.png"), rough=0.8, alpha=True))
setmat(parts["lashes"], pbr("Arta_Qerpiket", tex_copy(A("eyelashes/eyelashes01/eyelashes01.png"), "arta_qerpiket.png"), rough=0.8, alpha=True))
setmat(parts["teeth"], pbr("Arta_Dhembet", tex_copy(A("teeth/teeth_base/teeth.png"), "arta_dhembet.png"), rough=0.3))
# flokët: gështenjë e errët me reflekse bakri
hr = Image.open(A("hair/braid01/braid01_diffuse.png")).convert("RGBA")
h = np.asarray(hr).astype(np.float32) / 255
lum = h[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32)
lum = lum / max(1e-3, float(np.percentile(lum, 90)))
col = np.stack([0.30 * lum, 0.17 * lum, 0.09 * lum], -1)
hair_path = os.path.join(TEX_OUT, "arta_floket_diffuse.png")
Image.fromarray((np.clip(np.concatenate([col, h[..., 3:]], -1), 0, 1) * 255).astype(np.uint8)).save(hair_path)
setmat(parts["hair"], pbr("Arta_Floket", hair_path, rough=0.45, alpha=True, spec=0.6))
setmat(parts["suit"], pbr("Arta_Rrobat", tex_copy(os.path.join(TEXDIR, "arta_rrobat_diffuse.png")),
                          tex_copy(A("clothes/female_casualsuit01/female_casualsuit01_normal.png"), "arta_rrobat_normal.png"), rough=0.85))
setmat(parts["shoes"], pbr("Arta_Cizmet", tex_copy(A("clothes/shoes03/shoes03_diffuse.png"), "arta_cizmet_diffuse.png"), rough=0.55))

# ---------- 4. ndihmës për kockat dhe aksesorët ----------
B = {b.name.replace("mixamorig:", ""): b for b in rig.data.bones}
def bpos(n, tail=False):
    b = B[n]; return (b.tail_local if tail else b.head_local).copy()
ev = [v.co for v in parts["eyes"].data.vertices]
FRONT = -1.0 if sum(c.y for c in ev) / len(ev) < 0 else 1.0
crotch_z = bpos("Hips").z

def extent(obs, z, tol=0.012):
    pts = [v.co for ob in obs for v in ob.data.vertices if abs(v.co.z - z) < tol]
    xs = [abs(p.x) for p in pts]; ys = [p.y for p in pts]
    return (max(xs) if xs else 0.15), ((min(ys) + max(ys)) / 2 if ys else 0.0), ((max(ys) - min(ys)) / 2 if ys else 0.1)

def accessory(name, build, material, weights):
    m_ = bpy.data.meshes.new(name); bm_ = bmesh.new(); build(bm_)
    for f in bm_.faces: f.smooth = True
    bm_.to_mesh(m_); bm_.free()
    ob = bpy.data.objects.new(name, m_); scene.collection.objects.link(ob)
    m_.materials.append(material); ob.parent = rig
    for bn, w in weights:
        g = ob.vertex_groups.new(name="mixamorig:" + bn); g.add(list(range(len(m_.vertices))), w, "REPLACE")
    mod = ob.modifiers.new("Armature", "ARMATURE"); mod.object = rig
    return ob

def torus(bm_, center, R, r, sx=1.0, sy=1.0, tilt=0.0, seg=40, sides=8, flat=1.0):
    rot = Matrix.Rotation(tilt, 4, "X"); rows = []
    for i in range(seg):
        a_ = 2 * math.pi * i / seg; row = []
        for j in range(sides):
            b_ = 2 * math.pi * j / sides
            p = Vector(((R + r * flat * math.cos(b_)) * math.cos(a_) * sx, (R + r * flat * math.cos(b_)) * math.sin(a_) * sy, r * math.sin(b_)))
            row.append(bm_.verts.new(rot @ p + center))
        rows.append(row)
    for i in range(seg):
        for j in range(sides):
            bm_.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % sides], rows[i][(j + 1) % sides]))

RED = pbr("Arta_Kuqe", color=(0.30, 0.035, 0.03), rough=0.9)
LEATHER = pbr("Arta_Lekure", color=(0.26, 0.14, 0.06), rough=0.6)
BRASS = pbr("Arta_Bronz", color=(0.75, 0.55, 0.25), rough=0.35)
BRASS.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 1.0

# brezi i kuq (pëlhurë e lidhur mbi pantallona)
bz = crotch_z + 0.085
ex, cy, ey = extent([parts["suit"]], bz)
accessory("Arta_Brezi", lambda b_: torus(b_, Vector((0, cy, bz)), 1.0, 0.017, ex + 0.008, ey + 0.008, flat=0.3), RED, [("Hips", 1.0)])
# çanta lëkure në ijë + rripi i saj
side = 1.0
def bag(b_):
    c = Vector((side * (ex + 0.035), cy, bz - 0.11))
    bmesh.ops.create_cube(b_, size=1, matrix=Matrix.Translation(c) @ Matrix.Diagonal((0.055, 0.17, 0.15, 1)))
    bmesh.ops.create_cube(b_, size=1, matrix=Matrix.Translation(c + Vector((side * 0.03, 0, 0.055))) @ Matrix.Diagonal((0.012, 0.175, 0.07, 1)))
accessory("Arta_Canta", bag, LEATHER, [("Hips", 1.0)])
accessory("Arta_Kopsa", lambda b_: bmesh.ops.create_cube(b_, size=1, matrix=Matrix.Translation(Vector((side * (ex + 0.068), cy, bz - 0.085))) @ Matrix.Diagonal((0.006, 0.03, 0.025, 1))), BRASS, [("Hips", 1.0)])
# shiriti i kuq në ballë, mbi flokë
hd = bpos("Head")
eye_z = sum(c.z for c in ev) / len(ev)
hz = eye_z + 0.075
hx, hcy, hy = extent([body, parts["hair"]], hz, 0.006)
# (shiriti i ballit u hoq: balluket e flokëve e mbulojnë ballin)
# mbrojtëse lëkure në parakrahë
for s in ("Left", "Right"):
    a, b = bpos(s + "ForeArm"), bpos(s + "Hand")
    c = a.lerp(b, 0.72)
    def guard(b_, c=c, a=a, b=b):
        d = (b - a).normalized()
        q = Vector((0, 0, 1)).rotation_difference(d)
        bmesh.ops.create_cone(b_, cap_ends=False, segments=20, radius1=0.040, radius2=0.034, depth=0.11,
                              matrix=Matrix.Translation(c) @ q.to_matrix().to_4x4())
    accessory("Arta_Mbrojtese_" + s, guard, LEATHER, [(s + "ForeArm", 1.0)])

bpy.context.view_layer.objects.active = rig
# ---------- 5. animacionet (çdo frame i çelësuar, me lëvizje dytësore) ----------
# Run 1-21 (cikël), Jump 31-51, Slide 61-81, Hang 91-111 (cikël), Stumble 121-141, Death 151-181
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
PB = {pb.name.replace("mixamorig:", ""): pb for pb in rig.pose.bones}
for pb in rig.pose.bones: pb.rotation_mode = "QUATERNION"
REST = {n: b.matrix_local.to_quaternion() for n, b in B.items()}
PARENT = {n: (b.parent.name.replace("mixamorig:", "") if b.parent else None) for n, b in B.items()}
W = {}
def world(n):
    if n is None: return Quaternion()
    if n in W: return W[n]
    return world(PARENT[n])
def set_world(n, q):
    if n not in PB: return
    qc = world(PARENT[n]).inverted() @ q
    R = REST[n]
    PB[n].rotation_quaternion = R.inverted() @ qc @ R
    W[n] = q
def aim(n, target):
    rest = (bpos(n, True) - bpos(n)).normalized()
    set_world(n, rest.rotation_difference(Vector(target).normalized()))
def bend(n, axis, deg):
    set_world(n, Quaternion(Vector(axis), math.radians(deg)) @ world(PARENT[n]))
def reset():
    W.clear()
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion(); pb.location = Vector()
def hips_offset(v):
    PB["Hips"].location = REST["Hips"].inverted() @ Vector(v)
def key(f):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=f)
        pb.keyframe_insert("location", frame=f)

F = FRONT  # para = F * Y
FIST_SIGN = float(os.environ.get("FIST_SIGN", "1"))
SX = {"Left": 1 if bpos("LeftArm").x > 0 else -1}
SX["Right"] = -SX["Left"]
def D(a): return math.radians(a)
FINGERS = [n for n in PB if any(k in n for k in ("Index", "Middle", "Ring", "Pinky"))]
THUMBS = [n for n in PB if "Thumb" in n and not n.endswith("1")]

# Parametrat e një poze (të gjitha numra, që të interpolohen butë)
DEF = dict(pitch=0, lean=0, twist=0, tilt=0, hip_yaw=0, hx=0, hy=0, hz=0, head=0, head_yaw=0, chest=0, fist=1,
           Lth=0, Lkn=0, Lft=0, Lab=0.02, Ltoe=0, Rth=0, Rkn=0, Rft=0, Rab=0.02, Rtoe=0,
           Lsw=0, Lel=20, Lout=0.15, Lsh=0, Rsw=0, Rel=20, Rout=0.15, Rsh=0)

def apply(p):
    """p: fjalor parametrash -> pozë e skeletit"""
    reset()
    hips_offset((p["hx"], F * p["hy"], p["hz"]))
    # pitch: gjithë trupi përkulet rreth legenit (p.sh. kur bie me fytyrë përtokë)
    set_world("Hips", Quaternion((1, 0, 0), D(-F * p["pitch"])) @ Quaternion((0, 0, 1), D(p["hip_yaw"])) @ Quaternion((0, 1, 0), D(p["tilt"] * 0.4)))
    # shtylla: përkulja shpërndahet, gjoksi rrotullohet kundër legenit (twist)
    for n, k, kt in (("Spine", 0.4, 0.3), ("Spine1", 0.32, 0.35), ("Spine2", 0.28, 0.35)):
        q = (Quaternion((1, 0, 0), D(-F * p["lean"] * k)) @ Quaternion((0, 1, 0), D(p["tilt"] * k))
             @ Quaternion((0, 0, 1), D((p["twist"] + p["chest"]) * kt)))
        set_world(n, q @ world(PARENT[n]))
    for side in ("Left", "Right"):
        s = side[0]
        sx = SX[side]
        th, kn, ft, ab = p[s + "th"], p[s + "kn"], p[s + "ft"], p[s + "ab"]
        aim(side + "UpLeg", (sx * ab, F * math.sin(D(th)), -math.cos(D(th))))
        bend(side + "Leg", (1, 0, 0), -F * kn)
        set_world(side + "Foot", Quaternion((1, 0, 0), D(F * ft)))
        if side + "ToeBase" in PB:
            bend(side + "ToeBase", (1, 0, 0), -F * p[s + "toe"])
        # supi (klavikula) ngrihet/çohet pak me krahun
        if side + "Shoulder" in PB:
            set_world(side + "Shoulder", Quaternion((0, 1, 0), D(-sx * p[s + "sh"])) @ Quaternion((1, 0, 0), D(-F * p[s + "sw"] * 0.06)) @ world("Spine2"))
        sw, el, out = p[s + "sw"], p[s + "el"], p[s + "out"]
        aim(side + "Arm", (sx * out, F * math.sin(D(sw)), -math.cos(D(sw))))
        bend(side + "ForeArm", (1, 0, 0), F * el)
        set_world(side + "Hand", Quaternion((0, 0, 1), D(-sx * 8)) @ world(side + "ForeArm"))
    # koka mbetet e qëndrueshme: kundërvepron pjesërisht përkuljen dhe rrotullimin e trupit
    set_world("Neck", Quaternion((1, 0, 0), D(-F * p["head"] * 0.4)) @ world("Spine2"))
    set_world("Head", Quaternion((0, 0, 1), D(p["head_yaw"] - (p["twist"] + p["chest"]) * 0.25)) @ Quaternion((1, 0, 0), D(-F * p["head"])))
    a = p["fist"]
    for n in FINGERS: PB[n].rotation_quaternion = Quaternion((1, 0, 0), D(FIST_SIGN * 55 * a))
    for n in THUMBS: PB[n].rotation_quaternion = Quaternion((1, 0, 0), D(FIST_SIGN * 25 * a))

def P(**kw):
    d = dict(DEF); d.update(kw); return d

def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)

def sample(keys, t, loop=False):
    """keys: [(t, poza)], t në [0,1]. Interpolim Catmull-Rom (lëvizje e butë, pa ndalesa)."""
    n = len(keys)
    for i in range(n - 1):
        if keys[i][0] <= t <= keys[i + 1][0]:
            break
    else:
        i = n - 2
    t0, t1 = keys[i][0], keys[i + 1][0]
    u = 0 if t1 == t0 else (t - t0) / (t1 - t0)
    def kp(j):
        if loop: return keys[j % (n - 1)][1]
        return keys[max(0, min(n - 1, j))][1]
    a, b, c, d = kp(i - 1 if (i > 0 or loop) else 0), kp(i), kp(i + 1), kp(i + 2)
    if loop and i == 0: a = keys[n - 2][1]
    return {k: catmull(a[k], b[k], c[k], d[k], u) for k in DEF}

def bake(start, end, fn):
    for f in range(start, end + 1):
        t = (f - start) / (end - start)
        apply(fn(t)); key(f)

# ---------- VRAPIMI (analitik, 20 frame = 2 hapa) ----------
LEG = [(0.000, 28, 16, 8, 0), (0.10, 16, 34, 2, 0), (0.22, -4, 26, -6, 0), (0.34, -26, 18, -30, 28),
       (0.42, -30, 40, -42, 35), (0.52, -14, 104, -30, 10), (0.64, 26, 118, -10, 0), (0.76, 58, 88, 4, 0),
       (0.88, 50, 36, 12, 0), (1.000, 28, 16, 8, 0)]
def leg_at(ph):
    ph %= 1.0
    for i in range(len(LEG) - 1):
        if LEG[i][0] <= ph <= LEG[i + 1][0]:
            a = LEG[max(i - 1, 0) if i > 0 else len(LEG) - 2]; b = LEG[i]; c = LEG[i + 1]; d = LEG[min(i + 2, len(LEG) - 1) if i + 2 < len(LEG) else 1]
            u = (ph - b[0]) / (c[0] - b[0])
            return [catmull(a[k], b[k], c[k], d[k], u) for k in (1, 2, 3, 4)]
    return list(LEG[0][1:])

def run_pose(ph):
    L = leg_at(ph); R = leg_at(ph + 0.5)
    s = math.sin(2 * math.pi * ph)
    s2 = math.cos(4 * math.pi * (ph - 0.43))
    # krahët: kundër këmbëve, me vonesë të lehtë (overlap); bërryli mbyllet kur krahu del përpara
    aL = 0.62 * leg_at(ph + 0.5 - 0.04)[0] - 6
    aR = 0.62 * leg_at(ph - 0.04)[0] - 6
    return P(lean=12 + 1.5 * s2, twist=11 * s, chest=-4 * s, tilt=3 * math.sin(4 * math.pi * ph), hip_yaw=-7 * s,
             hz=-0.03 + 0.035 * s2, head=-8 - 1.5 * s2, head_yaw=0,
             Lth=L[0], Lkn=L[1], Lft=L[2], Ltoe=L[3], Rth=R[0], Rkn=R[1], Rft=R[2], Rtoe=R[3],
             Lsw=aL, Lel=70 + 0.35 * max(0.0, aL), Lout=0.18, Lsh=3 * max(0.0, aL) / 30,
             Rsw=aR, Rel=70 + 0.35 * max(0.0, aR), Rout=0.18, Rsh=3 * max(0.0, aR) / 30, fist=0.85)

bake(1, 21, lambda t: run_pose(t))

# ---------- KËRCIMI (kërcim i gjatë me këmbë të hapura, krahë që "notojnë" në ajër) ----------
R0 = run_pose(0.0); R1 = run_pose(0.08)
JUMP = [
    (0.00, R0),
    (0.08, P(lean=20, twist=6, hz=0.02, head=-12, Lth=72, Lkn=92, Lft=10, Rth=-36, Rkn=12, Rft=-46, Rtoe=40,
             Lsw=-48, Lel=55, Lout=0.25, Rsw=118, Rel=38, Rout=0.18, Rsh=10, fist=0.7)),
    (0.24, P(lean=10, twist=12, chest=-6, hz=0.17, head=-14, Lth=96, Lkn=110, Lft=18, Rth=-14, Rkn=96, Rft=-20,
             Lsw=30, Lel=70, Lout=0.55, Rsw=152, Rel=26, Rout=0.25, Rsh=14, fist=0.4)),
    (0.44, P(lean=22, twist=4, hz=0.23, head=-10, Lth=104, Lkn=124, Lft=18, Rth=34, Rkn=122, Rft=8,
             Lsw=96, Lel=58, Lout=0.45, Rsw=86, Rel=58, Rout=0.45, Lsh=6, Rsh=6, fist=0.5)),
    (0.64, P(lean=12, twist=-4, hz=0.13, head=-8, Lth=62, Lkn=48, Lft=12, Rth=22, Rkn=86, Rft=0,
             Lsw=48, Lel=30, Lout=0.75, Rsw=36, Rel=30, Rout=0.75, fist=0.3)),
    (0.82, P(lean=16, hz=0.02, head=-6, Lth=36, Lkn=26, Lft=14, Rth=-4, Rkn=58, Rft=-6,
             Lsw=28, Lel=55, Lout=0.4, Rsw=-18, Rel=55, Rout=0.4, fist=0.6)),
    (0.91, P(lean=26, hz=-0.15, head=-4, Lth=32, Lkn=72, Lft=6, Rth=-10, Rkn=76, Rft=-10,
             Lsw=10, Lel=65, Lout=0.35, Rsw=-25, Rel=65, Rout=0.35, fist=0.8)),
    (1.00, R1),
]
bake(31, 51, lambda t: sample(JUMP, t))

# ---------- RRËSHQITJA (power slide me dorë në tokë) ----------
SL = P(lean=-56, tilt=12, twist=-10, hip_yaw=12, hy=0.2, hz=-0.70, head=34, head_yaw=-6,
       Lth=86, Lkn=4, Lft=24, Ltoe=-10, Rth=32, Rkn=136, Rft=-34, Rab=0.15,
       Lsw=100, Lel=16, Lout=0.45, Rsw=-56, Rel=6, Rout=0.72, Rsh=-8, fist=0.25)
def slide_hold(u):
    w = math.sin(u * math.pi * 3)
    d = dict(SL); d["hz"] += 0.008 * w; d["lean"] += 2 * w; d["Lsw"] += 5 * w; d["head"] -= 2 * w; d["twist"] += 2 * w
    return d
SLIDE = [
    (0.00, run_pose(0.0)),
    (0.06, P(lean=4, tilt=4, hy=0.06, hz=-0.30, head=8, Lth=58, Lkn=32, Lft=12, Rth=-12, Rkn=96, Rft=-24, Rab=0.06,
             Lsw=58, Lel=40, Lout=0.35, Rsw=-30, Rel=25, Rout=0.5, fist=0.5)),
    (0.15, P(**{k: v for k, v in SL.items() if k not in ("lean", "hz")}, lean=-34, hz=-0.58)),
    (0.24, slide_hold(0.0)), (0.40, slide_hold(0.33)), (0.56, slide_hold(0.66)), (0.72, slide_hold(1.0)),
    (0.82, P(lean=6, tilt=8, hy=0.08, hz=-0.42, head=8, Lth=64, Lkn=68, Lft=6, Rth=4, Rkn=104, Rft=-22, Rab=0.06,
             Lsw=55, Lel=50, Lout=0.35, Rsw=-42, Rel=20, Rout=0.75, fist=0.5)),
    (0.92, P(lean=18, hz=-0.14, head=-4, Lth=42, Lkn=48, Lft=4, Rth=-20, Rkn=80, Rft=-26,
             Lsw=-6, Lel=64, Lout=0.25, Rsw=30, Rel=66, Rout=0.25, fist=0.8)),
    (1.00, run_pose(0.0)),
]
bake(61, 81, lambda t: sample(SLIDE, t))

# ---------- E VARUR NGA LITARI (cikël): krahë të drejtë, këmbë që lëkunden si biçikletë ----------
def hang(t):
    s, c = math.sin(2 * math.pi * t), math.cos(2 * math.pi * t)
    return P(lean=-6 + 4 * s, twist=5 * s, tilt=2 * c, hz=0.0, head=-6 + 2 * s, head_yaw=-3 * s,
             Lth=22 + 16 * s, Lkn=34 - 12 * c, Lft=14, Rth=12 - 14 * s, Rkn=46 + 12 * c, Rft=10,
             Lsw=176, Lel=6, Lout=0.12, Lsh=16, Rsw=176, Rel=6, Rout=0.12, Rsh=16, fist=1.0)
bake(91, 111, hang)

# ---------- PENGIMI: këmba ngec, trupi bie përpara, krahët hapen, rimëkëmbet në vrap ----------
STUMBLE = [
    (0.00, run_pose(0.0)),
    (0.14, P(lean=34, twist=-8, hz=-0.06, head=-22, Lth=46, Lkn=40, Lft=6, Rth=-34, Rkn=58, Rft=-30, Rtoe=20,
             Lsw=62, Lel=24, Lout=0.6, Rsw=78, Rel=30, Rout=0.7, Lsh=12, Rsh=12, fist=0.1)),
    (0.30, P(lean=44, twist=10, hz=-0.16, head=-28, Lth=20, Lkn=82, Lft=0, Rth=40, Rkn=70, Rft=6,
             Lsw=40, Lel=40, Lout=0.85, Rsw=88, Rel=20, Rout=0.5, Lsh=8, Rsh=14, fist=0.1)),
    (0.50, P(lean=30, twist=-6, hz=-0.10, head=-18, Lth=-10, Lkn=60, Lft=-14, Rth=52, Rkn=44, Rft=10,
             Lsw=58, Lel=50, Lout=0.6, Rsw=-12, Rel=60, Rout=0.45, fist=0.4)),
    (0.72, run_pose(0.55)),
    (1.00, run_pose(1.0)),
]
bake(121, 141, lambda t: sample(STUMBLE, t))

# ---------- VDEKJA: pengohet, bie përpara dhe shtrihet me fytyrë përtokë ----------
DEATH = [
    (0.00, run_pose(0.0)),
    (0.12, P(lean=36, hz=-0.05, head=-22, Lth=40, Lkn=30, Rth=-30, Rkn=70, Rft=-30,
             Lsw=70, Lel=20, Lout=0.5, Rsw=60, Rel=20, Rout=0.5, fist=0.1)),
    (0.30, P(pitch=35, lean=15, hy=0.30, hz=-0.20, head=-30, Lth=10, Lkn=40, Rth=-40, Rkn=50,
             Lsw=100, Lel=25, Lout=0.45, Rsw=95, Rel=30, Rout=0.45, fist=0.0)),
    (0.50, P(pitch=65, lean=8, hy=0.65, hz=-0.50, head=-34, Lth=-45, Lkn=25, Rth=-60, Rkn=40,
             Lsw=95, Lel=30, Lout=0.5, Rsw=88, Rel=35, Rout=0.5, fist=0.0)),
    (0.68, P(pitch=86, lean=3, hy=0.92, hz=-0.68, head=-34, Lth=-74, Lkn=14, Lft=-60, Rth=-82, Rkn=34, Rft=-60,
             Lsw=82, Lel=34, Lout=0.55, Rsw=74, Rel=40, Rout=0.55, fist=0.0)),
    (1.00, P(pitch=88, lean=2, hy=0.95, hz=-0.70, head=-30, tilt=4, Lth=-78, Lkn=12, Lft=-65, Rth=-86, Rkn=36, Rft=-65,
             Lsw=80, Lel=36, Lout=0.58, Rsw=70, Rel=44, Rout=0.55, fist=0.0)),
]
bake(151, 181, lambda t: sample(DEATH, t))

reset()
bpy.ops.object.mode_set(mode="OBJECT")
scene.frame_start, scene.frame_end = 1, 181
scene.frame_set(1)

# ---------- 6. eksporti ----------
bpy.ops.object.select_all(action="DESELECT")
for ob in [rig] + list(rig.children):
    ob.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in rig.children if o.type == "MESH")
print("ARTA REALE 2 GATI", OUT_FBX, "trekendesha:", tris, "front", FRONT)

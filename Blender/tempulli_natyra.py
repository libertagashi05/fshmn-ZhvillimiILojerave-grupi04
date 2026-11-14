# Tempulli i Humbur - natyra: shkëmbinj organikë, pemë, shkurre, fier. Eksport FBX për Unity.
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix, noise
out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.path.expanduser("~"), "Desktop", "tempulli_natyra.fbx")
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene
random.seed(3)

def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    return m
M_ROCK = mat("N_Shkembi", (0.5, 0.5, 0.56)); M_BARK = mat("N_Levorja", (0.3, 0.22, 0.15))
M_LEAF = mat("N_Gjethet", (0.2, 0.36, 0.12)); M_LEAF2 = mat("N_Gjethet2", (0.32, 0.45, 0.14)); M_MOSS = mat("N_Myshk", (0.24, 0.36, 0.14))

def new_obj(name, bm, material, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    me.materials.append(material)
    return o

def box_uv(o, scale=0.25):
    # UV kubike në metra (1 teksturë = 4 m)
    me = o.data; uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        n = p.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if ax == 0: u, v = co.y, co.z
            elif ax == 1: u, v = co.x, co.z
            else: u, v = co.x, co.y
            uv.data[li].uv = (u * scale, v * scale)

# ---------- shkëmbinj: ikosferë e deformuar me zhurmë + shtresa ----------
def rock(name, size, seed, flat=1.0, sharp=0.35):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    off = Vector((seed * 7.1, seed * 3.3, seed * 1.7))
    for v in bm.verts:
        p = v.co.copy()
        d = noise.fractal(p * 1.3 + off, 0.6, 2.2, 4) * 0.35
        d += noise.ridged_multi_fractal(p * 2.6 + off, 0.8, 2.0, 3, 1.0, 2.0) * 0.06
        strata = math.sin(p.z * 9 + noise.noise(p * 2 + off) * 2) * 0.025
        v.co = p * (1 + d + strata)
    for v in bm.verts:   # rrafsho pak (faqe të prera si shkëmb)
        v.co.z = v.co.z * flat
        for ax in range(3):
            if abs(v.co[ax]) > 1 - sharp * 0.5: v.co[ax] = math.copysign((1 - sharp * 0.5) + (abs(v.co[ax]) - (1 - sharp * 0.5)) * 0.3, v.co[ax])
    bm.transform(Matrix.Diagonal((*size, 1)))
    o = new_obj(name, bm, M_ROCK)
    box_uv(o)
    return o

R = []
R.append(rock("Shkembi_1", (6, 5, 14), 1, 1.0, 0.5))      # kolonë e lartë
R.append(rock("Shkembi_2", (9, 7, 7), 2, 0.8, 0.4))       # masë e gjerë
R.append(rock("Shkembi_3", (4, 4, 10), 3, 1.0, 0.6))      # majë
R.append(rock("Shkembi_4", (12, 9, 5), 4, 0.7, 0.3))      # pllakë/shkëmb poshtë
R.append(rock("Shkembi_5", (2.2, 1.8, 1.6), 5, 0.9, 0.3)) # gur i vogël

# ---------- pemë: trung i lakuar + degë + kurora gjethesh ----------
def blob(bm, c, r, seed, squash=0.75):
    m = Matrix.Translation(c) @ Matrix.Diagonal((r, r, r * squash, 1))
    res = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0, matrix=m)
    for v in res["verts"]:
        v.co += (v.co - c).normalized() * noise.noise(v.co * 1.7 + Vector((seed, 0, 0))) * r * 0.35

def tube(bm, pts, r0, r1, seg=8):
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = t.orthogonal().normalized(); b = t.cross(a)
        r = r0 + (r1 - r0) * i / (len(pts) - 1)
        rings.append([bm.verts.new(p + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    for i in range(len(rings) - 1):
        for k in range(seg):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % seg], rings[i + 1][(k + 1) % seg], rings[i + 1][k]))

def tree(name, h, lean, seed, crown=1.0):
    random.seed(seed)
    bt = bmesh.new()
    pts = [Vector((math.sin(t * 2.2) * lean * t, math.cos(t * 1.7) * lean * 0.4 * t, t * h)) for t in [i / 7 for i in range(8)]]
    tube(bt, pts, 0.45 * h / 10, 0.16 * h / 10)
    tops = []
    for k in range(4):   # degë
        s = pts[random.randint(4, 6)]
        d = Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(0.4, 0.9))).normalized()
        e = s + d * h * random.uniform(0.25, 0.4)
        tube(bt, [s, s.lerp(e, 0.5) + Vector((0, 0, 0.3)), e], 0.12 * h / 10, 0.05 * h / 10, 6)
        tops.append(e)
    tops.append(pts[-1])
    trunk = new_obj(name + "_Trungu", bt, M_BARK); box_uv(trunk, 0.5)
    bl = bmesh.new()
    for i, c in enumerate(tops):
        for j in range(4):
            blob(bl, c + Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-0.3, 0.5))) * h * 0.08,
                 h * 0.25 * crown * random.uniform(0.8, 1.2), seed * 10 + i * 3 + j)
    leaves = new_obj(name + "_Gjethet", bl, M_LEAF if seed % 2 else M_LEAF2); box_uv(leaves, 0.5)
    leaves.parent = trunk
    return trunk

T = [tree("Pema_1", 9, 0.8, 1), tree("Pema_2", 12, 1.4, 2, 0.9), tree("Pema_3", 7, 0.5, 3, 1.2)]

# shkurre dhe fier
bb = bmesh.new()
for i in range(5):
    blob(bb, Vector((random.uniform(-0.8, 0.8), random.uniform(-0.8, 0.8), 0.5)), random.uniform(0.6, 0.9), 50 + i, 0.7)
bush = new_obj("Shkurre", bb, M_LEAF2); box_uv(bush, 0.5)
bf = bmesh.new()
for i in range(9):
    a = i / 9 * 2 * math.pi
    d = Vector((math.cos(a), math.sin(a), 0))
    s = d * 0.05; m_ = d * 0.6 + Vector((0, 0, 0.45)); e = d * 1.1 + Vector((0, 0, 0.15))
    side = d.cross(Vector((0, 0, 1))) * 0.13
    vs = [bf.verts.new(s), bf.verts.new(m_ + side), bf.verts.new(e), bf.verts.new(m_ - side)]
    bf.faces.new(vs)
fern = new_obj("Fieri", bf, M_LEAF, False); box_uv(fern, 1.0)

# rreshto objektet larg njëri-tjetrit (në Unity merren veç e veç)
x = 0
for o in R + T + [bush, fern]:
    o.location = (x, 0, 0); x += 30
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_tspace=True)
bpy.ops.wm.save_as_mainfile(filepath=out.replace(".fbx", ".blend"))
print("NATYRA GATI", out, [o.name for o in bpy.data.objects])

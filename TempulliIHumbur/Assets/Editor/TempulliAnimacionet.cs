using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Animacionet e Artës në Unity (thirret nga TempulliSetup):
//  1. Importi i arta.fbx: Humanoid Avatar (që animacionet Mixamo/të tjera të ripërdoren), 14 klipe nga
//     Assets/Models/arta_klipet.json, "bake into pose" (lëvizja drejtohet nga kodi, jo root motion),
//     ngjarje animacioni (hapat, shtytja, ulja, rrëshqitja), kompresim "Optimal", maks. 4 kocka/kulm.
//  2. Animator Controller: vrap/sprint (blend tree sipas shpejtësisë), kërcim në 4 faza (ngritje, ajër, rënie, ulje),
//     rrëshqitje në 3 faza (hyrje, cikël, dalje), kthesa majtas/djathtas, litari, pengimi, vdekja.
//  3. Animation Rigging: koka shikon nga drejtimi/kthesa e ardhshme, duart kapin litarin (IK).
public static class TempulliAnimacionet
{
    public const string ModelPath = "Assets/Models/arta.fbx";
    const string ClipsJson = "Assets/Models/arta_klipet.json";

    // klipet e paracaktuara (nëse mungon json-i)
    static readonly (string name, int a, int b, bool loop)[] DefaultClips = {
        ("Run", 1, 21, true), ("Sprint", 25, 41, true), ("JumpUp", 51, 59, false), ("JumpAir", 61, 73, true),
        ("JumpFall", 76, 88, true), ("Land", 91, 101, false), ("SlideIn", 111, 119, false), ("SlideLoop", 121, 137, true),
        ("SlideOut", 141, 149, false), ("TurnL", 151, 163, false), ("TurnR", 171, 183, false), ("Hang", 191, 211, true),
        ("Stumble", 221, 241, false), ("Death", 251, 281, false) };

    // ngjarjet e animacionit: (klipi, koha 0-1, funksioni te ArtaAnimEvents)
    static readonly (string clip, float t, string fn)[] Events = {
        ("Run", 0.05f, "Hapi"), ("Run", 0.55f, "Hapi"), ("Sprint", 0.05f, "Hapi"), ("Sprint", 0.55f, "Hapi"),
        ("JumpUp", 0.45f, "Shtytja"), ("Land", 0.18f, "Ulja"), ("SlideIn", 0.75f, "Rreshqitja"),
        ("TurnL", 0.3f, "Hapi"), ("TurnR", 0.3f, "Hapi"), ("Stumble", 0.3f, "Hapi") };

    static List<(string name, int a, int b, bool loop)> ReadClips()
    {
        var list = new List<(string, int, int, bool)>();
        if (File.Exists(ClipsJson))
        {
            // formati: {"Run": [1, 21, true], ...}
            string s = File.ReadAllText(ClipsJson);
            foreach (var part in s.Split('}')[0].Trim('{', ' ', '\n', '\r').Split(']'))
            {
                var p = part.Trim(',', ' ', '\n', '\r');
                if (p.Length == 0) continue;
                int q1 = p.IndexOf('"'), q2 = p.IndexOf('"', q1 + 1), br = p.IndexOf('[');
                if (q1 < 0 || q2 < 0 || br < 0) continue;
                var nums = p.Substring(br + 1).Split(',');
                if (nums.Length < 3) continue;
                list.Add((p.Substring(q1 + 1, q2 - q1 - 1), int.Parse(nums[0].Trim()), int.Parse(nums[1].Trim()), nums[2].Trim().ToLower() == "true"));
            }
        }
        if (list.Count == 0) list.AddRange(DefaultClips);
        return list;
    }

    // ---------------- 1. importi ----------------
    public static Dictionary<string, AnimationClip> ImportModel()
    {
        var clips = new Dictionary<string, AnimationClip>();
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return clips;
        var defs = ReadClips();
        bool needs = importer.clipAnimations.Length != defs.Count || importer.userData != "tempulli-v5";
        if (needs)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = true;
            importer.importBlendShapes = false;
            importer.importCameras = false; importer.importLights = false;
            importer.skinWeights = ModelImporterSkinWeights.Custom; importer.maxBonesPerVertex = 4;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.optimizeMeshPolygons = true; importer.optimizeMeshVertices = true;
            var take = importer.defaultClipAnimations.Length > 0 ? importer.defaultClipAnimations[0].takeName : "Scene";
            var list = new List<ModelImporterClipAnimation>();
            foreach (var d in defs)
            {
                var c = new ModelImporterClipAnimation
                {
                    name = d.name, takeName = take, firstFrame = d.a, lastFrame = d.b,
                    loopTime = d.loop, loopPose = d.loop, loop = d.loop,
                    // lëvizja përpara/anash dhe rrotullimi vijnë nga kodi: gjithçka "bake into pose"
                    lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true,
                    keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true,
                };
                var ev = new List<AnimationEvent>();
                foreach (var e in Events) if (e.clip == d.name) ev.Add(new AnimationEvent { time = e.t, functionName = e.fn });
                c.events = ev.ToArray();
                list.Add(c);
            }
            importer.clipAnimations = list.ToArray();
            importer.userData = "tempulli-v5";
            importer.SaveAndReimport();
            // kontroll: nëse Avatar-i humanoid s'del i vlefshëm, kthehu te Generic (loja vazhdon të punojë)
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            if (avatar == null) foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath)) if (o is Avatar av) avatar = av;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("Tempulli: Avatar-i Humanoid nuk u krijua saktë — përdoret Generic.");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }
            else Debug.Log("Tempulli: Arta u importua si Humanoid (" + defs.Count + " klipe, me ngjarje animacioni).");
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) clips[c.name] = c;
        return clips;
    }

    // ---------------- 2. Animator Controller ----------------
    static AnimatorStateTransition T(AnimatorState from, AnimatorState to, float dur, float exit = -1f, float offset = 0f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = exit >= 0f; if (exit >= 0f) t.exitTime = exit;
        t.duration = dur; t.offset = offset; t.hasFixedDuration = true;
        t.interruptionSource = TransitionInterruptionSource.Destination;
        return t;
    }
    static AnimatorStateTransition A(AnimatorStateMachine sm, AnimatorState to, float dur)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false; t.duration = dur; t.hasFixedDuration = true; t.canTransitionToSelf = false;
        return t;
    }

    public static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> c, float startSpeed, float maxSpeed)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in new[] { "Speed", "VelY" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        foreach (var p in new[] { "Grounded", "Sliding", "Hanging", "FromSlide" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Bool);
        foreach (var p in new[] { "Jump", "Slide", "TurnL", "TurnR", "Stumble", "Die" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);
        // (të vjetrat, që kodi i vjetër të mos japë paralajmërime)
        ctrl.AddParameter(new AnimatorControllerParameter { name = "JumpSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter(new AnimatorControllerParameter { name = "SlideSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimationClip G(string n) => c.TryGetValue(n, out var x) ? x : null;
        var sm = ctrl.layers[0].stateMachine;

        // vrapi: blend tree Run -> Sprint sipas shpejtësisë; ritmi i hapave me RunSpeed
        var loco = ctrl.CreateBlendTreeInController("Vrapi", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
        tree.AddChild(G("Run"), startSpeed);
        tree.AddChild(G("Sprint") ?? G("Run"), maxSpeed);
        loco.speedParameter = "RunSpeed"; loco.speedParameterActive = true;
        loco.iKOnFeet = true;
        sm.defaultState = loco;

        AnimatorState S(string n, string clip, bool ik = false)
        {
            var s = sm.AddState(n); s.motion = G(clip) ?? G("Run"); s.iKOnFeet = ik; return s;
        }
        var jUp = S("Kercimi_Ngritje", "JumpUp");
        var jAir = S("Kercimi_Ajer", "JumpAir");
        var jFall = S("Kercimi_Renie", "JumpFall");
        var land = S("Ulja", "Land", true);
        var sIn = S("Rreshqitja_Hyrje", "SlideIn");
        var sLoop = S("Rreshqitja", "SlideLoop");
        var sOut = S("Rreshqitja_Dalje", "SlideOut", true);
        var tL = S("Kthesa_Majtas", "TurnL", true);
        var tR = S("Kthesa_Djathtas", "TurnR", true);
        var hang = S("Litari", "Hang");
        var stum = S("Pengimi", "Stumble");
        var death = S("Vdekja", "Death");

        // kërcimi: hyn menjëherë; ajri zgjat sa zgjat fizika; rënia kur shpejtësia vertikale bëhet negative; ulja kur prek tokën
        var aj = A(sm, jUp, 0.12f); aj.AddCondition(AnimatorConditionMode.If, 0, "Jump"); aj.AddCondition(AnimatorConditionMode.IfNot, 0, "FromSlide");
        // kërcim nga rrëshqitja: trupi duhet të ngrihet nga toka, ndaj përzierja zgjat pak më shumë (pa këputje te krahët)
        var ajs = A(sm, jUp, 0.2f); ajs.AddCondition(AnimatorConditionMode.If, 0, "Jump"); ajs.AddCondition(AnimatorConditionMode.If, 0, "FromSlide");
        T(jUp, jAir, 0.08f, 1f);
        T(jUp, jFall, 0.15f).AddCondition(AnimatorConditionMode.Less, -1f, "VelY");
        T(jAir, jFall, 0.2f).AddCondition(AnimatorConditionMode.Less, -1f, "VelY");
        foreach (var from in new[] { jAir, jFall })
            T(from, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        var upLand = T(jUp, land, 0.05f, 0.6f); upLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        T(land, loco, 0.12f, 0.82f);
        // bie pa kërcyer (p.sh. në fund të litarit ose nga skaji)
        var lf = T(loco, jFall, 0.2f); lf.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); lf.AddCondition(AnimatorConditionMode.Less, -5f, "VelY");

        // rrëshqitja: hyrje -> cikël (sa zgjat Sliding) -> dalje -> vrap
        A(sm, sIn, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Slide");
        T(sIn, sLoop, 0.05f, 1f);
        T(sIn, sOut, 0.06f).AddCondition(AnimatorConditionMode.IfNot, 0, "Sliding");
        T(sLoop, sOut, 0.06f).AddCondition(AnimatorConditionMode.IfNot, 0, "Sliding");
        T(sOut, loco, 0.1f, 0.9f);

        // kthesat: këmbët vazhdojnë hapin; mbarojnë në fazën 0.6 të vrapit, ndaj kthehen atje (pa këputje)
        T(loco, tL, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "TurnL");
        T(loco, tR, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "TurnR");
        T(tL, loco, 0.12f, 0.9f, 0.6f);
        T(tR, loco, 0.12f, 0.9f, 0.6f);

        // litari, pengimi, vdekja
        A(sm, hang, 0.12f).AddCondition(AnimatorConditionMode.If, 0, "Hanging");
        var hOut = T(hang, jFall, 0.2f); hOut.AddCondition(AnimatorConditionMode.IfNot, 0, "Hanging");
        A(sm, stum, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "Stumble");
        T(stum, loco, 0.15f, 0.85f);
        A(sm, death, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Die");
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ---------------- 3. Animation Rigging ----------------
    static Transform Bone(Animator anim, HumanBodyBones hb, string generic)
    {
        Transform t = null;
        if (anim.isHuman) t = anim.GetBoneTransform(hb);
        return t != null ? t : Find(anim.transform, generic);
    }
    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
        return null;
    }
    static MultiAimConstraintData.Axis BestAxis(Transform b, Vector3 dir, MultiAimConstraintData.Axis? not = null)
    {
        var axes = new[] { (MultiAimConstraintData.Axis.X, Vector3.right), (MultiAimConstraintData.Axis.X_NEG, Vector3.left),
                           (MultiAimConstraintData.Axis.Y, Vector3.up), (MultiAimConstraintData.Axis.Y_NEG, Vector3.down),
                           (MultiAimConstraintData.Axis.Z, Vector3.forward), (MultiAimConstraintData.Axis.Z_NEG, Vector3.back) };
        float best = -2f; var res = MultiAimConstraintData.Axis.Z;
        foreach (var (ax, v) in axes)
        {
            if (not.HasValue && ((int)ax / 2 == (int)not.Value / 2)) continue;
            float d = Vector3.Dot(b.TransformDirection(v).normalized, dir);
            if (d > best) { best = d; res = ax; }
        }
        return res;
    }

    public static void SetupRig(GameObject arta, PlayerController pc)
    {
        var anim = arta.GetComponent<Animator>();
        if (anim == null) return;
        Transform head = Bone(anim, HumanBodyBones.Head, "mixamorig:Head"), neck = Bone(anim, HumanBodyBones.Neck, "mixamorig:Neck");
        Transform lu = Bone(anim, HumanBodyBones.LeftUpperArm, "mixamorig:LeftArm"), ll = Bone(anim, HumanBodyBones.LeftLowerArm, "mixamorig:LeftForeArm"), lh = Bone(anim, HumanBodyBones.LeftHand, "mixamorig:LeftHand");
        Transform ru = Bone(anim, HumanBodyBones.RightUpperArm, "mixamorig:RightArm"), rl = Bone(anim, HumanBodyBones.RightLowerArm, "mixamorig:RightForeArm"), rh = Bone(anim, HumanBodyBones.RightHand, "mixamorig:RightHand");
        if (head == null || lu == null || ru == null) { Debug.LogWarning("Tempulli: kockat për Animation Rigging nuk u gjetën."); return; }

        var player = pc.transform;
        var caqet = new GameObject("Caqet_e_Rig").transform; caqet.SetParent(player, false);
        Transform Target(string n, Vector3 local) { var t = new GameObject(n).transform; t.SetParent(caqet, false); t.localPosition = local; return t; }
        var look = Target("Shikimi", new Vector3(0, 1.65f, 12f));
        var tl = Target("Dora_Majtas", new Vector3(-0.15f, pc.hangOffset, 0.05f));
        var tr = Target("Dora_Djathtas", new Vector3(0.15f, pc.hangOffset, 0.05f));
        var hl = Target("Berryli_Majtas", new Vector3(-0.6f, 1.6f, -0.4f));
        var hr = Target("Berryli_Djathtas", new Vector3(0.6f, 1.6f, -0.4f));

        var rigGo = new GameObject("Rig_Arta"); rigGo.transform.SetParent(arta.transform, false);
        var rig = rigGo.AddComponent<Rig>(); rig.weight = 1f;
        var rb = arta.GetComponent<RigBuilder>(); if (rb == null) rb = arta.AddComponent<RigBuilder>();
        rb.layers.Clear(); rb.layers.Add(new RigLayer(rig, true));

        MultiAimConstraint Aim(string n, Transform bone, float w, float limit)
        {
            var go = new GameObject(n); go.transform.SetParent(rigGo.transform, false);
            var c = go.AddComponent<MultiAimConstraint>();
            c.weight = w;
            c.data.constrainedObject = bone;
            var src = new WeightedTransformArray(); src.Add(new WeightedTransform(look, 1f));
            c.data.sourceObjects = src;
            c.data.aimAxis = BestAxis(bone, player.forward);
            c.data.upAxis = BestAxis(bone, Vector3.up, c.data.aimAxis);
            c.data.maintainOffset = true;
            c.data.limits = new Vector2(-limit, limit);
            c.data.constrainedXAxis = c.data.constrainedYAxis = c.data.constrainedZAxis = true;
            return c;
        }
        var aimNeck = neck != null ? Aim("Shikimi_Qafa", neck, 0.35f, 30f) : null;
        var aimHead = Aim("Shikimi_Koka", head, 0.65f, 45f);

        TwoBoneIKConstraint IK(string n, Transform a, Transform b, Transform c, Transform target, Transform hint)
        {
            var go = new GameObject(n); go.transform.SetParent(rigGo.transform, false);
            var k = go.AddComponent<TwoBoneIKConstraint>();
            k.weight = 0f;
            k.data.root = a; k.data.mid = b; k.data.tip = c; k.data.target = target; k.data.hint = hint;
            k.data.targetPositionWeight = 1f; k.data.targetRotationWeight = 0f; k.data.hintWeight = 0.6f;
            return k;
        }
        var ikL = IK("Dora_Majtas_IK", lu, ll, lh, tl, hl);
        var ikR = IK("Dora_Djathtas_IK", ru, rl, rh, tr, hr);

        var drv = arta.AddComponent<ArtaRig>();
        drv.player = pc; drv.headAim = aimHead; drv.neckAim = aimNeck; drv.handL = ikL; drv.handR = ikR;
        drv.lookTarget = look; drv.handTargetL = tl; drv.handTargetR = tr;
        arta.AddComponent<ArtaAnimEvents>().player = pc;
        Debug.Log("Tempulli: Animation Rigging gati (koka " + aimHead.data.aimAxis + ", duart IK te litari).");
    }
}

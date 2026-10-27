using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Tempulli i Humbur - ndërton skenën e lojës automatikisht.
// Menuja: Tempulli > Ndërto skenën e lojës
// Herën e parë që hapet projekti, ekzekutohet vetë nëse skena Game nuk ekziston.
[InitializeOnLoad]
public static class TempulliSetup
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string ModelPath = "Assets/Models/arta.fbx";
    const string OldModelPath = "Assets/Models/arta_realiste.fbx";
    const string ControllerPath = "Assets/Animations/ArtaAnimator.controller";

    static TempulliSetup()
    {
        if (!File.Exists(ScenePath))
            EditorApplication.delayCall += WaitAndBuild;
    }

    static void WaitAndBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += WaitAndBuild;
            return;
        }
        BuildScene();
    }

    [MenuItem("Tempulli/Ndërto skenën e lojës")]
    public static void BuildScene()
    {
        foreach (var f in new[] { "Models", "Materials", "Scenes", "Prefabs", "Animations", "Scripts" })
            if (!AssetDatabase.IsValidFolder("Assets/" + f)) AssetDatabase.CreateFolder("Assets", f);

        // 1. Arta (arta.fbx nga Blender, me skelet dhe animacione)
        string modelPath = File.Exists(ModelPath) ? ModelPath : (File.Exists(OldModelPath) ? OldModelPath : null);
        // 1-2. Arta: import Humanoid + 14 klipe me ngjarje, dhe Animator Controller-i (shih TempulliAnimacionet.cs)
        AnimatorController controller = null;
        if (modelPath == ModelPath)
        {
            var clips = TempulliAnimacionet.ImportModel();
            ArtaMaterials(AssetImporter.GetAtPath(ModelPath) as ModelImporter);
            controller = TempulliAnimacionet.BuildController(ControllerPath, clips, 10f, 18f);
        }
        // celular: deri në 4 kocka për kulm (përndryshe gjunjët/bërrylat deformohen në cilësi të ulët)
        QualitySettings.skinWeights = SkinWeights.FourBones;

        // 3. Skena e re
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.93f, 0.82f, 0.72f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 280f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.70f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.72f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.55f, 0.48f, 0.50f);
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Qielli.mat");
        if (sky == null)
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, "Assets/Materials/Qielli.mat"); }
        }
        if (sky != null)
        {
            sky.SetFloat("_SunSize", 0.06f);
            sky.SetFloat("_SunSizeConvergence", 3f);
            sky.SetFloat("_AtmosphereThickness", 1.45f);
            sky.SetColor("_SkyTint", new Color(0.50f, 0.56f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.95f, 0.86f, 0.78f));
            sky.SetFloat("_Exposure", 1.25f);
            RenderSettings.skybox = sky;
        }

        Material stone = MakeMat("Guri", new Color(0.70f, 0.62f, 0.50f), 0.12f);
        Material stoneDark = MakeMat("GuriErret", new Color(0.42f, 0.38f, 0.32f), 0.08f);
        Material moss = MakeMat("Myshku", new Color(0.22f, 0.36f, 0.14f), 0.05f);
        Material gold = MakeMat("Ari", new Color(1f, 0.74f, 0.2f), 0.85f, 1f, new Color(0.35f, 0.22f, 0f));
        Material wood = MakeMat("Druri", new Color(0.33f, 0.20f, 0.11f), 0.2f);
        Material leaves = MakeMat("Gjethet", new Color(0.22f, 0.40f, 0.12f), 0.15f);
        Material bark = MakeMat("Lëvorja", new Color(0.26f, 0.18f, 0.12f), 0.05f);
        Material glow = MakeMat("Shigjeta", new Color(1f, 0.8f, 0.3f), 0.5f, 0f, new Color(1.2f, 0.8f, 0.2f));
        Material eyes = MakeMat("SytERojtarit", new Color(1f, 0.2f, 0.05f), 0.5f, 0f, new Color(3f, 0.5f, 0.1f));
        // materialet me tekstura të tempullit mbi re
        Material deckM = TexMat("T_Shtegu", "shtegu", new Color(1f, 0.97f, 0.9f), 0.18f);
        Material wallM = TexMat("T_Muri", "muri", new Color(0.95f, 0.93f, 1f), 0.08f);
        Material carvedM = TexMat("T_Gdhendja", "gdhendja", new Color(1f, 0.95f, 0.85f), 0.12f);
        Material roofM = TexMat("T_Tjegulla", "tjegulla", Color.white, 0.25f);
        Material rockM = TexMat("T_Shkembi", "shkembi", new Color(0.95f, 0.92f, 1f), 0.05f);
        // Shader Graph: gur me myshk (alfa e ngjyrës = sasia e myshkut). Nëse grafi s'ngarkohet, mbeten materialet e mësipërme.
        wallM = TempulliPaketaPerdorimi.SGMat("SG_Muri", "SG_GuriMyshk.shadergraph", wallM, new Color(0.95f, 0.93f, 1f, 0.9f), 0.08f);
        carvedM = TempulliPaketaPerdorimi.SGMat("SG_Gdhendja", "SG_GuriMyshk.shadergraph", carvedM, new Color(1f, 0.95f, 0.85f, 0.5f), 0.12f);
        rockM = TempulliPaketaPerdorimi.SGMat("SG_Shkembi", "SG_GuriMyshk.shadergraph", rockM, new Color(0.95f, 0.92f, 1f, 1f), 0.05f);
        deckM = TempulliPaketaPerdorimi.SGMat("SG_Shtegu", "SG_GuriMyshk.shadergraph", deckM, new Color(1f, 0.97f, 0.9f, 0.3f), 0.18f);
        Material cloudM = CloudMat("T_Ret", "ret");
        Material puffM = MakeMat("T_Re_Fryre", new Color(1f, 0.90f, 0.84f), 0f, 0f, new Color(0.28f, 0.2f, 0.18f));
        Material cloudBaseM = MakeMat("T_Ret_Baza", new Color(1f, 0.86f, 0.78f), 0f, 0f, new Color(0.45f, 0.33f, 0.3f));

        // 4. Lojtari
        var player = new GameObject("Lojtari");
        player.transform.position = new Vector3(0, 0.05f, 0);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.95f; cc.radius = 0.42f; cc.center = new Vector3(0, 0.975f, 0); cc.stepOffset = 0.3f;
        var pc = player.AddComponent<PlayerController>();
        {
            var sp2 = new GameObject("Sperkatjet");
            sp2.transform.SetParent(player.transform, false);
            sp2.transform.localPosition = new Vector3(0, 0.15f, 0.2f);
            var ps = sp2.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = 0.5f; main.startSpeed = 3.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.8f); main.gravityModifier = 1.4f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 70f; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.35f;
            sp2.transform.localRotation = Quaternion.Euler(-60f, 0, 0);
            var pr = sp2.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = ParticleMat("Sperkat", new Color(0.85f, 0.95f, 1f, 0.85f));
            pc.splash = ps;
            // pluhuri kur ulet dhe shkëndijat e monedhave (Emit nga PlayerController)
            pc.dust = Burst(player.transform, "Pluhuri", new Vector3(0, 0.1f, 0), new Color(0.85f, 0.72f, 0.5f, 0.7f), 0.7f, 2.2f, 0.15f, 0.45f, 0.5f);
            pc.sparkle = Burst(null, "Shkendijat", Vector3.zero, new Color(1f, 0.85f, 0.3f, 1f), 0.45f, 4f, 0.05f, 0.14f, -0.3f);
        }
        // VFX Graph: pluhur i artë në ajër dhe mjegull e ulët që lëvizin me Artën
        TempulliPaketaPerdorimi.AddAmbientVFX(player.transform);

        var modelAsset = modelPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset != null)
        {
            var arta = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            arta.name = "Arta";
            arta.transform.SetParent(player.transform, false);
            arta.transform.localPosition = Vector3.zero;
            arta.transform.localRotation = Quaternion.identity;

            var anim = arta.GetComponent<Animator>();
            if (anim == null) anim = arta.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            pc.animator = anim;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal;

            var rends = arta.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                // lartësia e Artës ~1.95 m (pak më e madhe se më parë, që të duket mirë nga kamera)
                if (b.size.y > 0.1f) arta.transform.localScale *= 1.95f / b.size.y;
            }
            // Kontrollo që shikon përpara (+Z): gishtat duhet të jenë para thembrës
            Transform foot = FindDeep(arta.transform, "Foot.L") ?? FindDeep(arta.transform, "mixamorig:LeftFoot"), toes = FindDeep(arta.transform, "Toe.L") ?? FindDeep(arta.transform, "mixamorig:LeftToeBase");
            if (foot != null && toes != null && toes.position.z < foot.position.z)
                arta.transform.localRotation = Quaternion.Euler(0, 180, 0);
            // Animation Rigging: koka shikon nga kthesa, duart kapin litarin
            TempulliAnimacionet.SetupRig(arta, pc);
        }
        else
        {
            Debug.Log("Tempulli: arta.fbx nuk u gjet. Lojtari përkohësisht është kapsulë. Vendose modelin te Assets/Models dhe zgjidh Tempulli > Ndërto skenën e lojës.");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Trupi";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = MakeMat("Lojtari", new Color(0.85f, 0.35f, 0.2f));
            Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        // 5. Shtegu pa fund + loja
        var game = new GameObject("Loja");
        var spawner = game.AddComponent<TrackSpawner>();
        spawner.player = pc;
        spawner.stone = stone; spawner.stoneDark = stoneDark; spawner.moss = moss; spawner.gold = gold; spawner.wood = wood;
        spawner.leaves = leaves; spawner.bark = bark; spawner.glow = glow;
        // tre nuanca guri për dyshemenë: çdo segment merr njërën (shtegu nuk duket i kopjuar)
        spawner.deckVariants = new[] { deckM,
            TempulliPaketaPerdorimi.SGMat("SG_Shtegu_B", "SG_GuriMyshk.shadergraph", TexMat("T_Shtegu_B", "shtegu", new Color(0.93f, 0.92f, 0.86f), 0.16f), new Color(0.93f, 0.92f, 0.86f, 0.4f), 0.16f),
            TempulliPaketaPerdorimi.SGMat("SG_Shtegu_C", "SG_GuriMyshk.shadergraph", TexMat("T_Shtegu_C", "shtegu", new Color(1f, 0.92f, 0.78f), 0.2f), new Color(1f, 0.92f, 0.78f, 0.2f), 0.2f) };
        // ProBuilder: harku i portës dhe kolonat (meshe të ruajtura në Assets/Models/PB_*.asset)
        spawner.archMesh = TempulliPaketaPerdorimi.ArchMesh(spawner.pathWidth * 0.5f + 1.85f, 1.5f, 1.8f);
        spawner.columnMesh = TempulliPaketaPerdorimi.ColumnMesh();
        spawner.pathWidth = 6.6f; spawner.laneWidth = 2.0f;
        spawner.deck = deckM; spawner.wall = wallM; spawner.carved = carvedM; spawner.roof = roofM; spawner.rock = rockM;
        spawner.cloud = cloudM; spawner.cloudPuff = puffM; spawner.cloudBase = cloudBaseM;
        spawner.nature = NatureModel(rockM, bark, leaves, moss);
        spawner.riverbed = TexMat("T_Shtrati", "shtrati", Color.white, 0.35f);
        var barkT = TexMat("T_Levorja", "levorja", Color.white, 0.1f);
        barkT.SetTextureScale("_BaseMap", new Vector2(2f, 4f)); EditorUtility.SetDirty(barkT);
        spawner.barkTex = barkT;
        spawner.water = TempulliPaketaPerdorimi.SGMat("SG_Uji", "SG_Uji.shadergraph", WaterMat(), new Color(0.22f, 0.48f, 0.52f, 0.72f), 0.92f, 0.6f);
        spawner.grass = GrassMat();
        var chaserGo = new GameObject("Rojtari");
        chaserGo.transform.position = new Vector3(0, 0, -2.5f);
        var chaser = chaserGo.AddComponent<Chaser>();
        chaser.player = pc; chaser.stone = stoneDark; chaser.moss = moss; chaser.eyes = eyes;
        var gm = game.AddComponent<GameManager>();
        // Pamje paraprake e shtegut në editor (fshihet vetë kur shtyp Play)
        var preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        preview.name = "Shtegu_Paraprak";
        preview.tag = "EditorOnly";
        preview.transform.position = new Vector3(0, -0.5f, 90);
        preview.transform.localScale = new Vector3(6.6f, 1, 200);
        preview.GetComponent<Renderer>().sharedMaterial = deckM;
        Object.DestroyImmediate(preview.GetComponent<Collider>());
        gm.player = pc;

        // 6. Kamera dhe drita
        var cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = 400f;
            cam.nearClipPlane = 0.1f;
            cam.transform.position = new Vector3(0, 4f, -6.8f);
            cam.transform.rotation = Quaternion.Euler(18f, 0, 0);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.player = pc;
            // kamera më afër, mbi sup (si Temple Run 2): Arta zë ~1/5 e ekranit, në mes-poshtë
            follow.back = 4.4f; follow.up = 3.0f; follow.lookAhead = 11f; follow.lookHeight = 1.6f;
            follow.baseFov = 60f; follow.speedFov = 8f; follow.smooth = 9f; follow.turnSpeed = 9f;
            cam.transform.position = new Vector3(0, 3.0f, -4.4f);
            gm.cam = follow;
            // Cinemachine: CM_Vrapi ndjek caqet që llogarit CameraFollow; CM_Vdekja merr pamjen kur Arta bie
            var brain = cam.gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.1f);
            var cmRoot = new GameObject("Kamerat_Cinemachine").transform;
            var bodyT = new GameObject("CM_Caku_Trupi").transform; bodyT.SetParent(cmRoot); bodyT.position = cam.transform.position;
            var lookT = new GameObject("CM_Caku_Shikimi").transform; lookT.SetParent(cmRoot); lookT.position = new Vector3(0, 1.6f, 11f);
            var lens = LensSettings.Default; lens.FieldOfView = 60f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 400f;
            var cmRun = new GameObject("CM_Vrapi").AddComponent<CinemachineCamera>();
            cmRun.transform.SetParent(cmRoot); cmRun.transform.position = cam.transform.position;
            cmRun.Priority = 10; cmRun.Follow = bodyT; cmRun.LookAt = lookT; cmRun.Lens = lens;
            cmRun.gameObject.AddComponent<CinemachineHardLockToTarget>();
            cmRun.gameObject.AddComponent<CinemachineHardLookAt>();
            var listener = cmRun.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1; listener.Gain = 1f; listener.UseCameraSpace = true;
            var cmDeath = new GameObject("CM_Vdekja").AddComponent<CinemachineCamera>();
            cmDeath.transform.SetParent(cmRoot);
            cmDeath.Priority = 0; cmDeath.Follow = player.transform; cmDeath.LookAt = player.transform; cmDeath.Lens = lens;
            var dFollow = cmDeath.gameObject.AddComponent<CinemachineFollow>();
            dFollow.FollowOffset = new Vector3(1.8f, 2.3f, 4.4f);   // përpara dhe anash Artës, pak lart
            dFollow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            dFollow.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            cmDeath.gameObject.AddComponent<CinemachineHardLookAt>().LookAtOffset = new Vector3(0, 0.5f, 0);
            var imp = cam.gameObject.AddComponent<CinemachineImpulseSource>();
            imp.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            imp.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            imp.ImpulseDefinition.ImpulseDuration = 0.3f;
            imp.DefaultVelocity = new Vector3(0.25f, -1f, 0f);
            follow.cmRun = cmRun; follow.cmDeath = cmDeath; follow.cmBody = bodyT; follow.cmLook = lookT; follow.impulse = imp;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            // diell më i ulët dhe më i ngrohtë: hije të gjata që theksojnë relievin e gurit
            sun.transform.rotation = Quaternion.Euler(24f, 205f, 0f);
            sun.color = new Color(1f, 0.80f, 0.58f);
            sun.intensity = 2.0f;
            sun.shadowStrength = 0.85f;
            sun.shadows = LightShadows.Soft;
        }

        // Efektet e kamerës: tonemapping, bloom, vinjetë, ngjyra më të ngrohta
        string profPath = "Assets/Settings/Tempulli_Efektet.asset";
        AssetDatabase.DeleteAsset(profPath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profPath);
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.45f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.6f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.4f);
        var col = profile.Add<ColorAdjustments>(true); col.postExposure.Override(0.25f); col.contrast.Override(12f); col.saturation.Override(8f);
        col.colorFilter.Override(new Color(1f, 0.96f, 0.88f));
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        var volGo = new GameObject("Efektet");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        foreach (var rpPath in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            if (rp != null)
            {
                rp.shadowDistance = 70f;
                rp.shadowCascadeCount = rpPath.Contains("PC") ? 2 : 1;
                EditorUtility.SetDirty(rp);
            }
        }
        // SSAO (ekziston te PC_Renderer): pak më i fortë, që cepat e gurit dhe bazat e kolonave të kenë thellësi
        var pcData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
        if (pcData != null)
        {
            foreach (var f in pcData.rendererFeatures)
            {
                if (f == null || !f.GetType().Name.Contains("AmbientOcclusion")) continue;
                var so = new SerializedObject(f);
                var inten = so.FindProperty("m_Settings.Intensity");
                var rad = so.FindProperty("m_Settings.Radius");
                if (inten != null) inten.floatValue = 0.75f;
                if (rad != null) rad.floatValue = 0.35f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f);
            }
        }

        // 7. Ruaj dhe vendos në Build Settings
        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(x => x.path == ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("Tempulli: skena e lojës u ndërtua (" + ScenePath + "). Kliko Play.");
    }

    // Materialet realiste të Artës (URP Lit) nga teksturat në Assets/Models/arta_tekstura
    const string ArtaTex = "Assets/Models/arta_tekstura/";
    static void ArtaMaterials(ModelImporter importer)
    {
        if (importer == null || !AssetDatabase.IsValidFolder("Assets/Models/arta_tekstura")) return;
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Arta")) AssetDatabase.CreateFolder("Assets/Materials", "Arta");
        var nti = AssetImporter.GetAtPath(ArtaTex + "arta_rrobat_normal.png") as TextureImporter;
        if (nti != null && nti.textureType != TextureImporterType.NormalMap) { nti.textureType = TextureImporterType.NormalMap; nti.SaveAndReimport(); }
        foreach (var n in new[] { "arta_floket_diffuse.png", "arta_vetullat.png", "arta_qerpiket.png" })
        {
            var ti = AssetImporter.GetAtPath(ArtaTex + n) as TextureImporter;
            if (ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f; ti.SaveAndReimport(); }
        }
        var mats = new Dictionary<string, Material>
        {
            { "Arta_Lekura",   ArtaMat("Arta_Lekura", "arta_lekura_diffuse.png", Color.white, 0.42f) },
            { "Arta_Syte",     ArtaMat("Arta_Syte", "arta_syte_diffuse.png", Color.white, 0.92f) },
            { "Arta_Vetullat", ArtaMat("Arta_Vetullat", "arta_vetullat.png", Color.white, 0.1f, 0.35f) },
            { "Arta_Qerpiket", ArtaMat("Arta_Qerpiket", "arta_qerpiket.png", Color.white, 0.1f, 0.3f) },
            { "Arta_Dhembet",  ArtaMat("Arta_Dhembet", "arta_dhembet.png", Color.white, 0.6f) },
            { "Arta_Floket",   ArtaMat("Arta_Floket", "arta_floket_diffuse.png", Color.white, 0.5f, 0.45f) },
            { "Arta_Rrobat",   ArtaMat("Arta_Rrobat", "arta_rrobat_diffuse.png", Color.white, 0.18f, -1f, "arta_rrobat_normal.png") },
            { "Arta_Cizmet",   ArtaMat("Arta_Cizmet", "arta_cizmet_diffuse.png", Color.white, 0.4f) },
            { "Arta_Kuqe",     ArtaMat("Arta_Kuqe", null, new Color(0.45f, 0.06f, 0.05f), 0.15f) },
            { "Arta_Lekure",   ArtaMat("Arta_Lekure", null, new Color(0.26f, 0.14f, 0.06f), 0.4f) },
            { "Arta_Bronz",    ArtaMat("Arta_Bronz", null, new Color(0.75f, 0.55f, 0.25f), 0.65f, -1f, null, 1f) },
        };
        bool changed = false;
        var existing = importer.GetExternalObjectMap();
        foreach (var kv in mats)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            if (existing.TryGetValue(id, out var cur) && cur == kv.Value) continue;
            importer.AddRemap(id, kv.Value); changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    static Material ArtaMat(string name, string tex, Color c, float smooth, float alphaClip = -1f, string normal = null, float metal = 0f)
    {
        var m = MakeMat("Arta/" + name, c, smooth, metal);
        var t = tex == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(ArtaTex + tex);
        if (t != null) { m.SetTexture("_BaseMap", t); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t); }
        if (normal != null)
        {
            var nt = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtaTex + normal);
            if (nt != null) { m.SetTexture("_BumpMap", nt); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        }
        if (alphaClip >= 0f)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", alphaClip); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    const string TexDir = "Assets/Textures/Tempulli/";
    // shkëmbinjtë, pemët dhe bimët nga Blender, me materialet e lojës
    static GameObject NatureModel(Material rockM, Material barkM, Material leafM, Material mossM)
    {
        const string path = "Assets/Models/tempulli_natyra.fbx";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return null;
        var leaf2 = MakeMat("Gjethet2", new Color(0.30f, 0.44f, 0.14f), 0.15f);
        foreach (var lm in new[] { leafM, leaf2 }) { lm.SetFloat("_Cull", 0f); if (lm.HasProperty("_CullMode")) lm.SetFloat("_CullMode", 0f); EditorUtility.SetDirty(lm); }
        var map = new Dictionary<string, Material> { { "N_Shkembi", rockM }, { "N_Levorja", barkM }, { "N_Gjethet", leafM }, { "N_Gjethet2", leaf2 }, { "N_Myshk", mossM } };
        bool changed = imp.animationType != ModelImporterAnimationType.None || !imp.isReadable;
        imp.isReadable = true;   // që TrackSpawner t'i bashkojë mesh-et (më pak draw calls)
        imp.animationType = ModelImporterAnimationType.None;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        var existing = imp.GetExternalObjectMap();
        foreach (var kv in map)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            if (existing.TryGetValue(id, out var cur) && cur == kv.Value) continue;
            imp.AddRemap(id, kv.Value); changed = true;
        }
        if (changed) imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
    static Material TexMat(string name, string tex, Color tint, float smooth)
    {
        var m = MakeMat(name, tint, smooth);
        var nti = AssetImporter.GetAtPath(TexDir + tex + "_normal.png") as TextureImporter;
        if (nti != null && nti.textureType != TextureImporterType.NormalMap) { nti.textureType = TextureImporterType.NormalMap; nti.SaveAndReimport(); }
        var a = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + "_albedo.png");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + "_normal.png");
        if (a != null) { m.SetTexture("_BaseMap", a); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", a); }
        if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static ParticleSystem Burst(Transform parent, string name, Vector3 pos, Color col, float life, float speed, float size0, float size1, float gravity)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = life; main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size0, size1); main.startColor = col; main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
        var col2 = ps.colorOverLifetime; col2.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col2.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("Grimca_" + name, col);
        return ps;
    }

    // material për grimca: URP Particles/Unlit, transparent, me teksturën e butë të grimcës
    static Material ParticleMat(string name, Color col)
    {
        string p = "Assets/Materials/" + name + ".mat";
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return MakeMat(name, col);
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WaterMat()
    {
        var m = TexMat("T_Uji", "uji", new Color(0.35f, 0.62f, 0.62f, 0.72f), 0.93f);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetFloat("_BumpScale", 0.6f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GrassMat()
    {
        var ti = AssetImporter.GetAtPath(TexDir + "bari_albedo.png") as TextureImporter;
        if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
        { ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        var m = MakeMat("T_Bari", Color.white, 0.1f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "bari_albedo.png"));
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f); if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CloudMat(string name, string tex)
    {
        string p = "Assets/Materials/" + name + ".mat";
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
        var ti = AssetImporter.GetAtPath(TexDir + tex + "_albedo.png") as TextureImporter;
        if (ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + "_albedo.png"));
        m.SetColor("_BaseColor", new Color(1f, 0.88f, 0.80f, 0.92f));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static ModelImporterClipAnimation Clip(string name, string take, int first, int last, bool loop)
    {
        return new ModelImporterClipAnimation { name = name, takeName = take, firstFrame = first, lastFrame = last, loopTime = loop, loop = loop };
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    static Material MakeMat(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emission = null)
    {
        string p = "Assets/Materials/" + name + ".mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null && GraphicsSettings.currentRenderPipeline != null) sh = GraphicsSettings.currentRenderPipeline.defaultShader;
        if (sh == null) sh = Shader.Find("Standard");
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        else m.shader = sh;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Tempulli i Humbur - lojtari vrapon vetë përpara.
// Mbaj ← → (A/D) ose anoje telefonin: Arta anohet lirisht majtas/djathtas (lëvizje e butë, me përshpejtim).
// Shtyp ← → (ose rrëshqit gishtin) para kthesës: kthesa ruhet dhe bëhet saktë në cep.
// ↑ / W / Space / gishti lart: kërcen. ↓ / S / gishti poshtë: rrëshqet (në ajër: bie shpejt).
// Përplasje anash = pengohet (rojtari afrohet); dy pengime radhazi ose përplasje ballore = fund.
// Vendoset te: GameObject "Lojtari" (me CharacterController). Referencat: animator (Arta), splash, dust, sparkle.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Animator animator;
    public ParticleSystem splash;           // spërkatjet në ujë
    public ParticleSystem dust;             // pluhuri kur ulet pas kërcimit
    public ParticleSystem sparkle;          // shkëndija kur merr monedhë
    public float startSpeed = 10f;
    public float maxSpeed = 18f;
    public float acceleration = 0.06f;      // rritje e ngadaltë: shpejtësia max pas ~2 minutash
    public float laneWidth = 2.4f;          // për monedhat/pengesat dhe autopilotin
    public float tiltSpeed = 7.5f;          // sa shpejt lëviz caku anash kur mban A/D (m/s)
    public float maxLateral = 3.35f;        // sa larg nga mesi i shtegut mund të shkojë
    public float laneChangeSpeed = 14f;     // shpejtësia maksimale anash (m/s)
    public float lateralSmoothTime = 0.08f; // butësia e lëvizjes anash (SmoothDamp)
    public float jumpHeight = 1.6f;
    public float gravity = -30f;
    public float fallGravityMultiplier = 1.55f; // bie më shpejt se ngrihet: kërcim më i "prerë"
    public float coyoteTime = 0.12f;        // mund të kërcejë edhe pak pasi ka dalë nga skaji
    public float jumpBufferTime = 0.15f;    // kërcimi i shtypur pak para uljes nuk humbet
    public float turnBufferDistance = 12f;  // sa metra para kthesës ruhet shtypja
    public float slideTime = 0.9f;
    public float slideClipLength = 0.667f;
    public float jumpClipLength = 0.667f;
    public float leanAngle = 9f;            // anim i trupit kur lëviz anash
    public float hangOffset = 2.35f;        // largësia nga duart (litari) te këmbët

    public float Speed { get; private set; }
    public bool IsDead { get; private set; }
    public float Distance { get; private set; }           // në këtë botë (shtegu dhe rojtari llogariten me këtë)
    public float TotalDistance => Udhetimi.DistancaPara + Distance;   // bashkë me botët e mëparshme (pikët)
    public int Coins { get; private set; }
    public FuqiteLojtarit Fuqite { get; private set; }
    public float SpeedMultiplier => Fuqite != null ? Fuqite.SpeedMul : 1f;
    public Vector3 Forward => lineDir;
    public float GroundY { get; private set; }
    public bool OnZipline => zip != null;
    public bool InWater { get; private set; }
    public bool IsSliding => slideTimer > 0;
    public bool IsGrounded => cc != null && cc.enabled && cc.isGrounded;
    public float LastStumbleTime { get; private set; } = -100f;
    public System.Action<string> OnEvent;
    public string DeathReason { get; private set; } = "";
    public static bool Autopilot;
    public static bool StressTest;          // testi i animacioneve: kërcime/rrëshqitje të shpeshta + shpejtësi të ndryshme
    public static float TestSpeed;          // >0: shpejtësia mbahet kjo (testi)
    float nextStress;

    CharacterController cc;
    float lateralTarget, lateralVel;
    float vy;
    float slideTimer;
    float baseHeight;
    float lastGroundedTime, jumpBufferTimer, airTime;
    bool wasGrounded = true;
    int pendingTurn;                       // -1/1: kthesë e ruajtur
    int waterZones;
    Vector2 touchStart; bool swipeDone;
    Vector3 lineOrigin = Vector3.zero;
    Vector3 lineDir = Vector3.forward;
    TrackSpawner.Zipline zip;
    float zipT;
    Quaternion modelBase = Quaternion.identity;
    float leanNow;

    // vagoni (bota e varrit): Arta s'vrapon, udhëton mbi shina; anohet (A/D ose telefoni) dhe ulet (S); pa kërcime
    [System.NonSerialized] public bool vagon;
    float prirja;                          // -1 anohet majtas ... 1 djathtas
    public float Prirja => prirja;
    bool vagonUlur, vagonFillim, animNgrire;
    // gjendjet e reja të Animator-it (Mixamo): vagoni me anim, ulja në vagon, akulli (snowboard)
    static readonly int H_Vagoni = Animator.StringToHash("Vagoni"), H_VagonUlur = Animator.StringToHash("Vagoni_Ulur"),
        H_Akulli = Animator.StringToHash("Akulli"), H_Vrapi = Animator.StringToHash("Vrapi"), A_Prirja = Animator.StringToHash("Prirja");
    bool gjendjetGati, kaVagon, kaVagonUlur, kaAkull, neAkull;
    // salto: herë pas here kërcimi bëhet salto (Mixamo "Running Forward Flip"), e sinkronizuar me kohën në ajër
    [Range(0f, 1f)] public float saltoMundesia = 0.3f;
    static readonly int H_Salto = Animator.StringToHash("Kercimi_Salto"), A_Salto = Animator.StringToHash("Salto"),
        A_SaltoSpeed = Animator.StringToHash("SaltoSpeed");
    bool kaSalto, neSalto; float saltoPrekja = 0.63f;
    public bool NeSalto => neSalto;
    public bool NeAkull => neAkull;
    float vagonNgri;

    // hash-et e parametrave të Animator-it (llogariten një herë, jo me string në çdo frame)
    static readonly int A_FromSlide = Animator.StringToHash("FromSlide"), A_Sliding = Animator.StringToHash("Sliding"),
        A_Slide = Animator.StringToHash("Slide"), A_Jump = Animator.StringToHash("Jump"), A_Speed = Animator.StringToHash("Speed"),
        A_RunSpeed = Animator.StringToHash("RunSpeed"), A_VelY = Animator.StringToHash("VelY"), A_Grounded = Animator.StringToHash("Grounded"),
        A_Hanging = Animator.StringToHash("Hanging"), A_Stumble = Animator.StringToHash("Stumble"), A_Die = Animator.StringToHash("Die");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        baseHeight = cc.height;
        Speed = startSpeed;
        // erdhi nga një portal: vazhdon me të njëjtën shpejtësi dhe monedha
        if (Udhetimi.NeUdhetim)
        {
            Speed = Mathf.Clamp(Udhetimi.Shpejtesia, startSpeed, maxSpeed);
            Coins = Udhetimi.Monedhat;
        }
        Fuqite = gameObject.AddComponent<FuqiteLojtarit>();
        Fuqite.player = this;
        GroundY = transform.position.y;
        if (animator) modelBase = animator.transform.localRotation;
#if ENABLE_INPUT_SYSTEM
        if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
#endif
    }

    void Update()
    {
        bool playing = GameManager.Instance == null || GameManager.Instance.Playing;
        // pas vdekjes animacioni i rënies vazhdon (shpejtësi 1); në menu/pauzë ngrin
        // shpejtësia e animacioneve është 1 (kërcimi/rrëshqitja kanë kohë të sakta); vetëm ritmi i vrapit ndjek shpejtësinë (RunSpeed)
        if (animator) animator.speed = IsDead ? 1f : (!playing ? 0f : (animNgrire ? 0f : 1f));
        if (IsDead || !playing) { SetSplash(false); return; }

        float dt = Time.deltaTime;
        if (!gjendjetGati && animator != null && animator.runtimeAnimatorController != null)
        {
            gjendjetGati = true;
            kaVagon = animator.HasState(0, H_Vagoni);
            kaVagonUlur = animator.HasState(0, H_VagonUlur);
            kaAkull = animator.HasState(0, H_Akulli);
            kaSalto = animator.HasState(0, H_Salto);
            if (kaSalto)
                foreach (var klip in animator.runtimeAnimatorController.animationClips)
                    if (klip.name == "JumpFlip")
                        foreach (var e in klip.events) if (e.functionName == "Ulja") saltoPrekja = e.time;
        }
        var spawner = TrackSpawner.Instance;
        if (zip != null) { ZipUpdate(dt); return; }

        ReadInput(out bool left, out bool right, out float axis, out bool jump, out bool slide);
        // gjatë vrapit të shpejtë kthesat dhe gropat merren vetë (si autopiloti)
        if (Autopilot || Fuqite.Vrapon) AutoInput(ref left, ref right, ref axis, ref jump, ref slide);
        if (vagon) Vagon(ref left, ref right, ref axis, ref jump, dt);

        // ---- kthesat: shtypja ruhet dhe ekzekutohet në cep ----
        var j = spawner != null ? spawner.NextJunction() : null;
        if (j != null)
        {
            float along = Vector3.Dot(transform.position - j.center, lineDir);
            if ((left || right) && along > -turnBufferDistance && along < 3.3f) pendingTurn = left ? -1 : 1;
            if (pendingTurn != 0 && along >= -1.6f && along < 3.3f)
            {
                if (pendingTurn == j.turn) DoTurn(j, spawner);
                else pendingTurn = 0;
            }
        }
        else pendingTurn = 0;

        // ---- litari: kapet vetë kur arrin te shtylla ----
        var z = spawner != null ? spawner.NextZipline() : null;
        if (z != null)
        {
            Vector3 d = z.a - transform.position; d.y = 0;
            float along = Vector3.Dot(d, lineDir);
            Vector3 zd = z.b - z.a; zd.y = 0;
            bool sameLine = Vector3.Dot(zd.normalized, lineDir) > 0.9f && Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, lineDir))) < 4.5f;
            if (sameLine && along < 1.0f && along > -12f && transform.position.y > z.a.y - hangOffset - 8f) { StartZip(z); return; }
            if (sameLine && along <= -12f) z.used = true;
        }

        // ---- lëvizja anash: caku lëviz me A/D, trupi e ndjek butë ----
        lateralTarget = Mathf.Clamp(lateralTarget + axis * tiltSpeed * dt, -maxLateral, maxLateral);

        // ---- toka, kërcimi (me coyote time dhe buffer), rrëshqitja ----
        bool grounded = cc.isGrounded;
        // teposhtja e akullit (TrackSpawner.SpawnTeposhte): Arta rrëshqet vetë, më shpejt, dhe ngjitet pas rampës
        bool teposhte = false; float tepTan = 0f;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var tepHit, 1.6f, ~0, QueryTriggerInteraction.Ignore)
            && tepHit.collider.name == "Teposhtja")
        {
            teposhte = true;
            float ny = Mathf.Clamp(tepHit.normal.y, 0.5f, 1f);
            tepTan = Mathf.Sqrt(1f - ny * ny) / ny;
        }
        if (grounded)
        {
            if (!wasGrounded && airTime > 0.25f) OnEvent?.Invoke("land");   // pluhuri vjen nga ngjarja "Ulja" e animacionit
            lastGroundedTime = Time.time;
            GroundY = transform.position.y;
            airTime = 0f;
            neSalto = false;
        }
        else airTime += dt;
        wasGrounded = grounded;

        if (jump) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= dt;
        bool canJump = grounded || Time.time - lastGroundedTime < coyoteTime;
        if (jumpBufferTimer > 0f && canJump && vy <= 0.5f)
        {
            jumpBufferTimer = 0f;
            lastGroundedTime = -100f;
            bool fromSlide = slideTimer > 0;
            vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
            slideTimer = 0; SetHeight(baseHeight);
            if (animator)
            {
                // kërcimi në Animator: Ngritje -> Ajër (sa zgjat fizika) -> Rënie (VelY < 0) -> Ulje (Grounded)
                animator.SetBool(A_FromSlide, fromSlide);
                animator.SetBool(A_Sliding, false);
                animator.ResetTrigger(A_Slide);
                // salto herë pas here (jo nga rrëshqitja, jo në vagon/akull): prekja e tokës në klip = rënia e vërtetë
                bool salto = kaSalto && !fromSlide && !vagon && !neAkull && Random.value < saltoMundesia;
                if (salto)
                {
                    float g = -gravity, ngritje = vy / g, renie = Mathf.Sqrt(2f * jumpHeight / (g * fallGravityMultiplier));
                    animator.SetFloat(A_SaltoSpeed, Mathf.Clamp(saltoPrekja / (ngritje + renie), 0.7f, 1.6f));
                    animator.ResetTrigger(A_Jump);
                    animator.SetTrigger(A_Salto);
                }
                else animator.SetTrigger(A_Jump);
                neSalto = salto;
            }
            OnEvent?.Invoke("jump");
        }
        else if (grounded && vy < 0) vy = -2f;

        if (teposhte && grounded && vy <= 0.5f && !vagon && !kaAkull) slide = true;   // rrëshqitje e vazhdueshme mbi akull
        if (slide)
        {
            if (slideTimer <= 0)
            {
                OnEvent?.Invoke("slide");
                if (animator && !vagon) animator.SetTrigger(A_Slide);   // hyrja; cikli zgjat sa Sliding = true (vagoni: VagonAnim)
            }
            slideTimer = slideTime;
            if (!grounded) vy = Mathf.Min(vy, -22f);
            if (animator) animator.ResetTrigger(A_Jump);
        }
        if (slideTimer > 0)
        {
            slideTimer -= dt;
            SetHeight(baseHeight * 0.45f);
            if (slideTimer <= 0)
            {
                if (Physics.SphereCast(transform.position + Vector3.up * 0.4f, cc.radius * 0.9f, Vector3.up, out _, baseHeight - 0.4f, ~0, QueryTriggerInteraction.Ignore))
                    slideTimer = 0.05f;
                else SetHeight(baseHeight);
            }
        }

        vy += gravity * (vy < 0f ? fallGravityMultiplier : 1f) * dt;
        vy = Mathf.Max(vy, -40f);
        Speed = Mathf.Min(maxSpeed, Speed + acceleration * dt);
        if (TestSpeed > 0f) Speed = TestSpeed;

        // ---- lëvizja ----
        Vector3 side = Vector3.Cross(Vector3.up, lineDir);
        float lateral = Vector3.Dot(transform.position - lineOrigin, side);
        float newLat = Mathf.SmoothDamp(lateral, lateralTarget, ref lateralVel, lateralSmoothTime, laneChangeSpeed, dt);
        float speedHere = (InWater ? Speed * 0.9f : Speed) * Fuqite.SpeedMul;
        if (teposhte)
        {
            speedHere *= 1.25f;
            if (vy <= 0f) vy = -2f - speedHere * tepTan;   // mbetet ngjitur pas akullit (pa "fluturime" të vogla)
        }
        Vector3 before = transform.position;
        cc.Move(lineDir * speedHere * dt + side * (newLat - lateral) + Vector3.up * vy * dt);
        // distanca nga lëvizja e vërtetë (rojtari dhe shtegu mbeten të sinkronizuar)
        Distance += Mathf.Max(0f, Vector3.Dot(transform.position - before, lineDir));

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lineDir), 1f - Mathf.Exp(-12f * dt));

        if (transform.position.y < GroundY - 5f)
        {
            DeathReason = "ra nga shtegu";
            Debug.Log("Tempulli: ra nga shtegu te " + transform.position + " drejtimi " + lineDir + " origjina " + lineOrigin + " GroundY " + GroundY + " kthesa e ruajtur " + pendingTurn +
                      " | rrëshqitje " + (slideTimer > 0) + " vy " + vy.ToString("0.0") + " koha në ajër " + airTime.ToString("0.00") + " test " + StressTest);
            Die();
        }
        SetSplash(InWater && grounded);

        if (animator)
        {
            float s01 = Mathf.Clamp01((Speed - startSpeed) / Mathf.Max(0.01f, maxSpeed - startSpeed));
            animator.SetFloat(A_Speed, Speed);
            animator.SetFloat(A_RunSpeed, Mathf.Lerp(1f, 1.15f, s01));
            animator.SetFloat(A_VelY, grounded && vy < 0f ? 0f : vy);
            animator.SetBool(A_Grounded, grounded);
            animator.SetBool(A_Sliding, slideTimer > 0);
            animator.SetBool(A_Hanging, false);
        }
        // teposhtja: Arta zbret më këmbë si me snowboard (gjendja "Akulli"); kur del, kthehet te vrapi
        if (kaAkull && animator && !vagon)
        {
            if (teposhte && grounded && vy <= 0.5f && slideTimer <= 0f)
            {
                neAkull = true;
                if (!NeGjendje(H_Akulli)) animator.CrossFadeInFixedTime(H_Akulli, 0.2f);
            }
            else if (!teposhte && neAkull && grounded)
            {
                neAkull = false;
                if (NeGjendje(H_Akulli)) animator.CrossFadeInFixedTime(H_Vrapi, 0.25f);
            }
        }
        if (vagon) VagonAnim(dt);
    }

    // a është Animator-i në këtë gjendje (ose duke kaluar drejt saj)?
    bool NeGjendje(int h)
    {
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == h) return !animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).shortNameHash == h;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == h;
    }

    void LateUpdate()
    {
        // anim i lehtë i trupit në drejtim të lëvizjes anash (pas Animator-it)
        if (!animator) return;
        float target = (IsDead || zip != null) ? 0f : Mathf.Clamp(-lateralVel / laneChangeSpeed, -1f, 1f) * leanAngle;
        if (vagon && !IsDead) target = -prirja * (kaVagon ? 8f : 24f);   // vagoni: Arta anohet fort jashtë vagonit
        leanNow = Mathf.Lerp(leanNow, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        animator.transform.localRotation = Quaternion.Euler(0, vagon ? 0f : -leanNow * 0.6f, leanNow) * modelBase;
    }

    // ---------------- vagoni ----------------
    // prirja ndjek A/D (ose animin e telefonit); kthesat i merr vetë vagoni; kërcimi s'ka kuptim në vagon
    void Vagon(ref bool left, ref bool right, ref float axis, ref bool jump, float dt)
    {
        float deshira = Mathf.Clamp(axis, -1f, 1f);
        if (Autopilot || Fuqite.Vrapon) deshira = AutoPrirja();
        prirja = Mathf.MoveTowards(prirja, deshira, 6f * dt);
        var sp = TrackSpawner.Instance;
        var jj = sp != null ? sp.NextJunction() : null;
        if (jj != null) pendingTurn = jj.turn;
        left = right = false; axis = 0f; jump = false;
    }

    // autopiloti: anohu larg gurit më të afërt që del nga muri
    float AutoPrirja()
    {
        float best = float.MaxValue, d = 0f;
        foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 1.2f, lineDir, Speed * 0.6f + 5f, ~0, QueryTriggerInteraction.Collide))
        {
            var pa = h.collider.GetComponent<PengeseAnash>();
            if (pa != null && h.distance < best) { best = h.distance; d = -pa.ana; }
        }
        return d;
    }

    // guri që del nga muri: Arta shpëton vetëm nëse anohet në anën tjetër (ose ka fuqi)
    void GuriAnash(Collider c, PengeseAnash pa)
    {
        if (prirja * pa.ana <= -0.5f) return;
        Vector3 pika = transform.position + Vector3.up * 1.5f;
        if (Fuqite.Pathyeshem) { Fuqite.Thyej(c, pika); return; }
        if (Fuqite.PerdorMburojen(c, pika)) return;
        DeathReason = "u përplas me gurin e murit (anohu në anën tjetër)";
        Die();
    }

    // poza në vagon: ulja (gjunjët e përkulur) e ngrirë; kur ulet (S) luhet rrëshqitja, që e fsheh brenda vagonit
    void VagonAnim(float dt)
    {
        if (!animator || IsDead) return;
        if (kaVagon)
        {
            // animacionet Mixamo: qëndrimi i përkulur që anohet sipas Prirja; ulja me duar mbi kokë
            animNgrire = false;
            animator.SetFloat(A_Prirja, prirja);
            bool ulet = slideTimer > 0f && kaVagonUlur;
            int deshira = ulet ? H_VagonUlur : H_Vagoni;
            if (!NeGjendje(deshira)) animator.CrossFadeInFixedTime(deshira, ulet ? 0.08f : 0.18f);
            return;
        }
        if (slideTimer > 0f)
        {
            if (!vagonUlur)
            {
                vagonUlur = true; animNgrire = false;
                animator.CrossFadeInFixedTime("Rreshqitja", 0.12f);
                animator.ResetTrigger(A_Slide);
            }
            return;
        }
        if (vagonUlur || !vagonFillim)
        {
            vagonUlur = false; vagonFillim = true; animNgrire = false;
            animator.CrossFadeInFixedTime("Ulja", 0.1f, 0, 0.02f);
            vagonNgri = 0.1f;
        }
        else if (!animNgrire)
        {
            vagonNgri -= dt;
            if (vagonNgri <= 0f) animNgrire = true;
        }
    }

    void DoTurn(TrackSpawner.Junction j, TrackSpawner spawner)
    {
        lineOrigin = j.center;
        lineDir = j.newDir;
        Vector3 side = Vector3.Cross(Vector3.up, lineDir);
        // mbaj pozicionin anash ku është, që të mos ketë kërcim të papritur
        lateralTarget = Mathf.Clamp(Vector3.Dot(transform.position - lineOrigin, side), -maxLateral * 0.6f, maxLateral * 0.6f);
        lateralVel = 0f;
        pendingTurn = 0;
        spawner.Passed(j);
        if (animator && cc.isGrounded && slideTimer <= 0 && !vagon) animator.SetTrigger(j.turn < 0 ? "TurnL" : "TurnR");
        OnEvent?.Invoke("turn");
    }

    // ---------------- litari ----------------
    void StartZip(TrackSpawner.Zipline z)
    {
        zip = z; zip.used = true;
        Vector3 ab0 = z.b - z.a; ab0.y = 0;
        Vector3 ap = transform.position - z.a; ap.y = 0;
        zipT = Mathf.Clamp01(Vector3.Dot(ap, ab0) / Mathf.Max(0.01f, ab0.sqrMagnitude));
        slideTimer = 0; SetHeight(baseHeight);
        cc.enabled = false;
        lateralTarget = 0; lateralVel = 0;
        OnEvent?.Invoke("jump");
        if (animator) { animator.SetBool(A_Sliding, false); animator.SetBool(A_Hanging, true); }
    }

    void ZipUpdate(float dt)
    {
        Vector3 ab = zip.b - zip.a;
        float horiz = new Vector3(ab.x, 0, ab.z).magnitude;
        float v = Mathf.Max(Speed * 1.3f, 14f);
        float prevT = zipT;
        zipT = Mathf.Min(1f, zipT + v * dt / ab.magnitude);
        Distance += (zipT - prevT) * horiz;
        Vector3 rope = Vector3.Lerp(zip.a, zip.b, zipT) - Vector3.up * (Mathf.Sin(zipT * Mathf.PI) * zip.sag);
        transform.position = Vector3.Lerp(transform.position, rope - Vector3.up * hangOffset, 1f - Mathf.Exp(-18f * dt));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lineDir), 1f - Mathf.Exp(-10f * dt));
        SetSplash(false);
        if (animator) { animator.SetBool(A_Hanging, true); animator.SetBool(A_Grounded, false); animator.SetFloat(A_Speed, Speed); }
        if (zipT >= 1f)
        {
            zip = null;
            cc.enabled = true;
            vy = 0f; airTime = 0.5f; wasGrounded = false;
            GroundY = transform.position.y - 3f;
            if (animator) { animator.SetBool(A_Hanging, false); animator.SetFloat(A_VelY, -6f); }
            OnEvent?.Invoke("turn");
        }
    }

    // ---------------- ngjarjet e animacionit (ArtaAnimEvents) ----------------
    public void AnimEvent(string e)
    {
        if (IsDead || (GameManager.Instance != null && !GameManager.Instance.Playing)) return;
        switch (e)
        {
            case "step":
                if (!cc.isGrounded || slideTimer > 0) return;
                if (InWater) { if (splash) splash.Emit(6); }
                else if (dust) dust.Emit(1);
                OnEvent?.Invoke("step");
                break;
            case "takeoff": if (dust && !InWater) dust.Emit(6); break;
            case "touchdown": if (InWater) { if (splash) splash.Emit(20); } else if (dust) dust.Emit(14); break;
            case "slidedust": if (dust && !InWater) dust.Emit(8); break;
        }
    }

    // ---------------- uji ----------------
    void SetSplash(bool on)
    {
        if (splash == null) return;
        var em = splash.emission;
        if (em.enabled != on) em.enabled = on;
    }

    // ---------------- autopiloti (testim) ----------------
    static readonly Unity.Profiling.ProfilerMarker mAuto = new Unity.Profiling.ProfilerMarker("Tempulli.AutoInput");

    void AutoInput(ref bool left, ref bool right, ref float axis, ref bool jump, ref bool slide)
    {
        using var _prof = mAuto.Auto();
        var sp = TrackSpawner.Instance;
        var j = sp != null ? sp.NextJunction() : null;
        if (j != null)
        {
            float along = Vector3.Dot(transform.position - j.center, lineDir);
            if (along > -2.5f && along < 2f) { if (j.turn < 0) left = true; else right = true; return; }
        }
        bool nearTurn = j != null && Vector3.Dot(transform.position - j.center, lineDir) > -14f;
        float look = Speed * 0.5f + 2f;
        Vector3 o = transform.position;
        Vector3 side = Vector3.Cross(Vector3.up, lineDir);
        float lateral = Vector3.Dot(o - lineOrigin, side);

        // larg: a ka dysheme? nëse jo, kërko anën që ka (gjysmë-shtegu). Afër: nëse s'ka askund, kërce (gropë).
        float far = Speed * 0.45f + 2f, near = Speed * 0.13f + 0.9f;
        bool Floor(float lat, float dist) => Physics.Raycast(o + side * (lat - lateral) + lineDir * dist + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        if (cc.isGrounded && !nearTurn)
        {
            if (!Floor(lateral, far))
            {
                float best = float.NaN;
                for (float l = -3f; l <= 3.01f; l += 0.75f)
                    if (Floor(l, far) && (float.IsNaN(best) || Mathf.Abs(l - lateral) < Mathf.Abs(best - lateral))) best = l;
                if (!float.IsNaN(best))
                {
                    float dir = Mathf.Sign(best - lateral);   // pak më brenda, larg skajit
                    if (Floor(best + dir * 0.75f, far)) best += dir * 0.75f;
                    lateralTarget = best;
                }
            }
            if (!Floor(lateral, near))
            {
                bool any = false;
                for (float l = -3f; l <= 3.01f && !any; l += 0.75f) any = Floor(l, near);
                if (!any) { jump = true; return; }
            }
        }
        // me gjerësinë e trupit (sferë), jo vetëm një rreze në mes
        var lowHits = Physics.SphereCastAll(o + Vector3.up * 0.6f, 0.5f, lineDir, look, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(lowHits, (x, y) => x.distance.CompareTo(y.distance));   // pengesa më e afërt e para
        foreach (var h in lowHits)
            if (h.collider.GetComponentInParent<Obstacle>() != null)
            {
                string n = h.collider.name;
                if (n.StartsWith("Rrenja")) { if (h.distance < Speed * 0.3f + 1f) jump = true; return; }
                if (n.StartsWith("Blloku"))
                {
                    for (int l = -1; l <= 1; l++)
                    {
                        Vector3 p = o + side * (l * laneWidth - lateral);
                        // vetëm rreshti i njëjtë i pengesave (jo rreshti tjetër më larg)
                        if (!Physics.SphereCast(new Vector3(p.x, o.y + 0.6f, p.z), 0.5f, lineDir, out _, h.distance + 1.5f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            lateralTarget = l * laneWidth; return;
                        }
                    }
                    return;
                }
            }
        foreach (var h in Physics.RaycastAll(o + Vector3.up * 1.5f, lineDir, look, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name.StartsWith("Porta") && slideTimer <= 0 && h.distance < Speed * 0.3f + 1.5f) { slide = true; return; }
        // testi: veprime të shpeshta e të shpejta (kërcim pas rrëshqitjeje, rrëshqitje në ajër...) kur rruga para është e lirë
        if (StressTest && Time.time > nextStress && !nearTurn && !InWater)
        {
            bool clear = !Physics.SphereCast(o + Vector3.up * 0.6f, 0.5f, lineDir, out _, Speed * 1.3f + 2f, ~0, QueryTriggerInteraction.Ignore)
                         && !Physics.Raycast(o + Vector3.up * 1.5f, lineDir, Speed * 1.3f + 2f, ~0, QueryTriggerInteraction.Ignore);
            bool floor = Floor(lateral, Speed * 0.9f) && Floor(lateral, Speed * 0.5f);
            if (clear && floor)
            {
                nextStress = Time.time + Random.Range(0.25f, 0.9f);
                if (Random.value < 0.55f) jump = true; else slide = true;
            }
        }
    }

    void SetHeight(float h)
    {
        if (Mathf.Approximately(cc.height, h)) return;
        cc.height = h;
        cc.center = new Vector3(0, h / 2f, 0);
        cc.stepOffset = h < baseHeight * 0.9f ? 0f : 0.3f;
    }

    // ---------------- hyrja: tastierë, gisht (swipe gjatë lëvizjes), anim i telefonit ----------------
    void ReadInput(out bool left, out bool right, out float axis, out bool jump, out bool slide)
    {
        left = right = jump = slide = false;
        axis = 0f;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            left = k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame;
            right = k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame;
            if (k.leftArrowKey.isPressed || k.aKey.isPressed) axis -= 1f;
            if (k.rightArrowKey.isPressed || k.dKey.isPressed) axis += 1f;
            jump = k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            slide = k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame;
        }
        var acc = Accelerometer.current;
        if (acc != null && acc.enabled) axis += Mathf.Clamp(acc.acceleration.ReadValue().x * 2.5f, -1f, 1f);
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var t = ts.primaryTouch;
            if (t.press.wasPressedThisFrame) { touchStart = t.position.ReadValue(); swipeDone = false; }
            // swipe-i njihet sapo gishti lëviz mjaftueshëm (jo vetëm kur ngrihet) - një herë për prekje
            if (t.press.isPressed && !swipeDone)
            {
                Vector2 d = t.position.ReadValue() - touchStart;
                float min = Mathf.Max(40f, Screen.dpi > 0 ? Screen.dpi * 0.25f : 60f);
                if (d.magnitude > min) { Swipe(d, ref left, ref right, ref jump, ref slide); swipeDone = true; }
            }
        }
#else
        left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) axis -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) axis += 1f;
        axis += Mathf.Clamp(Input.acceleration.x * 2.5f, -1f, 1f);
        jump = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space);
        slide = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) { touchStart = t.position; swipeDone = false; }
            if (!swipeDone && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Ended))
            {
                Vector2 d = t.position - touchStart;
                if (d.magnitude > 60f) { Swipe(d, ref left, ref right, ref jump, ref slide); swipeDone = true; }
            }
        }
#endif
        // rruga rezervë (IMGUI): kur Input System s'merr tastet në këtë kompjuter (shih Tastiera.cs)
        if (Tastiera.Rezerve)
        {
            left |= Tastiera.Shtypur(KeyCode.LeftArrow) || Tastiera.Shtypur(KeyCode.A);
            right |= Tastiera.Shtypur(KeyCode.RightArrow) || Tastiera.Shtypur(KeyCode.D);
            if (Tastiera.Mbajtur(KeyCode.LeftArrow) || Tastiera.Mbajtur(KeyCode.A)) axis -= 1f;
            if (Tastiera.Mbajtur(KeyCode.RightArrow) || Tastiera.Mbajtur(KeyCode.D)) axis += 1f;
            jump |= Tastiera.Shtypur(KeyCode.UpArrow) || Tastiera.Shtypur(KeyCode.W) || Tastiera.Shtypur(KeyCode.Space);
            slide |= Tastiera.Shtypur(KeyCode.DownArrow) || Tastiera.Shtypur(KeyCode.S);
        }
        axis = Mathf.Clamp(axis, -1f, 1f);
        if (jump && slide) slide = false;   // mos i bëj të dyja në të njëjtin frame
    }

    static void Swipe(Vector2 d, ref bool l, ref bool r, ref bool j, ref bool s)
    {
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) { if (d.x < 0) l = true; else r = true; }
        else { if (d.y > 0) j = true; else s = true; }
    }

    // ---------------- përplasjet ----------------
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsDead) return;
        if (hit.collider.GetComponentInParent<Obstacle>() == null) return;
        if (hit.point.y > transform.position.y + cc.height + 0.02f) return;   // mbi kokë (step offset) - injoroje
        // këmbët prekin vetëm majën e pengesës (p.sh. ulet mbi rrënjë pas kërcimit): fal, mos e vrit
        if (hit.collider.bounds.max.y <= transform.position.y + 0.3f) return;
        // vrap i shpejtë ose sapo shpëtoi nga mburoja: pengesa thyhet, Arta vazhdon
        if (Fuqite.Pathyeshem) { Fuqite.Thyej(hit.collider, hit.point); return; }
        Vector3 n = hit.normal; n.y = 0;
        bool sideHit = n.sqrMagnitude > 0.01f && Mathf.Abs(Vector3.Dot(n.normalized, lineDir)) < 0.45f;
        if (sideHit)
        {
            if (Time.time - LastStumbleTime < 0.6f) return;               // e njëjta përplasje
            if (Time.time - LastStumbleTime < 6f)
            {
                if (Fuqite.PerdorMburojen(hit.collider, hit.point)) return;
                DeathReason = "u pengua dy herë radhazi"; Die(); return;
            }
            // pengim: shtyhet pak anash, ngadalësohet, rojtari afrohet
            LastStumbleTime = Time.time;
            Vector3 side = Vector3.Cross(Vector3.up, lineDir);
            lateralTarget = Mathf.Clamp(Vector3.Dot(transform.position - lineOrigin, side) + Vector3.Dot(n.normalized, side) * 0.7f, -maxLateral, maxLateral);
            lateralVel = 0f;
            Speed = Mathf.Max(startSpeed, Speed * 0.9f);
            if (animator) animator.SetTrigger(A_Stumble);
            OnEvent?.Invoke("stumble");
            return;
        }
        if (Fuqite.PerdorMburojen(hit.collider, hit.point)) return;   // mburoja e shpëton njëherë
        var b = hit.collider.bounds;
        DeathReason = hit.collider.name + "  (pengesa " + b.min.y.ToString("0.00") + "–" + b.max.y.ToString("0.00") + " m, Arta " +
                      (transform.position.y).ToString("0.00") + "–" + (transform.position.y + cc.height).ToString("0.00") + " m, rrëshqitje: " + (slideTimer > 0 ? "PO" : "JO") + ")";
        Debug.Log("Tempulli: u përplas me " + DeathReason);
        Die();
    }

    void OnTriggerEnter(Collider other)
    {
        var coin = other.GetComponent<Coin>();
        if (coin != null) CollectCoin(coin);
        if (other.GetComponent<WaterZone>() != null) { waterZones++; InWater = true; }
        if (IsDead) return;
        var fuqia = other.GetComponentInParent<Fuqia>();
        if (fuqia != null) fuqia.Merre(this);
        var portali = other.GetComponent<Portali>();
        if (portali != null) portali.Hyr(this);
        var guri = other.GetComponent<PengeseAnash>();
        if (guri != null && vagon) GuriAnash(other, guri);
    }

    // marrja e monedhës (nga prekja ose nga magneti)
    public void CollectCoin(Coin coin)
    {
        if (coin == null || IsDead || !coin.Collect(transform)) return;
        Coins++;
        OnEvent?.Invoke("coin");
        if (sparkle) { sparkle.transform.position = coin.transform.position; sparkle.Emit(8); }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<WaterZone>() != null) { waterZones = Mathf.Max(0, waterZones - 1); InWater = waterZones > 0; }
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        if (zip != null) { zip = null; cc.enabled = true; }
        SetSplash(false);
        if (animator)
        {
            animator.SetFloat(A_Speed, 0); animator.SetBool(A_Hanging, false); animator.SetBool(A_Sliding, false);
            animator.ResetTrigger(A_Jump); animator.SetTrigger(A_Die);
        }
        OnEvent?.Invoke("die");
        if (GameManager.Instance != null) GameManager.Instance.GameOver();
    }
}

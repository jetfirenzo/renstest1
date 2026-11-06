using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Dragon Cave v2: echte assets.
// Menu: Tools > Dragon Cave > Bouw Grot v2
// Scene: Assets/DragonCave/DragonCave_v2.unity (v1 blijft onaangeroerd)
// Assets: Mines and Cave Set (Loafbrr, CC0) + Dragon for Boss Monster HP (TerrorBringer, Dungeon Mason)
// Layout: mijngang met houten stutten > koepelzaal > draak slaapt in het midden onder een spotlicht, ei tussen de poten.
// Opnieuw draaien = alles opnieuw genereren (Assets/DragonCave/GeneratedV2 wordt vervangen).
public static class DragonCaveV2Builder
{
    const string Root = "Assets/DragonCave";
    const string GenFolder = "Assets/DragonCave/GeneratedV2";
    const string ScenePath = "Assets/DragonCave/DragonCave_v2.unity";
    const string AudioFolder = "Assets/DragonCave/Audio";

    const string Kit = "Assets/LoafbrrAssets/MInesAndCaveSet/";
    const string DragonPrefab = "Assets/FourEvilDragonsHP/Prefab/DragonTerrorBringer/Red.prefab";
    const string DragonAnims = "Assets/FourEvilDragonsHP/Animations/DragonTerrorBringer/";

    const float CaveRadius = 15f;
    const float CaveHeight = 11f;
    const float EntranceAngle = -90f;          // ingang aan de -Z kant
    const float DragonLength = 6f;             // grootste horizontale maat (kop tot staart) in meters. Speler = 1,75 m
    static readonly Vector3 DragonCenter = Vector3.zero;   // midden van de grot

    static Material wallMat, groundMat, flameMat, woodMat, goldMat, dayMat, dustMat, eggMat;
    static AudioClip torchClip, snoreClip, waterClip, screamClip, crackClip;
    static readonly List<string> missing = new List<string>();
    static GameObject xrRigInstance;   // gezet door TrySpawnXRRig, gebruikt voor SenseGlove-koppeling

    static bool VrMode => EditorPrefs.GetBool("DragonCave_VRMode", false);   // zelfde schakelaar als v1

    [MenuItem("Tools/Dragon Cave/Bouw Grot v2", false, 1)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Dragon Cave v2", "Stop eerst Play-modus en probeer het dan opnieuw.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DragonPrefab) == null || !AssetDatabase.IsValidFolder(Kit.TrimEnd('/')))
        {
            EditorUtility.DisplayDialog("Dragon Cave v2", "Assets ontbreken. Nodig: 'Dragon for Boss Monster HP' en 'Mines and Cave Set' (Package Manager > My Assets > Import).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        missing.Clear();
        PrepareFolders();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Random.InitState(7);
        MakeMaterials();
        LoadAudio();
        SetupAtmosphere();

        var cave = new GameObject("Grot").transform;
        BuildShell(cave);
        List<Vector3> path = BuildTunnel(cave);
        AddTunnelAmbience(cave, path);
        DressHall(cave);

        GameObject dragon = BuildDragon(out Transform head, out Vector3 eggPos);
        GameObject egg = BuildEgg(eggPos);
        WireDragon(dragon, head, egg);
        BuildHoard(cave);
        BuildHallTorches();
        BuildSpotlight(dragon);
        AddDust(cave);
        BuildPlayerAndPost(path);
        BuildSenseGloveTorch(path);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = dragon;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

        if (missing.Count > 0) Debug.LogWarning("[Dragon Cave v2] Niet gevonden (overgeslagen): " + string.Join(", ", missing));
        Debug.Log("[Dragon Cave v2] Klaar (" + (VrMode ? "VR" : "Desktop") + "): " + ScenePath + ". Pak het ei en de draak wordt wakker. Fakkel staat naast het spawnpunt.");
    }

    // ---------------------------------------------------------------- materialen + sfeer

    static void MakeMaterials()
    {
        wallMat = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/A_MineWalls.mat");
        groundMat = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/A_MinesGround.mat");
        if (wallMat == null) wallMat = MakeMat("Rots", new Color(0.30f, 0.26f, 0.23f), 0.15f, 0f, Color.black);
        if (groundMat == null) groundMat = MakeMat("Vloer", new Color(0.24f, 0.20f, 0.17f), 0.1f, 0f, Color.black);
        flameMat = MakeMat("Vlam", new Color(1f, 0.6f, 0.2f), 0f, 0f, new Color(4f, 1.6f, 0.3f));
        woodMat = MakeMat("Hout", new Color(0.30f, 0.18f, 0.10f), 0.1f, 0f, Color.black);
        goldMat = MakeMat("Goud", new Color(1f, 0.76f, 0.3f), 0.85f, 1f, new Color(0.15f, 0.08f, 0f));
        dayMat = MakeMat("Daglicht", new Color(0.5f, 0.65f, 1f), 0f, 0f, new Color(0.9f, 1.2f, 1.8f));
        eggMat = MakeMat("Ei", new Color(0.18f, 0.45f, 0.42f), 0.75f, 0.2f, new Color(0.05f, 0.35f, 0.3f));
        dustMat = MakeParticleMat("Stof", new Color(1f, 0.82f, 0.6f, 0.45f));
    }

    static void SetupAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.075f, 0.075f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.045f, 0.03f, 0.025f);
        RenderSettings.fogDensity = 0.028f;
    }

    // ---------------------------------------------------------------- schil (koepel + vloer)

    static void BuildShell(Transform cave)
    {
        Mesh dome = BuildDome(CaveRadius, CaveHeight, 96, 32, 0.22f);
        AssetDatabase.CreateAsset(dome, GenFolder + "/Koepel.asset");
        MeshObject("Koepel", cave, dome, wallMat);

        Mesh floor = BuildCaveFloor(CaveRadius * 1.05f, 36, 96);
        AssetDatabase.CreateAsset(floor, GenFolder + "/Grotvloer.asset");
        MeshObject("Vloer", cave, floor, groundMat);
    }

    static float CeilingAt(float r) => CaveHeight * Mathf.Sqrt(Mathf.Max(0f, 1f - (r * r) / (CaveRadius * CaveRadius)));

    static float FloorHeight(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float bumps = (Mathf.PerlinNoise(x * 0.18f + 200f, z * 0.18f + 200f) - 0.5f) * 0.6f;
        float dn = Vector2.Distance(new Vector2(x, z), new Vector2(DragonCenter.x, DragonCenter.z));
        float flat = Mathf.Clamp01((dn - 7f) / 3f);                                    // vlak rond de draak
        float walk = z < DragonCenter.z ? Mathf.Clamp01((Mathf.Abs(x) - 2f) / 2.5f) : 1f; // vlak looppad vanaf de ingang
        float edge = Mathf.Clamp01((r - CaveRadius * 0.78f) / (CaveRadius * 0.22f));
        float ang = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        float entrance = Mathf.Clamp01((Mathf.Abs(Mathf.DeltaAngle(ang, EntranceAngle)) - 10f) / 8f);
        return bumps * flat * walk + edge * edge * 1.4f * entrance;
    }

    // ---------------------------------------------------------------- gang met mijnstutten

    static List<Vector3> BuildTunnel(Transform cave)
    {
        var tunnel = new GameObject("Gang").transform;
        tunnel.SetParent(cave, false);

        const int steps = 120;
        const float length = 45f;               // lange gang: het duurt even voor je bij de draak bent
        float z0 = -CaveRadius * 0.88f;
        var path = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            // S-bocht: je ziet de zaal pas vlak voor het einde
            float x = 4.5f * Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Clamp01(t * 6f);
            path.Add(new Vector3(x, 0f, z0 - t * length));
        }

        Mesh mesh = BuildTunnelMesh(path, 2.7f, 3.4f, 0.4f, 28);
        AssetDatabase.CreateAsset(mesh, GenFolder + "/Gang.asset");
        MeshObject("Tunnelwand", tunnel, mesh, wallMat);

        // Houten mijnstutten (de gevangenen hebben deze gang gegraven)
        int n = 0;
        for (int i = 10; i < steps - 3; i += 11)
        {
            Vector3 tangent = (path[i + 1] - path[i - 1]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Quaternion rot = Quaternion.LookRotation(tangent);
            var frame = new GameObject("Stut_" + (++n)).transform;
            frame.SetParent(tunnel, false);
            PlaceKit("Posts/Post_Reinforced_A", frame, path[i] + right * 1.9f, rot, 3.1f, false);
            PlaceKit("Posts/Post_Reinforced_A", frame, path[i] - right * 1.9f, rot, 3.1f, false);
            GameObject beam = PlaceKit("Posts/Beam_A", frame, path[i] + Vector3.up * 3.05f, Quaternion.identity, 0f, false);
            if (beam != null)
            {
                bool longX = FitLength(beam, 4.3f);
                beam.transform.rotation = rot * (longX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));   // dwars over de gang
                CenterXZ(beam, path[i]);
            }
            if (n % 2 == 1) MakeTorch(frame, "Gangfakkel_" + n, path[i] + right * 1.7f + Vector3.up * 2.1f, -right, 6f, 9f);
        }

        // Daglicht aan het eind
        Vector3 end = path[steps];
        Vector3 outDir = (path[steps] - path[steps - 1]).normalized;
        Prim(PrimitiveType.Quad, "Daglicht", tunnel, end + outDir * 0.5f + Vector3.up * 1.8f, new Vector3(6f, 4.5f, 1f), dayMat, Quaternion.LookRotation(outDir));
        PointLight("Daglicht_licht", tunnel, end - outDir * 1.5f + Vector3.up * 2.5f, new Color(0.55f, 0.7f, 1f), 5f, 12f, LightShadows.None);
        return path;
    }

    // ---------------------------------------------------------------- aankleding zaal

    static void DressHall(Transform cave)
    {
        // Ring van rotswanden voor de koepel, ingang vrij
        var walls = new GameObject("Rotswanden").transform;
        walls.SetParent(cave, false);
        string[] wallKit = { "Wall/Cave_Wall_Rocks_A", "Wall/Cave_Wall_Rocks_B", "Wall/Cave_Wall_Rocks_C", "Wall/Cave_Wall_Rocks_D", "Wall/Cave_Wall_Rocks_E", "Wall/Cave_Wall_Rocks_F", "Wall/Cave_Wall_Rocks_G" };
        float ringR = CaveRadius * 0.9f;
        float ang = EntranceAngle + 20f;
        int w = 0;
        while (ang < EntranceAngle + 340f && w < 60)
        {
            float a = ang * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(a) * ringR, 0f, Mathf.Sin(a) * ringR);
            pos.y = FloorHeight(pos.x, pos.z) - 0.3f;
            Quaternion rot = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z)) * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
            GameObject g = PlaceKit(wallKit[w % wallKit.Length], walls, pos, rot, Random.Range(5.5f, 7.5f), true);
            float width = g != null ? Mathf.Max(1.5f, HorizontalSize(g) * 0.75f) : 3f;
            ang += width / ringR * Mathf.Rad2Deg;
            w++;
        }

        // Pilaren van vloer tot plafond
        var pillars = new GameObject("Pilaren").transform;
        pillars.SetParent(cave, false);
        string[] pillarKit = { "Wall/Cave_Pillar_A", "Wall/Cave_Pillar_B", "Wall/Cave_Pillar_C" };
        float[] pillarAngles = { -40f, 15f, 60f, 120f, 165f, 215f };
        for (int i = 0; i < pillarAngles.Length; i++)
        {
            float a = pillarAngles[i] * Mathf.Deg2Rad;
            float r = CaveRadius * Random.Range(0.6f, 0.7f);
            var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            pos.y = FloorHeight(pos.x, pos.z) - 0.2f;
            PlaceKit(pillarKit[i % 3], pillars, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), CeilingAt(r) + 0.6f, true);
        }

        // Losse rotsen en hopen langs de randen
        var rocks = new GameObject("Rotsen").transform;
        rocks.SetParent(cave, false);
        string[] rockKit = { "Wall/Cave_Rocks_A", "Wall/Cave_Rocks_B", "Wall/Cave_Rocks_C", "Wall/Cave_Rocks_D", "Ground/Ground_Mound_A", "Ground/Ground_Mound_C", "Ground/Ground_Mound_E" };
        int placed = 0;
        for (int i = 0; i < 300 && placed < 18; i++)
        {
            float a = Random.Range(0f, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(a, EntranceAngle)) < 24f) continue;
            float r = CaveRadius * Random.Range(0.72f, 0.84f);
            var pos = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * r);
            pos.y = FloorHeight(pos.x, pos.z) - 0.1f;
            PlaceKit(rockKit[placed % rockKit.Length], rocks, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Random.Range(1f, 2.6f), true);
            placed++;
        }
    }

    // ---------------------------------------------------------------- draak

    static GameObject BuildDragon(out Transform head, out Vector3 eggPos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DragonPrefab);
        var dragon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        dragon.name = "Draak (TerrorBringer)";

        AnimatorController ctrl = MakeDragonController();
        var anim = dragon.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Slaappose alvast in de editor zetten, zodat we bounds en poten kunnen meten
        AnimationClip sleep = LoadClip(DragonAnims + "sleep.fbx");
        if (sleep != null && anim != null) sleep.SampleAnimation(anim.gameObject, 0f);

        // Schalen naar DragonLength, kop richting de ingang (-Z)
        dragon.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        // Voorlopige maat in de editor (op de botten). In Play zet DragonFit hem definitief goed.
        Bounds b = BoneBounds(dragon);
        float size = Mathf.Max(b.size.x, b.size.z);
        float k = size > 0.0001f ? DragonLength / size : 1f;
        dragon.transform.localScale = Vector3.one * k;
        b = BoneBounds(dragon);
        dragon.transform.position += new Vector3(DragonCenter.x - b.center.x, DragonLength * 0.02f - b.min.y, DragonCenter.z - b.center.z);

        head = FindBone(dragon.transform, "Head");
        var hands = dragon.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Hand")).ToList();
        if (hands.Count >= 2)
        {
            Vector3 mid = (hands[0].position + hands[1].position) * 0.5f;
            Vector3 fwd = dragon.transform.forward;
            eggPos = new Vector3(mid.x, 0f, mid.z) + fwd * 0.8f;
        }
        else if (head != null)
        {
            eggPos = new Vector3(head.position.x, 0f, head.position.z) + dragon.transform.forward * 1.5f;
        }
        else eggPos = DragonCenter + new Vector3(0f, 0f, -4f);

        Debug.Log("[Dragon Cave v2] Draak in editor geschaald x" + k.ToString("0.0000") + ", " + hands.Count + " handen gevonden, kop " + (head != null ? "gevonden" : "NIET gevonden"));
        return dragon;
    }

    static AnimatorController MakeDragonController()
    {
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(GenFolder + "/DraakV2.controller");
        ctrl.AddParameter("Wake", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var sleep = sm.AddState("Sleep");
        sleep.motion = LoadClip(DragonAnims + "sleep.fbx");
        var scream = sm.AddState("Scream");
        scream.motion = LoadClip(DragonAnims + "scream.fbx");
        var idle = sm.AddState("Idle");
        idle.motion = LoadClip(DragonAnims + "idle01.fbx");
        sm.defaultState = sleep;

        var t1 = sleep.AddTransition(scream);
        t1.AddCondition(AnimatorConditionMode.If, 0f, "Wake");
        t1.hasExitTime = false;
        t1.duration = 0.25f;

        var t2 = scream.AddTransition(idle);
        t2.hasExitTime = true;
        t2.exitTime = 0.95f;
        t2.duration = 0.3f;
        return ctrl;
    }

    static AnimationClip LoadClip(string fbxPath)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) missing.Add(fbxPath);
        return clip;
    }

    static Transform FindBone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == name) return t;
        return null;
    }

    // ---------------------------------------------------------------- ei

    static GameObject BuildEgg(Vector3 pos)
    {
        var nest = new GameObject("Nest").transform;
        nest.position = new Vector3(pos.x, FloorHeight(pos.x, pos.z), pos.z);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            float s = Random.Range(0.25f, 0.4f);
            Prim(PrimitiveType.Sphere, "Steen_" + i, nest, new Vector3(Mathf.Cos(a) * 0.75f, s * 0.3f, Mathf.Sin(a) * 0.75f), new Vector3(s, s * 0.7f, s), wallMat);
        }
        var egg = Prim(PrimitiveType.Sphere, "Drakenei", nest, new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 1.05f, 0.75f), eggMat);
        PointLight("Ei-gloed", egg.transform, new Vector3(0f, 0.1f, -0.7f), new Color(0.35f, 1f, 0.85f), 2.5f, 4f, LightShadows.None);

        Object.DestroyImmediate(egg.GetComponent<SphereCollider>());
        var col = egg.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = 0.5f;
        col.height = 1f;
        var rb = egg.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = egg.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = true;
        grab.smoothPosition = true;
        grab.smoothRotation = true;
        egg.AddComponent<GrabbableEgg>();
        return egg;
    }

    // ---------------------------------------------------------------- draak + ei + geluid koppelen

    static void WireDragon(GameObject dragon, Transform head, GameObject egg)
    {
        Transform voice = head != null ? head : dragon.transform;
        AudioSource snore = null, scream = null;
        if (snoreClip != null)
        {
            // Al hoorbaar bij het begin van de gang (~60 m), steeds luider hoe dichterbij.
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.08f, 0.85f), new Keyframe(0.25f, 0.5f),
                                           new Keyframe(0.5f, 0.24f), new Keyframe(0.8f, 0.09f), new Keyframe(1f, 0.04f));
            snore = Sound3D(voice, "Geluid_Snurken", Vector3.zero, snoreClip, 1f, 70f, curve, true);
        }
        if (screamClip != null)
        {
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 0.35f));
            scream = Sound3D(voice, "Geluid_Schreeuw", Vector3.zero, screamClip, 1f, 60f, curve, false);
            scream.spatialBlend = 0.75f;
        }

        var ge = egg.GetComponent<GrabbableEgg>();
        ge.snoreSource = snore;
        ge.screamSource = scream;
        if (crackClip != null)
        {
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.6f), new Keyframe(1f, 0f));
            ge.crackSource = Sound3D(egg.transform, "Geluid_Krak", Vector3.zero, crackClip, 1f, 15f, curve, false);
        }

        var fit = dragon.AddComponent<DragonFit>();
        fit.targetLength = DragonLength;
        fit.center = DragonCenter;
        fit.floorY = FloorHeight(DragonCenter.x, DragonCenter.z);
        fit.nest = egg.transform.parent;

        var wake = dragon.AddComponent<DragonWake>();
        wake.egg = ge;
        wake.animator = dragon.GetComponentInChildren<Animator>();
        wake.snoreSource = snore;
        wake.screamSource = scream;
    }

    // ---------------------------------------------------------------- schat achter de draak

    static void BuildHoard(Transform cave)
    {
        var hoard = new GameObject("Schat").transform;
        hoard.SetParent(cave, false);
        Vector3 c = DragonCenter + new Vector3(0f, 0f, 5.5f);
        List<GameObject> coinPrefabs = FindCoinPrefabs();
        if (coinPrefabs.Count > 0) Debug.Log("[Dragon Cave v2] Munten gevonden: " + string.Join(", ", coinPrefabs.Select(p => p.name)));
        else Debug.Log("[Dragon Cave v2] Geen munt-prefabs gevonden, ik gebruik simpele munten. Importeer een muntenpakket en bouw opnieuw.");
        for (int i = 0; i < 140; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rr = 3.2f * Mathf.Sqrt(Random.value);
            float x = c.x + Mathf.Cos(ang) * rr, z = c.z + Mathf.Sin(ang) * rr;
            float h = 1.2f * Mathf.Pow(1f - rr / 3.2f, 1.5f);
            var pos = new Vector3(x, FloorHeight(x, z) + h, z);
            var rot = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
            if (coinPrefabs.Count > 0)
            {
                var coin = (GameObject)PrefabUtility.InstantiatePrefab(coinPrefabs[i % coinPrefabs.Count], hoard);
                coin.name = "Munt_" + i;
                coin.transform.SetPositionAndRotation(pos, rot);
                float cs = HorizontalSize(coin);
                if (cs > 0.01f) coin.transform.localScale *= 0.28f / cs;   // ~28 cm, zoals de oude munten
                foreach (var col in coin.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                foreach (var rb in coin.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rb);
            }
            else Prim(PrimitiveType.Cylinder, "Munt_" + i, hoard, pos, new Vector3(0.28f, 0.02f, 0.28f), goldMat, rot);
        }
        PlaceKit("MineCart/Mine_Cart_AA", hoard, c + new Vector3(-4.2f, FloorHeight(c.x - 4.2f, c.z), -0.5f), Quaternion.Euler(0f, 70f, 8f), 1.3f, true);
        PointLight("Goudglans", hoard, c + new Vector3(0f, 2.5f, -1.5f), new Color(1f, 0.75f, 0.35f), 2f, 6f, LightShadows.None);
    }

    // Zoekt munt-prefabs in het hele project (behalve de grot-kit en de draak).
    static List<GameObject> FindCoinPrefabs()
    {
        var list = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith(Kit) || p.StartsWith("Assets/FourEvilDragonsHP") || p.StartsWith("Assets/Samples")) continue;
            string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (!(n.Contains("coin") || n.Contains("munt"))) continue;
            if (n.Contains("pile") || n.Contains("stack") || n.Contains("bag") || n.Contains("ui") || n.Contains("icon")) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null && go.GetComponentInChildren<Renderer>() != null) list.Add(go);
            if (list.Count >= 4) break;
        }
        return list;
    }

    // ---------------------------------------------------------------- licht

    static void BuildHallTorches()
    {
        var torches = new GameObject("Fakkels").transform;
        float[] angles = { -60f, -120f, 10f, 170f, 90f };
        for (int i = 0; i < angles.Length; i++)
        {
            float a = angles[i] * Mathf.Deg2Rad;
            float r = CaveRadius * 0.8f;
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            MakeTorch(torches, "Fakkel_" + (i + 1), new Vector3(x, FloorHeight(x, z) + 2.4f, z), new Vector3(-x, 0f, -z), 8f, 12f);
        }
    }

    // De draak is het hoofdding: warm spotlicht van bovenaf, enige licht met schaduw.
    static void BuildSpotlight(GameObject dragon)
    {
        Vector3 target = DragonCenter + Vector3.up * 1.2f;
        var g = new GameObject("Spotlicht_Draak");
        g.transform.position = target + new Vector3(0f, 7f, -4f);
        g.transform.LookAt(target);
        var l = g.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.72f, 0.45f);
        l.intensity = 14f;
        l.range = 18f;
        l.spotAngle = 70f;
        l.innerSpotAngle = 35f;
        l.shadows = LightShadows.Soft;
    }

    static void MakeTorch(Transform parent, string name, Vector3 pos, Vector3 facing, float intensity, float range)
    {
        var torch = new GameObject(name).transform;
        torch.SetParent(parent, false);
        torch.localPosition = pos;
        torch.localRotation = Quaternion.LookRotation(facing);
        Prim(PrimitiveType.Cylinder, "Stok", torch, Vector3.zero, new Vector3(0.08f, 0.45f, 0.08f), woodMat, Quaternion.Euler(20f, 0f, 0f));
        Prim(PrimitiveType.Sphere, "Vlam", torch, new Vector3(0f, 0.5f, 0.15f), new Vector3(0.22f, 0.32f, 0.22f), flameMat);
        Light l = PointLight("Licht", torch, new Vector3(0f, 0.7f, 0.4f), new Color(1f, 0.55f, 0.2f), intensity, range, LightShadows.None);
        var flicker = torch.gameObject.AddComponent<TorchFlicker>();
        flicker.torchLight = l;
        flicker.baseIntensity = intensity;
        if (torchClip != null)
        {
            var crackle = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 0.9f), new Keyframe(0.4f, 0.35f), new Keyframe(1f, 0f));
            var s = Sound3D(torch, "Geluid_Knetteren", new Vector3(0f, 0.5f, 0.15f), torchClip, 0.45f, 8f, crackle, true);
            s.GetComponent<RandomLoopStart>().pitchVariation = 0.06f;
        }
    }

    // ---------------------------------------------------------------- geluid

    static void LoadAudio()
    {
        torchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Torch.wav");
        snoreClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/SleepingDragon.wav");
        waterClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/WaterDripping.wav");
        screamClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/DragonScream.wav");
        crackClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/EggCrack.wav");
    }

    static AudioSource Sound3D(Transform parent, string name, Vector3 localPos, AudioClip clip, float volume, float maxDistance, AnimationCurve rolloff, bool loop)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        var s = g.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Custom;
        s.maxDistance = maxDistance;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
        if (loop) g.AddComponent<RandomLoopStart>();
        return s;
    }

    static void AddTunnelAmbience(Transform cave, List<Vector3> path)
    {
        if (waterClip == null) return;
        var g = new GameObject("Geluid_Druppels_Gang");
        g.transform.SetParent(cave, false);
        var s = g.AddComponent<AudioSource>();
        s.clip = waterClip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        var zone = g.AddComponent<AmbientZone>();
        foreach (var p in path) zone.points.Add(cave.TransformPoint(p));
        zone.innerRadius = 3f;
        zone.fadeDistance = 6f;
        zone.maxVolume = 0.7f;
        zone.outsideVolume = 0f;
    }

    static void AddDust(Transform parent)
    {
        var go = new GameObject("Stofdeeltjes");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 3f, 2f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 14f;
        main.startSpeed = 0.04f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 28f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(22f, 6f, 22f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.25f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMat;
    }

    // ---------------------------------------------------------------- speler + post

    static void BuildPlayerAndPost(List<Vector3> path)
    {
        int si = path.Count - 4;
        Vector3 spawn = path[si];
        Quaternion spawnRot = Quaternion.LookRotation((path[si - 1] - path[si + 1]).normalized);

        bool xr = VrMode && TrySpawnXRRig(spawn, spawnRot);

        var player = new GameObject(xr ? "Desktop Speler (uit, VR actief)" : "Speler");
        player.transform.SetPositionAndRotation(spawn + Vector3.up * 0.05f, Quaternion.Euler(0f, spawnRot.eulerAngles.y, 0f));
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.875f, 0f);
        cc.stepOffset = 0.4f;
        cc.slopeLimit = 50f;
        var camGo = new GameObject("Main Camera");
        if (!xr) camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        player.AddComponent<FirstPersonWalker>().cam = camGo.transform;
        camGo.AddComponent<DesktopGrabber>();
        if (xr) player.SetActive(false);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.2f);
        bloom.threshold.Override(0.9f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        AssetDatabase.CreateAsset(profile, GenFolder + "/PostProfile.asset");
        foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
        var vol = new GameObject("Post Processing").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    // Pakbare fakkel naast het spawnpunt (SenseGlove: pakken = trillen + force feedback).
    static void BuildSenseGloveTorch(List<Vector3> path)
    {
        int si = path.Count - 4;
        Quaternion spawnRot = Quaternion.LookRotation((path[si - 1] - path[si + 1]).normalized);
        DragonCaveSGTorch.BuildTorch(path[si], spawnRot, woodMat, flameMat, woodMat, torchClip);
        DragonCaveSGTorch.AddSenseGloveRig(xrRigInstance);
    }

    static bool TrySpawnXRRig(Vector3 pos, Quaternion rot)
    {
        xrRigInstance = null;
        foreach (string guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != "XR Origin (XR Rig)") continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, rot);
            xrRigInstance = go;
            foreach (var c in go.GetComponentsInChildren<Camera>(true))
                c.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return true;
        }
        Debug.LogWarning("[Dragon Cave v2] Geen 'XR Origin (XR Rig)' gevonden. Nu: desktop-camera.");
        return false;
    }

    // ---------------------------------------------------------------- kit-helpers

    // Plaatst een prefab uit de Mines and Cave Set. targetHeight > 0: schaal zodat hij zo hoog is.
    static GameObject PlaceKit(string rel, Transform parent, Vector3 pos, Quaternion rot, float targetHeight, bool collide)
    {
        string p = Kit + "prefabs/" + rel + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
        if (prefab == null) { if (!missing.Contains(rel)) missing.Add(rel); return null; }
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (targetHeight > 0f)
        {
            Bounds b = WorldBounds(g);
            if (b.size.y > 0.01f) g.transform.localScale = Vector3.one * (targetHeight / b.size.y);
        }
        Bounds nb = WorldBounds(g);
        g.transform.rotation = rot;
        g.transform.position = pos + Vector3.up * (-nb.min.y);   // onderkant op pos
        if (collide)
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        return g;
    }

    // Rekt een balk (rotatie identity) in zijn langste horizontale richting tot 'length'. true = lange as is x.
    static bool FitLength(GameObject g, float length)
    {
        Bounds b = WorldBounds(g);
        Vector3 s = g.transform.localScale;
        bool longX = b.size.x >= b.size.z;
        if (longX && b.size.x > 0.01f) s.x *= length / b.size.x;
        else if (!longX && b.size.z > 0.01f) s.z *= length / b.size.z;
        g.transform.localScale = s;
        return longX;
    }

    static void CenterXZ(GameObject g, Vector3 target)
    {
        Bounds b = WorldBounds(g);
        g.transform.position += new Vector3(target.x - b.center.x, 0f, target.z - b.center.z);
    }

    static Bounds BoneBounds(GameObject g)
    {
        var bones = g.GetComponentsInChildren<Transform>().Where(t => t != g.transform && t.GetComponent<Renderer>() == null).ToArray();
        if (bones.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        var b = new Bounds(bones[0].position, Vector3.zero);
        foreach (var t in bones) b.Encapsulate(t.position);
        return b;
    }

    static float HorizontalSize(GameObject g)
    {
        Bounds b = WorldBounds(g);
        return Mathf.Max(b.size.x, b.size.z);
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        Bounds b = new Bounds();
        bool first = true;
        foreach (var r in rs)
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds rb = r is SkinnedMeshRenderer smr ? SkinnedBounds(smr) : r.bounds;
            if (first) { b = rb; first = false; } else b.Encapsulate(rb);
        }
        return first ? new Bounds(g.transform.position, Vector3.zero) : b;
    }

    // Skinned mesh: bake de huidige pose om echte bounds te krijgen.
    static Bounds SkinnedBounds(SkinnedMeshRenderer smr)
    {
        var baked = new Mesh();
        smr.BakeMesh(baked, true);   // pose + schaal zitten er al in, alleen nog positie en rotatie
        var verts = baked.vertices;
        if (verts.Length == 0) { Object.DestroyImmediate(baked); return smr.bounds; }
        var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        Bounds b = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
        for (int i = 0; i < verts.Length; i += 4) b.Encapsulate(m.MultiplyPoint3x4(verts[i]));
        Object.DestroyImmediate(baked);
        return b;
    }

    // ---------------------------------------------------------------- algemene helpers

    static void PrepareFolders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "DragonCave");
        if (AssetDatabase.IsValidFolder(GenFolder)) AssetDatabase.DeleteAsset(GenFolder);
        AssetDatabase.CreateFolder(Root, "GeneratedV2");
    }

    static Material MakeMat(string name, Color baseCol, float smooth, float metallic, Color emission)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh) { name = name };
        m.SetColor("_BaseColor", baseCol);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, GenFolder + "/" + name + ".mat");
        return m;
    }

    static Material MakeParticleMat(string name, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, GenFolder + "/" + name + ".mat");
        return m;
    }

    static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, LightShadows shadows)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows;
        return l;
    }

    static Mesh FinishMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris, int probeVertex, Vector3 wanted)
    {
        var mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (Vector3.Dot(mesh.normals[probeVertex], wanted) < 0f)
        {
            for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildDome(float radius, float height, int lon, int lat, float noise)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        for (int y = 0; y <= lat; y++)
        {
            float phi = y / (float)lat * Mathf.PI * 0.5f;
            for (int x = 0; x <= lon; x++)
            {
                float th = x / (float)lon * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th) * radius, Mathf.Sin(phi) * height, Mathf.Cos(phi) * Mathf.Sin(th) * radius);
                float n1 = Mathf.PerlinNoise(p.x * 0.25f + 50f, p.z * 0.25f + p.y * 0.3f + 50f) - 0.5f;
                float n2 = Mathf.PerlinNoise(p.x * 0.8f + 10f, p.y * 0.8f + p.z * 0.5f + 10f) - 0.5f;
                float k = 1f + noise * n1 + noise * 0.4f * n2;
                if (y == 0) k = Mathf.Max(k, 1.02f);
                p = new Vector3(p.x * k, p.y * (1f + (k - 1f) * 0.5f), p.z * k);
                verts.Add(p);
                uvs.Add(new Vector2(x / (float)lon * 12f, y / (float)lat * 4f));
            }
        }
        var tris = new List<int>();
        int row = lon + 1;
        int entranceRows = Mathf.RoundToInt(lat * 0.3f);
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                float thDeg = (x + 0.5f) / lon * 360f;
                if (y < entranceRows && Mathf.Abs(Mathf.DeltaAngle(thDeg, EntranceAngle)) < 11f) continue;
                int a = y * row + x, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        int probe = tris[0];
        return FinishMesh("Koepel", verts, uvs, tris, probe, new Vector3(0f, 2f, 0f) - verts[probe]);
    }

    static Mesh BuildCaveFloor(float radius, int rings, int segs)
    {
        var verts = new List<Vector3> { new Vector3(0f, FloorHeight(0f, 0f), 0f) };
        var uvs = new List<Vector2> { Vector2.zero };
        for (int r = 1; r <= rings; r++)
        {
            float rr = radius * r / rings;
            for (int s = 0; s < segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr;
                verts.Add(new Vector3(x, FloorHeight(x, z), z));
                uvs.Add(new Vector2(x * 0.25f, z * 0.25f));
            }
        }
        var tris = new List<int>();
        for (int s = 0; s < segs; s++) { tris.Add(0); tris.Add(1 + (s + 1) % segs); tris.Add(1 + s); }
        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * segs, outer = 1 + r * segs;
            for (int s = 0; s < segs; s++)
            {
                int s2 = (s + 1) % segs;
                tris.Add(inner + s); tris.Add(inner + s2); tris.Add(outer + s);
                tris.Add(inner + s2); tris.Add(outer + s2); tris.Add(outer + s);
            }
        }
        return FinishMesh("Grotvloer", verts, uvs, tris, 0, Vector3.up);
    }

    static Mesh BuildTunnelMesh(List<Vector3> path, float halfWidth, float halfHeight, float centerY, int ringSegs)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        float dist = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 prev = path[Mathf.Max(0, i - 1)], next = path[Mathf.Min(path.Count - 1, i + 1)];
            Vector3 tangent = (next - prev).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (i > 0) dist += Vector3.Distance(path[i], path[i - 1]);
            for (int s = 0; s <= ringSegs; s++)
            {
                float a = s / (float)ringSegs * Mathf.PI * 2f;
                Vector3 p = path[i] + right * (Mathf.Cos(a) * halfWidth) + Vector3.up * (centerY + Mathf.Sin(a) * halfHeight);
                float n = Mathf.PerlinNoise(p.x * 0.45f + 300f, p.z * 0.45f + p.y * 0.4f + 300f) - 0.5f;
                float k = 1f + n * 0.35f;
                p = path[i] + right * (Mathf.Cos(a) * halfWidth * k) + Vector3.up * (centerY + Mathf.Sin(a) * halfHeight * k);
                if (p.y < 0f) p.y = 0f;
                verts.Add(p);
                uvs.Add(new Vector2(s / (float)ringSegs * 5f, dist * 0.35f));
            }
        }
        var tris = new List<int>();
        int row = ringSegs + 1;
        for (int i = 0; i < path.Count - 1; i++)
            for (int s = 0; s < ringSegs; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        return FinishMesh("Gang", verts, uvs, tris, 0, (path[0] + Vector3.up * (centerY + 1f)) - verts[0]);
    }
}

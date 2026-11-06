using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Dragon Cave builder (fase 2 + 2b).
// Menu: Tools > Dragon Cave > Bouw Grot
// Layout: gang met bocht (start bij daglicht) > koepelzaal > slapende draak opgekruld rond het ei, schat erachter, fakkels.
// VR: als de XRI Starter Assets geïmporteerd zijn, wordt "XR Origin (XR Rig)" aan het begin van de gang gezet.
// Opnieuw draaien = alles opnieuw genereren (Assets/DragonCave/Generated wordt vervangen).
public static class DragonCaveBuilder
{
    const string Root = "Assets/DragonCave";
    const string GenFolder = "Assets/DragonCave/Generated";
    const string ScenePath = "Assets/DragonCave/DragonCave.unity";

    const float CaveRadius = 16f;
    const float CaveHeight = 10f;
    const float EntranceAngle = -90f;   // ingang aan de -Z kant
    static readonly Vector3 NestCenter = new Vector3(0f, 0f, 3f);
    static readonly Vector3 TreasureCenter = new Vector3(4.5f, 0f, 9.5f);

    const string AudioFolder = "Assets/DragonCave/Audio";   // NIET in Generated: blijft staan bij opnieuw bouwen
    static AudioClip torchClip, dragonSleepClip, waterClip, dragonScreamClip, eggCrackClip;

    static Material rockMat, floorMat, skinMat, bellyMat, wingMat, boneMat, eggMat, flameMat, woodMat, goldMat, gemMat, slitMat, dayMat, dustMat, smokeMat;

    // Speler-modus: onthouden per pc (EditorPrefs). VR-instellingen blijven altijd bewaard,
    // de schakelaar bepaalt alleen wie er bij het spawnpunt staat.
    const string VrPref = "DragonCave_VRMode";
    const string MenuDesktop = "Tools/Dragon Cave/Modus: Desktop (WASD lopen)";
    const string MenuVR = "Tools/Dragon Cave/Modus: VR (Quest)";
    static bool VrMode => EditorPrefs.GetBool(VrPref, false);

    [MenuItem(MenuDesktop, false, 20)]
    static void SetDesktop() { EditorPrefs.SetBool(VrPref, false); Debug.Log("[Dragon Cave] Modus: Desktop. Klik Bouw Grot om toe te passen."); }
    [MenuItem(MenuDesktop, true)]
    static bool SetDesktopCheck() { Menu.SetChecked(MenuDesktop, !VrMode); return true; }

    [MenuItem(MenuVR, false, 21)]
    static void SetVR() { EditorPrefs.SetBool(VrPref, true); Debug.Log("[Dragon Cave] Modus: VR. Klik Bouw Grot om toe te passen."); }
    [MenuItem(MenuVR, true)]
    static bool SetVRCheck() { Menu.SetChecked(MenuVR, VrMode); return true; }

    [MenuItem("Tools/Dragon Cave/Bouw Grot", false, 0)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Dragon Cave", "Stop eerst Play-modus (klik nogmaals op de Play-knop) en probeer het dan opnieuw.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        PrepareFolders();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Random.InitState(42);
        MakeMaterials();
        LoadAudio();
        SetupAtmosphere();

        var cave = new GameObject("Grot").transform;
        BuildDomeAndFloor(cave);
        List<Vector3> path = BuildTunnel(cave);
        AddTunnelAmbience(cave, path);
        BuildDecor(cave);
        BuildNestAndEgg(cave, out GameObject egg);
        BuildDragon();
        WireEggToDragon(egg);
        BuildTreasure(cave);
        BuildCaveTorches();
        AddDust(cave);
        BuildPlayerAndPost(path);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = egg;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Dragon Cave] Klaar (" + (VrMode ? "VR" : "Desktop") + "): " + ScenePath +
                  ". Desktop: WASD lopen, rechtermuisknop = kijken, Shift = rennen, F = vliegmodus aan/uit, linkermuisknop = ei pakken/loslaten, B = ei kapotknijpen. VR: grijpknop pakken, trekker = knijpen. Hard laten vallen/gooien = breken.");
    }

    // ---------------------------------------------------------------- materialen + sfeer

    static void MakeMaterials()
    {
        rockMat = MakeMat("Rots", new Color(0.30f, 0.26f, 0.23f), 0.15f, 0f, Color.black);
        floorMat = MakeMat("Vloer", new Color(0.24f, 0.20f, 0.17f), 0.1f, 0f, Color.black);
        skinMat = MakeMat("Drakenhuid", new Color(0.36f, 0.05f, 0.04f), 0.55f, 0.1f, Color.black);
        bellyMat = MakeMat("Drakenbuik", new Color(0.72f, 0.52f, 0.32f), 0.4f, 0f, Color.black);
        wingMat = MakeMat("Vleugel", new Color(0.22f, 0.04f, 0.04f), 0.35f, 0f, Color.black);
        boneMat = MakeMat("Bot", new Color(0.86f, 0.81f, 0.68f), 0.35f, 0f, Color.black);
        eggMat = MakeMat("Ei", new Color(0.18f, 0.45f, 0.42f), 0.75f, 0.2f, new Color(0.05f, 0.35f, 0.3f));
        flameMat = MakeMat("Vlam", new Color(1f, 0.6f, 0.2f), 0f, 0f, new Color(4f, 1.6f, 0.3f));
        woodMat = MakeMat("Hout", new Color(0.30f, 0.18f, 0.10f), 0.1f, 0f, Color.black);
        goldMat = MakeMat("Goud", new Color(1f, 0.76f, 0.3f), 0.85f, 1f, new Color(0.15f, 0.08f, 0f));
        gemMat = MakeMat("Edelsteen", new Color(0.7f, 0.05f, 0.15f), 0.95f, 0f, new Color(1.2f, 0.05f, 0.2f));
        slitMat = MakeMat("Ooglid", new Color(0.05f, 0.01f, 0.01f), 0.2f, 0f, Color.black);
        dayMat = MakeMat("Daglicht", new Color(0.5f, 0.65f, 1f), 0f, 0f, new Color(0.9f, 1.2f, 1.8f));
        dustMat = MakeParticleMat("Stof", new Color(1f, 0.82f, 0.6f, 0.45f));
        smokeMat = MakeParticleMat("Rook", new Color(0.55f, 0.5f, 0.48f, 0.3f));
    }

    static void SetupAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.055f, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.045f, 0.028f, 0.022f);
        RenderSettings.fogDensity = 0.03f;
    }

    // ---------------------------------------------------------------- grot

    static void BuildDomeAndFloor(Transform cave)
    {
        Mesh dome = BuildDome(CaveRadius, CaveHeight, 112, 36, 0.22f);
        AssetDatabase.CreateAsset(dome, GenFolder + "/Koepel.asset");
        MeshObject("Koepel", cave, dome, rockMat);

        Mesh floor = BuildCaveFloor(CaveRadius, 40, 120);
        AssetDatabase.CreateAsset(floor, GenFolder + "/Grotvloer.asset");
        MeshObject("Vloer", cave, floor, floorMat);
    }

    static float FloorHeight(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float bumps = (Mathf.PerlinNoise(x * 0.18f + 200f, z * 0.18f + 200f) - 0.5f) * 0.8f
                    + (Mathf.PerlinNoise(x * 0.7f + 20f, z * 0.7f + 20f) - 0.5f) * 0.18f;
        float dn = Vector2.Distance(new Vector2(x, z), new Vector2(NestCenter.x, NestCenter.z));
        float nestFlat = Mathf.Clamp01((dn - 4.5f) / 3f);
        float walkFlat = z < NestCenter.z ? Mathf.Clamp01((Mathf.Abs(x) - 1.8f) / 2.5f) : 1f;
        float edge = Mathf.Clamp01((r - CaveRadius * 0.76f) / (CaveRadius * 0.24f));
        float ang = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        float entranceMask = Mathf.Clamp01((Mathf.Abs(Mathf.DeltaAngle(ang, EntranceAngle)) - 10f) / 8f);
        return bumps * nestFlat * walkFlat + edge * edge * 1.6f * entranceMask;
    }

    // ---------------------------------------------------------------- gang

    static List<Vector3> BuildTunnel(Transform cave)
    {
        var tunnel = new GameObject("Gang").transform;
        tunnel.SetParent(cave, false);

        const int steps = 60;
        const float length = 22f;
        float z0 = -CaveRadius * 0.88f;
        var path = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            path.Add(new Vector3(3.2f * Mathf.Sin(t * Mathf.PI), 0f, z0 - t * length));
        }

        Mesh mesh = BuildTunnelMesh(path, 2.7f, 3.4f, 0.4f, 28);
        AssetDatabase.CreateAsset(mesh, GenFolder + "/Gang.asset");
        MeshObject("Tunnelwand", tunnel, mesh, rockMat);

        // Poortrotsen rond de overgang gang > grot (verbergen naden)
        Vector3 c0 = path[2];
        Vector3 right0 = Vector3.Cross(Vector3.up, (path[3] - path[1]).normalized).normalized;
        for (int i = 0; i <= 8; i++)
        {
            float a = i / 8f * Mathf.PI;
            float s = Random.Range(1.4f, 2.1f);
            Vector3 p = c0 + right0 * (Mathf.Cos(a) * 3.3f) + Vector3.up * (0.4f + Mathf.Sin(a) * 4.0f);
            Prim(PrimitiveType.Sphere, "Poortrots_" + i, tunnel, p, new Vector3(s, s * 1.2f, s * 1.4f), rockMat, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }

        // Fakkels in de gang, om en om links/rechts
        int[] torchIdx = { 14, 30, 46 };
        for (int k = 0; k < torchIdx.Length; k++)
        {
            int i = torchIdx[k];
            Vector3 tangent = (path[i + 1] - path[i - 1]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float side = k % 2 == 0 ? 1f : -1f;
            Vector3 pos = path[i] + right * side * 2.25f + Vector3.up * 2.0f;
            MakeTorch(tunnel, "Gangfakkel_" + (k + 1), pos, -right * side, 6f, 9f, false);
        }

        // Daglicht aan het eind van de gang
        Vector3 end = path[steps];
        Vector3 outDir = (path[steps] - path[steps - 1]).normalized;
        Prim(PrimitiveType.Quad, "Daglicht", tunnel, end + outDir * 0.5f + Vector3.up * 1.8f, new Vector3(6f, 4.5f, 1f), dayMat, Quaternion.LookRotation(outDir));
        PointLight("Daglicht_licht", tunnel, end - outDir * 1.5f + Vector3.up * 2.5f, new Color(0.55f, 0.7f, 1f), 5f, 12f, LightShadows.None);

        return path;
    }

    // ---------------------------------------------------------------- decor

    static void BuildDecor(Transform cave)
    {
        var rocks = new GameObject("Rotsblokken").transform;
        rocks.SetParent(cave, false);
        int placed = 0;
        for (int i = 0; i < 400 && placed < 40; i++)
        {
            float ang = Random.Range(0f, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(ang, EntranceAngle)) < 22f) continue;
            float r = CaveRadius * Random.Range(0.72f, 0.93f);
            float s = Random.Range(0.8f, 2.8f);
            var scale = new Vector3(s * Random.Range(0.8f, 1.4f), s * Random.Range(0.5f, 1.1f), s * Random.Range(0.8f, 1.4f));
            float x = Mathf.Cos(ang * Mathf.Deg2Rad) * r, z = Mathf.Sin(ang * Mathf.Deg2Rad) * r;
            Prim(PrimitiveType.Sphere, "Rots_" + placed, rocks, new Vector3(x, FloorHeight(x, z) + scale.y * 0.25f, z), scale, rockMat, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), 0f));
            placed++;
        }

        // Kiezels verspreid over de vloer
        var pebbles = new GameObject("Kiezels").transform;
        pebbles.SetParent(cave, false);
        for (int i = 0; i < 80; i++)
        {
            Vector2 p = Random.insideUnitCircle * CaveRadius * 0.8f;
            if (Mathf.Abs(p.x) < 1.6f && p.y < NestCenter.z - 4f) continue;
            if (Vector2.Distance(p, new Vector2(NestCenter.x, NestCenter.z)) < 4.5f) continue;
            float s = Random.Range(0.12f, 0.45f);
            Prim(PrimitiveType.Sphere, "Kiezel_" + i, pebbles, new Vector3(p.x, FloorHeight(p.x, p.y) + s * 0.2f, p.y), new Vector3(s, s * 0.6f, s * Random.Range(0.8f, 1.3f)), rockMat);
        }

        // Stalactieten en stalagmieten
        var stal = new GameObject("Stalactieten").transform;
        stal.SetParent(cave, false);
        for (int i = 0; i < 28; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(3f, CaveRadius * 0.75f);
            float ceiling = CaveHeight * Mathf.Sqrt(Mathf.Max(0f, 1f - (r * r) / (CaveRadius * CaveRadius)));
            float k = Random.Range(0.6f, 1.6f);
            var scale = new Vector3(0.35f * k, 1.8f * k, 0.35f * k);
            Prim(PrimitiveType.Sphere, "Stalactiet_" + i, stal, new Vector3(Mathf.Cos(ang) * r, ceiling - scale.y * 0.3f, Mathf.Sin(ang) * r), scale, rockMat);
        }
        for (int i = 0; i < 10; i++)
        {
            float ang = Random.Range(0f, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(ang, EntranceAngle)) < 25f) continue;
            float r = CaveRadius * Random.Range(0.55f, 0.75f);
            float x = Mathf.Cos(ang * Mathf.Deg2Rad) * r, z = Mathf.Sin(ang * Mathf.Deg2Rad) * r;
            float k = Random.Range(0.8f, 1.8f);
            Prim(PrimitiveType.Sphere, "Stalagmiet_" + i, stal, new Vector3(x, FloorHeight(x, z), z), new Vector3(0.6f * k, 2.4f * k, 0.6f * k), rockMat);
        }

        // Botten van eerdere indringers
        var bones = new GameObject("Botten").transform;
        bones.SetParent(cave, false);
        for (int i = 0; i < 9; i++)
        {
            Vector2 p = Random.insideUnitCircle * CaveRadius * 0.65f;
            if (Mathf.Abs(p.x) < 1.6f && p.y < NestCenter.z - 4f) continue;
            if (Vector2.Distance(p, new Vector2(NestCenter.x, NestCenter.z)) < 5f) continue;
            float y = FloorHeight(p.x, p.y) + 0.08f;
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var bone = new GameObject("Bot_" + i).transform;
            bone.SetParent(bones, false);
            bone.localPosition = new Vector3(p.x, y, p.y);
            bone.localRotation = rot;
            Prim(PrimitiveType.Sphere, "Schacht", bone, Vector3.zero, new Vector3(0.12f, 0.12f, 0.9f), boneMat);
            Prim(PrimitiveType.Sphere, "Knobbel_A", bone, new Vector3(0f, 0f, 0.45f), Vector3.one * 0.2f, boneMat);
            Prim(PrimitiveType.Sphere, "Knobbel_B", bone, new Vector3(0f, 0f, -0.45f), Vector3.one * 0.2f, boneMat);
            if (i % 3 == 0) Prim(PrimitiveType.Sphere, "Schedel", bone, new Vector3(0.5f, 0.12f, 0.2f), new Vector3(0.35f, 0.3f, 0.42f), boneMat);
        }
    }

    // ---------------------------------------------------------------- nest + ei

    static void BuildNestAndEgg(Transform cave, out GameObject egg)
    {
        var nest = new GameObject("Nest").transform;
        nest.SetParent(cave, false);
        nest.localPosition = NestCenter;
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            float s = Random.Range(0.3f, 0.5f);
            Prim(PrimitiveType.Sphere, "Steen_" + i, nest, new Vector3(Mathf.Cos(a) * 0.95f, s * 0.3f, Mathf.Sin(a) * 0.95f), new Vector3(s, s * 0.7f, s), rockMat);
        }
        egg = Prim(PrimitiveType.Sphere, "Drakenei", nest, new Vector3(0f, 0.72f, 0f), new Vector3(1f, 1.4f, 1f), eggMat);
        // gloed hangt aan het ei zelf, zodat hij meegaat als je het ei steelt
        PointLight("Ei-gloed", egg.transform, new Vector3(0f, 0.13f, -0.65f), new Color(0.35f, 1f, 0.85f), 2.5f, 4f, LightShadows.None);
        MakeEggGrabbable(egg);
    }

    static void MakeEggGrabbable(GameObject egg)
    {
        // eivormige collider in plaats van een bol
        Object.DestroyImmediate(egg.GetComponent<SphereCollider>());
        var col = egg.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = 0.5f;
        col.height = 1f;

        var rb = egg.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.isKinematic = true;   // ligt stil in het nest tot je hem pakt
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // VR: grijpbaar met de grijpknop (dichtbij of met de straal van de Starter Assets-rig)
        var grab = egg.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.useDynamicAttach = true;      // vast op de plek waar je hand hem raakt
        grab.throwOnDetach = true;
        grab.smoothPosition = true;
        grab.smoothRotation = true;

        egg.AddComponent<GrabbableEgg>();
    }

    // ---------------------------------------------------------------- draak

    static void BuildDragon()
    {
        var dragon = new GameObject("Draak (placeholder)").transform;
        var body = new GameObject("Lichaam").transform;
        body.SetParent(dragon, false);

        const int segments = 26;
        const float ringR = 3.2f;
        var ring = new List<Vector3>();
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            float ang = Mathf.Lerp(-35f, 225f, t) * Mathf.Deg2Rad;
            float size = t < 0.12f ? Mathf.Lerp(2.2f, 2.8f, t / 0.12f) : Mathf.Lerp(2.8f, 0.35f, (t - 0.12f) / 0.88f);
            var pos = NestCenter + new Vector3(Mathf.Cos(ang) * ringR, size * 0.42f, Mathf.Sin(ang) * ringR);
            ring.Add(pos);
            var tangent = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            GameObject seg = Prim(PrimitiveType.Sphere, "Segment_" + i, body, pos, new Vector3(size, size * 0.85f, size * 1.15f), skinMat, Quaternion.LookRotation(tangent));
            // buikplaat aan de binnenkant (lokale -x = richting nest)
            Prim(PrimitiveType.Sphere, "Buik", seg.transform, new Vector3(-0.22f, -0.18f, 0f), new Vector3(0.78f, 0.7f, 0.95f), bellyMat);
            // rugstekels
            if (i % 2 == 0 && t < 0.9f)
                Prim(PrimitiveType.Sphere, "Stekel", seg.transform, new Vector3(0f, 0.48f, -0.1f), new Vector3(0.1f, 0.55f, 0.28f), boneMat, Quaternion.Euler(-25f, 0f, 0f));
            var breath = seg.AddComponent<DragonBreath>();
            breath.phase = t * 1.5f;
            breath.amount = 0.04f * (1f - t);
        }

        // Staartpunt
        Vector3 tip = ring[segments - 1];
        Vector3 tipDir = (ring[segments - 1] - ring[segments - 2]).normalized;
        Prim(PrimitiveType.Cube, "Staartpunt", dragon, tip + tipDir * 0.4f, new Vector3(0.6f, 0.08f, 0.6f), boneMat, Quaternion.LookRotation(tipDir) * Quaternion.Euler(0f, 45f, 0f));

        // Achterpoten (dijen buiten de ring)
        float[] hipAngles = { 70f, 160f };
        foreach (float ha in hipAngles)
        {
            float a = ha * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 hip = NestCenter + radial * (ringR + 0.9f) + Vector3.up * 0.8f;
            Prim(PrimitiveType.Sphere, "Dij", dragon, hip, new Vector3(1.6f, 1.3f, 1.9f), skinMat, Quaternion.LookRotation(new Vector3(-radial.z, 0f, radial.x)));
            Prim(PrimitiveType.Sphere, "Achterklauw", dragon, hip + radial * 0.6f - Vector3.up * 0.6f, new Vector3(0.8f, 0.4f, 1.1f), skinMat);
        }

        // Kop: rust op de grond rechtsvoor het ei, ogen dicht
        var head = new GameObject("Kop").transform;
        head.SetParent(dragon, false);
        head.localPosition = NestCenter + new Vector3(1.7f, 0.6f, -2.1f);
        head.localRotation = Quaternion.LookRotation(new Vector3(-1f, 0f, -0.3f));
        Prim(PrimitiveType.Sphere, "Schedel", head, Vector3.zero, new Vector3(1.4f, 1.0f, 1.7f), skinMat);
        Prim(PrimitiveType.Cube, "Snuit", head, new Vector3(0f, -0.1f, 1.1f), new Vector3(0.8f, 0.5f, 1.1f), skinMat);
        Prim(PrimitiveType.Cube, "Kaak", head, new Vector3(0f, -0.42f, 0.95f), new Vector3(0.72f, 0.22f, 1.0f), bellyMat);
        Prim(PrimitiveType.Sphere, "Wenkbrauw_L", head, new Vector3(-0.42f, 0.35f, 0.35f), new Vector3(0.35f, 0.22f, 0.6f), skinMat);
        Prim(PrimitiveType.Sphere, "Wenkbrauw_R", head, new Vector3(0.42f, 0.35f, 0.35f), new Vector3(0.35f, 0.22f, 0.6f), skinMat);
        Prim(PrimitiveType.Sphere, "Ooglid_L", head, new Vector3(-0.58f, 0.18f, 0.4f), new Vector3(0.05f, 0.06f, 0.35f), slitMat);
        Prim(PrimitiveType.Sphere, "Ooglid_R", head, new Vector3(0.58f, 0.18f, 0.4f), new Vector3(0.05f, 0.06f, 0.35f), slitMat);
        Prim(PrimitiveType.Sphere, "Neusgat_L", head, new Vector3(-0.2f, 0.08f, 1.64f), Vector3.one * 0.12f, slitMat);
        Prim(PrimitiveType.Sphere, "Neusgat_R", head, new Vector3(0.2f, 0.08f, 1.64f), Vector3.one * 0.12f, slitMat);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 b = new Vector3(0.45f * side, 0.45f, -0.5f);
            Prim(PrimitiveType.Sphere, "Hoorn_basis", head, b, new Vector3(0.28f, 0.28f, 1.1f), boneMat, Quaternion.Euler(-20f, 18f * side, 0f));
            Prim(PrimitiveType.Sphere, "Hoorn_punt", head, b + new Vector3(0.25f * side, 0.5f, -0.75f), new Vector3(0.16f, 0.16f, 0.9f), boneMat, Quaternion.Euler(-55f, 28f * side, 0f));
        }
        AddSmoke(head, new Vector3(0f, 0.1f, 1.75f));

        // Snurken: uit de snuit, ver hoorbaar maar pas echt luid dichtbij.
        // Curve over 0..maxDistance (26 m): bij de grotingang (~15 m) nog zacht, in de gang stil.
        if (dragonSleepClip != null)
        {
            var snore = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.95f), new Keyframe(0.35f, 0.45f), new Keyframe(0.7f, 0.12f), new Keyframe(1f, 0f));
            Sound3D(head, "Geluid_Snurken", new Vector3(0f, 0f, 1.2f), dragonSleepClip, 0.9f, 26f, snore, 0f);
        }
        Limb("Nek", dragon, ring[0] + Vector3.up * 0.4f, head.localPosition, 1.2f, skinMat);

        // Voorpoten met klauwen: het ei ligt ertussen
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 paw = NestCenter + new Vector3(1.1f * side, 0.25f, -1.4f);
            Prim(PrimitiveType.Sphere, side > 0 ? "Poot_R" : "Poot_L", dragon, paw, new Vector3(0.9f, 0.5f, 1.2f), skinMat);
            for (int c = -1; c <= 1; c++)
                Prim(PrimitiveType.Sphere, "Klauw", dragon, paw + new Vector3(c * 0.28f, -0.1f, -0.6f), new Vector3(0.1f, 0.1f, 0.35f), boneMat, Quaternion.Euler(20f, c * 10f, 0f));
            Limb(side > 0 ? "Arm_R" : "Arm_L", dragon, NestCenter + new Vector3(2.9f * side, 0.9f, 0.2f), paw, 0.75f, skinMat);
        }

        // Gevouwen vleugels met botrand
        for (int w = 0; w < 2; w++)
        {
            float ang = (w == 0 ? 50f : 120f) * Mathf.Deg2Rad;
            var p = NestCenter + new Vector3(Mathf.Cos(ang) * ringR, 2.6f, Mathf.Sin(ang) * ringR);
            var tangent = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            Quaternion rot = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, w == 0 ? 28f : -28f);
            GameObject wing = Prim(PrimitiveType.Sphere, w == 0 ? "Vleugel_R" : "Vleugel_L", dragon, p, new Vector3(0.15f, 1.6f, 3.6f), wingMat, rot);
            Prim(PrimitiveType.Sphere, "Vleugelbot", wing.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.12f, 1.05f), boneMat);
        }
    }

    // ---------------------------------------------------------------- schat

    static void BuildTreasure(Transform cave)
    {
        var hoard = new GameObject("Schat").transform;
        hoard.SetParent(cave, false);
        for (int i = 0; i < 160; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rr = 3.4f * Mathf.Sqrt(Random.value);
            float x = TreasureCenter.x + Mathf.Cos(ang) * rr;
            float z = TreasureCenter.z + Mathf.Sin(ang) * rr;
            float h = 1.4f * Mathf.Pow(1f - rr / 3.4f, 1.5f);
            Prim(PrimitiveType.Cylinder, "Munt_" + i, hoard, new Vector3(x, FloorHeight(x, z) + h + Random.Range(0f, 0.1f), z),
                new Vector3(0.28f, 0.02f, 0.28f), goldMat, Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f)));
        }
        // Schatkist
        var chest = new GameObject("Schatkist").transform;
        chest.SetParent(hoard, false);
        float cx = TreasureCenter.x - 3.6f, cz = TreasureCenter.z - 0.8f;
        chest.localPosition = new Vector3(cx, FloorHeight(cx, cz), cz);
        chest.localRotation = Quaternion.Euler(0f, 30f, 0f);
        Prim(PrimitiveType.Cube, "Kist", chest, new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.7f, 0.8f), woodMat);
        Prim(PrimitiveType.Cube, "Deksel", chest, new Vector3(0f, 0.85f, -0.35f), new Vector3(1.2f, 0.12f, 0.8f), woodMat, Quaternion.Euler(-60f, 0f, 0f));
        Prim(PrimitiveType.Cube, "Beslag", chest, new Vector3(0f, 0.35f, 0f), new Vector3(1.24f, 0.1f, 0.84f), goldMat);
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Sphere, "Edelsteen_" + i, chest, new Vector3(Random.Range(-0.4f, 0.4f), 0.72f, Random.Range(-0.25f, 0.25f)), Vector3.one * 0.14f, gemMat);
        // Bekers
        for (int i = 0; i < 3; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float x = TreasureCenter.x + Mathf.Cos(ang) * 2.6f, z = TreasureCenter.z + Mathf.Sin(ang) * 2.6f;
            Prim(PrimitiveType.Cylinder, "Beker_" + i, hoard, new Vector3(x, FloorHeight(x, z) + 0.45f, z), new Vector3(0.25f, 0.2f, 0.25f), goldMat, Quaternion.Euler(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f)));
        }
        PointLight("Goudglans", hoard, TreasureCenter + new Vector3(-1f, 2.5f, -2f), new Color(1f, 0.75f, 0.35f), 2f, 6f, LightShadows.None);
    }

    // ---------------------------------------------------------------- fakkels

    static void BuildCaveTorches()
    {
        var torches = new GameObject("Fakkels").transform;
        float[] angles = { -62f, -118f, 20f, 160f, 75f };
        for (int i = 0; i < angles.Length; i++)
        {
            float a = angles[i] * Mathf.Deg2Rad;
            float r = CaveRadius * 0.86f;
            Vector3 inward = new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a));
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            MakeTorch(torches, "Fakkel_" + (i + 1), new Vector3(x, FloorHeight(x, z) + 2.4f, z), inward, 9f, 13f, false);
        }
    }

    static void MakeTorch(Transform parent, string name, Vector3 pos, Vector3 facing, float intensity, float range, bool shadows)
    {
        var torch = new GameObject(name).transform;
        torch.SetParent(parent, false);
        torch.localPosition = pos;
        torch.localRotation = Quaternion.LookRotation(facing);
        Prim(PrimitiveType.Cylinder, "Stok", torch, Vector3.zero, new Vector3(0.08f, 0.45f, 0.08f), woodMat, Quaternion.Euler(20f, 0f, 0f));
        Prim(PrimitiveType.Sphere, "Vlam", torch, new Vector3(0f, 0.5f, 0.15f), new Vector3(0.22f, 0.32f, 0.22f), flameMat);
        Light l = PointLight("Licht", torch, new Vector3(0f, 0.7f, 0.4f), new Color(1f, 0.55f, 0.2f), intensity, range, shadows ? LightShadows.Soft : LightShadows.None);
        var flicker = torch.gameObject.AddComponent<TorchFlicker>();
        flicker.torchLight = l;
        flicker.baseIntensity = intensity;

        // Knetteren: zacht, alleen in de buurt (8 m). Elke fakkel start op een ander punt.
        if (torchClip != null)
        {
            var crackle = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 0.9f), new Keyframe(0.4f, 0.35f), new Keyframe(1f, 0f));
            Sound3D(torch, "Geluid_Knetteren", new Vector3(0f, 0.5f, 0.15f), torchClip, 0.45f, 8f, crackle, 0.06f);
        }
    }

    // ---------------------------------------------------------------- geluid

    static void LoadAudio()
    {
        torchClip = LoadClip("Torch.wav", AudioClipLoadType.CompressedInMemory);
        dragonSleepClip = LoadClip("SleepingDragon.wav", AudioClipLoadType.CompressedInMemory);
        waterClip = LoadClip("WaterDripping.wav", AudioClipLoadType.CompressedInMemory);
        dragonScreamClip = LoadClip("DragonScream.wav", AudioClipLoadType.DecompressOnLoad);   // ei breekt
        eggCrackClip = LoadClip("EggCrack.wav", AudioClipLoadType.DecompressOnLoad);
    }

    // Eenmalig geluid (niet loopen), 3D met custom rolloff.
    static AudioSource OneShot3D(Transform parent, string name, Vector3 localPos, AudioClip clip, float volume, float spatial, float maxDistance, AnimationCurve rolloff)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        var s = g.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = false;
        s.playOnAwake = false;
        s.volume = volume;
        s.spatialBlend = spatial;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Custom;
        s.maxDistance = maxDistance;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
        return s;
    }

    // Ei breekt: krak op het ei zelf, draak stopt met snurken en schreeuwt.
    static void WireEggToDragon(GameObject egg)
    {
        var ge = egg.GetComponent<GrabbableEgg>();
        if (ge == null) return;

        if (eggCrackClip != null)
        {
            var crackCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.6f), new Keyframe(1f, 0f));
            ge.crackSource = OneShot3D(egg.transform, "Geluid_Krak", Vector3.zero, eggCrackClip, 1f, 1f, 15f, crackCurve);
        }

        var head = GameObject.Find("Kop");
        if (head != null)
        {
            var snore = head.transform.Find("Geluid_Snurken");
            if (snore != null) ge.snoreSource = snore.GetComponent<AudioSource>();
            if (dragonScreamClip != null)
            {
                // de schreeuw moet je overal in de grot en gang horen, maar wel van de draak af komen
                var screamCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 0.35f));
                ge.screamSource = OneShot3D(head.transform, "Geluid_Schreeuw", new Vector3(0f, 0f, 1.2f), dragonScreamClip, 1f, 0.75f, 60f, screamCurve);
            }
        }
        if (ge.screamSource == null) Debug.LogWarning("[Dragon Cave] Geen draak-schreeuw gekoppeld aan het ei (DragonScream.wav of 'Kop' ontbreekt).");
    }

    static AudioClip LoadClip(string file, AudioClipLoadType loadType)
    {
        string p = AudioFolder + "/" + file;
        var imp = AssetImporter.GetAtPath(p) as AudioImporter;
        if (imp == null)
        {
            Debug.LogWarning("[Dragon Cave] Geluid ontbreekt: " + p);
            return null;
        }
        var s = imp.defaultSampleSettings;
        if (s.loadType != loadType || s.compressionFormat != AudioCompressionFormat.Vorbis || !imp.loadInBackground)
        {
            s.loadType = loadType;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
    }

    static AudioSource Sound3D(Transform parent, string name, Vector3 localPos, AudioClip clip, float volume, float maxDistance, AnimationCurve rolloff, float pitchVariation)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        var s = g.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;          // RandomLoopStart start hem
        s.volume = volume;
        s.spatialBlend = 1f;            // volledig 3D: links/rechts + afstand
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Custom;
        s.maxDistance = maxDistance;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
        g.AddComponent<RandomLoopStart>().pitchVariation = pitchVariation;
        return s;
    }

    // Druppels: achtergrond in de hele gang, vervaagt zodra je de koepelzaal in loopt.
    static void AddTunnelAmbience(Transform cave, List<Vector3> path)
    {
        if (waterClip == null) return;
        var g = new GameObject("Geluid_Druppels_Gang");
        g.transform.SetParent(cave, false);
        var s = g.AddComponent<AudioSource>();
        s.clip = waterClip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;            // 2D sfeerlaag, stereo blijft behouden
        s.volume = 0f;
        var zone = g.AddComponent<AmbientZone>();
        foreach (var p in path) zone.points.Add(cave.TransformPoint(p));
        zone.innerRadius = 3f;
        zone.fadeDistance = 6f;
        zone.maxVolume = 0.7f;
        zone.outsideVolume = 0f;
    }

    // ---------------------------------------------------------------- deeltjes

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
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 35f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 6f, 24f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.25f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMat;
    }

    static void AddSmoke(Transform head, Vector3 localPos)
    {
        var go = new GameObject("Neusrook");
        go.transform.SetParent(head, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.35f;
        main.startSize = 0.25f;
        main.gravityModifier = -0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 3f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.08f;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
    }

    // ---------------------------------------------------------------- speler + post

    static void BuildPlayerAndPost(List<Vector3> path)
    {
        int si = path.Count - 4;
        Vector3 spawn = path[si];
        Vector3 toCave = (path[si - 1] - path[si + 1]).normalized;
        Quaternion spawnRot = Quaternion.LookRotation(toCave);

        // VR-modus: XR Origin zoals eerder. Desktop-modus (of geen rig gevonden): lopende speler.
        bool xr = VrMode && TrySpawnXRRig(spawn, spawnRot);
        if (!VrMode) Debug.Log("[Dragon Cave] Desktop-modus: geen VR-rig geplaatst. VR terug? Tools > Dragon Cave > Modus: VR, en opnieuw bouwen.");

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
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.9f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        AssetDatabase.CreateAsset(profile, GenFolder + "/PostProfile.asset");
        foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
        var vol = new GameObject("Post Processing").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    static bool TrySpawnXRRig(Vector3 pos, Quaternion rot)
    {
        foreach (string guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != "XR Origin (XR Rig)") continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, rot);
            foreach (var c in go.GetComponentsInChildren<Camera>(true))
                c.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Debug.Log("[Dragon Cave] VR-rig geplaatst: " + path);
            return true;
        }
        Debug.LogWarning("[Dragon Cave] Geen 'XR Origin (XR Rig)' gevonden. Importeer via Package Manager > XR Interaction Toolkit > Samples > Starter Assets, en bouw opnieuw. Nu: desktop-camera.");
        return false;
    }

    // ---------------------------------------------------------------- helpers

    static void PrepareFolders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "DragonCave");
        if (AssetDatabase.IsValidFolder(GenFolder)) AssetDatabase.DeleteAsset(GenFolder);
        AssetDatabase.CreateFolder(Root, "Generated");
    }

    static Material MakeMat(string name, Color baseCol, float smooth, float metallic, Color emission)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
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
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = name };
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
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

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        return Prim(type, name, parent, pos, scale, mat, Quaternion.identity);
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static void Limb(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material mat)
    {
        Vector3 dir = to - from;
        Prim(PrimitiveType.Sphere, name, parent, (from + to) * 0.5f, new Vector3(thickness, thickness, dir.magnitude + thickness * 0.5f), mat, Quaternion.LookRotation(dir.normalized));
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

    // Maakt de mesh af en zorgt dat de zichtbare kant naar 'wanted' wijst.
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
            for (int i = 0; i < tris.Count; i += 3)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
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
                uvs.Add(new Vector2(x / (float)lon * 6f, y / (float)lat * 3f));
            }
        }
        var tris = new List<int>();
        int row = lon + 1;
        int entranceRows = Mathf.RoundToInt(lat * 0.3f);
        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                float thDeg = (x + 0.5f) / lon * 360f;
                if (y < entranceRows && Mathf.Abs(Mathf.DeltaAngle(thDeg, EntranceAngle)) < 11f) continue;
                int a = y * row + x, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
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
                uvs.Add(new Vector2(x * 0.3f, z * 0.3f));
            }
        }
        var tris = new List<int>();
        for (int s = 0; s < segs; s++)
        {
            tris.Add(0); tris.Add(1 + (s + 1) % segs); tris.Add(1 + s);
        }
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

    // Buis langs een pad, onderkant platgedrukt tot een vlakke vloer (y = 0).
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
                Vector3 local = right * (Mathf.Cos(a) * halfWidth) + Vector3.up * (centerY + Mathf.Sin(a) * halfHeight);
                Vector3 p = path[i] + local;
                float n = Mathf.PerlinNoise(p.x * 0.45f + 300f, p.z * 0.45f + p.y * 0.4f + 300f) - 0.5f;
                float k = 1f + n * 0.35f;
                p = path[i] + right * (Mathf.Cos(a) * halfWidth * k) + Vector3.up * (centerY + Mathf.Sin(a) * halfHeight * k);
                if (p.y < 0f) p.y = 0f;
                verts.Add(p);
                uvs.Add(new Vector2(s / (float)ringSegs * 4f, dist * 0.3f));
            }
        }
        var tris = new List<int>();
        int row = ringSegs + 1;
        for (int i = 0; i < path.Count - 1; i++)
        {
            for (int s = 0; s < ringSegs; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        // probe: ring 0, s = 0 is de rechterwand; binnenkant = richting padcentrum
        return FinishMesh("Gang", verts, uvs, tris, 0, (path[0] + Vector3.up * (centerY + 1f)) - verts[0]);
    }
}

using UnityEditor;
using UnityEngine;
using SG;

// Dragon Cave v2: pakbare SenseGlove-fakkel + SenseGlove-rig.
// Wordt aangeroepen vanuit DragonCaveV2Builder. Vereist de map Assets/SenseGlove (plugin) en Assets/Cave project (trilprofielen).
public static class DragonCaveSGTorch
{
    const string HoldWaveGuid = "f14800d92e9619445b971325027b4c48";  // Cave project/Constantvibration.asset
    const string StopWaveGuid = "7bab439d248168d40ab9276a89deffb5";  // Cave project/Stopvibration.asset
    const string FireMatGuid  = "dffbc3fce13e69840afd3f75ea8ab4ae";  // SenseGlove/Materials/Torch fire.mat (Glow-shader)

    // Fakkel op een houten paal naast het spawnpunt, steel schuin omhoog zodat je hem makkelijk pakt.
    public static GameObject BuildTorch(Vector3 spawn, Quaternion spawnRot, Material wood, Material fallbackFlame, Material stand, AudioClip crackleClip)
    {
        Vector3 fwd = spawnRot * Vector3.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 basePos = spawn + fwd * 0.8f + right * 0.45f;

        // paal (statisch)
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Fakkelpaal";
        post.transform.position = basePos + Vector3.up * 0.45f;
        post.transform.localScale = new Vector3(0.18f, 0.9f, 0.18f);
        post.GetComponent<Renderer>().sharedMaterial = stand;

        // fakkel: root = Rigidbody + SG_Grabable, kinderen = steel, vlam, licht, geluid
        var torch = new GameObject("Pakbare Fakkel (SenseGlove)");
        torch.transform.position = basePos + Vector3.up * 1.12f;
        torch.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(-25f, 0f, 0f) * Quaternion.Euler(90f, 0f, 0f);

        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = "Steel";
        Object.DestroyImmediate(stick.GetComponent<Collider>());
        stick.transform.SetParent(torch.transform, false);
        stick.transform.localScale = new Vector3(0.045f, 0.22f, 0.045f);   // 44 cm lang, 4,5 cm dik
        stick.GetComponent<Renderer>().sharedMaterial = wood;

        var fireMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(FireMatGuid));
        var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Vlam";
        Object.DestroyImmediate(flame.GetComponent<Collider>());
        flame.transform.SetParent(torch.transform, false);
        flame.transform.localPosition = new Vector3(0f, 0.27f, 0f);
        flame.transform.localScale = new Vector3(0.11f, 0.16f, 0.11f);
        flame.GetComponent<Renderer>().sharedMaterial = fireMat != null ? fireMat : fallbackFlame;

        var lightGo = new GameObject("Licht");
        lightGo.transform.SetParent(torch.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.36f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.55f, 0.2f);
        l.intensity = 5f;
        l.range = 7f;
        l.shadows = LightShadows.None;
        var flicker = torch.AddComponent<TorchFlicker>();
        flicker.torchLight = l;
        flicker.baseIntensity = 5f;

        if (crackleClip != null)
        {
            var sGo = new GameObject("Geluid_Knetteren");
            sGo.transform.SetParent(torch.transform, false);
            sGo.transform.localPosition = new Vector3(0f, 0.27f, 0f);
            var s = sGo.AddComponent<AudioSource>();
            s.clip = crackleClip;
            s.loop = true;
            s.playOnAwake = true;
            s.volume = 0.5f;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.3f;
            s.maxDistance = 8f;
        }

        var col = torch.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = 0.04f;
        col.height = 0.6f;
        col.center = new Vector3(0f, 0.04f, 0f);

        var rb = torch.AddComponent<Rigidbody>();
        rb.mass = 0.6f;
        rb.useGravity = true;
        rb.isKinematic = true;   // blijft netjes op de paal tot je hem pakt
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var grab = torch.AddComponent<SG_Grabable>();
        grab.baseTransform = torch.transform;
        grab.physicsBody = rb;
        torch.AddComponent<SG_Material>();   // force feedback: vingers stoppen op de steel

        var haptics = torch.AddComponent<TorchHaptics>();
        haptics.holdWaveform = AssetDatabase.LoadAssetAtPath<SG_CustomWaveform>(AssetDatabase.GUIDToAssetPath(HoldWaveGuid));
        haptics.stopWaveform = AssetDatabase.LoadAssetAtPath<SG_CustomWaveform>(AssetDatabase.GUIDToAssetPath(StopWaveGuid));
        if (haptics.holdWaveform == null || haptics.stopWaveform == null)
            Debug.LogWarning("[Dragon Cave v2] Trilprofielen niet gevonden: kopieer de map 'Cave project' mee uit VR_Torch_test.");

        return torch;
    }

    // [SG_User] (handen) in de scène + koppeling met de XR Origin, zodat de handen meelopen met de rig op het spawnpunt.
    public static void AddSenseGloveRig(GameObject xrOrigin)
    {
        GameObject prefab = null;
        foreach (string guid in AssetDatabase.FindAssets("SG_User t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == "[SG_User]") { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p); break; }
        }
        if (prefab == null) { Debug.LogWarning("[Dragon Cave v2] [SG_User]-prefab niet gevonden: is de map SenseGlove gekopieerd?"); return; }
        PrefabUtility.InstantiatePrefab(prefab);

        if (xrOrigin == null) { Debug.LogWarning("[Dragon Cave v2] Geen XR Origin: SenseGlove-handen zonder rig-koppeling (zet VR-modus aan)."); return; }

        var links = new GameObject("SenseGlove Tracking Links").AddComponent<SG_XR_SceneTrackingLinks>();
        Transform offset = FindDeep(xrOrigin.transform, "Camera Offset");
        links.xrRig = offset != null ? offset : xrOrigin.transform;
        var cam = xrOrigin.GetComponentInChildren<Camera>(true);
        links.head = cam != null ? cam.transform : null;
        links.leftHandTrackingDevice = FindDeep(xrOrigin.transform, "Left Controller");
        links.rightHandTrackingDevice = FindDeep(xrOrigin.transform, "Right Controller");
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

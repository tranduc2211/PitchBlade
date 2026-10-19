using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Generates every demo asset (pitch texture, goal net, materials, scene, GrassSystem prefab)
// so the repository only has to version the grass system itself plus this script.
// Menu: Tools/Stadium Grass Demo. Batch mode: -executeMethod DemoSceneBuilder.BuildAll
public static class DemoSceneBuilder
{
    const string Root = "Assets/Demo";
    const string GrassRoot = "Assets/GrassRendering";
    const string ScenePath = Root + "/Scenes/StadiumGrassDemo.unity";
    const string PitchTexturePath = Root + "/Textures/PitchGrass.jpg";
    const string NetTexturePath = Root + "/Textures/GoalNet.png";

    // Plane is 80 m (x) by 120 m (z); the FIFA pitch (68 x 105) sits in the middle, length along z.
    const float PlaneWidth = 80f;
    const float PlaneLength = 120f;
    const int PitchTexWidth = 2048;
    const int PitchTexHeight = 3072;
    const float HalfPitchWidth = 34f;
    const float HalfPitchLength = 52.5f;
    const float LineHalfWidth = 0.06f;

    const float GoalHalfWidth = 3.66f;
    const float GoalHeight = 2.44f;
    const float GoalDepth = 2f;
    const float PostDiameter = 0.12f;
    const float NetTileMeters = 1.2f;

    static int FieldLayer => LayerMask.NameToLayer("Field");
    static int GrassCamLineLayer => LayerMask.NameToLayer("GrassCamLine");

    [MenuItem("Tools/Stadium Grass Demo/Rebuild Everything")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(Root + "/Textures");
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Scenes");

        GeneratePitchTexture();
        GenerateNetTexture();
        BuildScene();
    }

    [MenuItem("Tools/Stadium Grass Demo/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/StadiumGrassDemo.exe",
            target = BuildTarget.StandaloneWindows64,
        });
    }

    // ---------------------------------------------------------------- textures

    static void GeneratePitchTexture()
    {
        var light = new Color(0.40f, 0.60f, 0.22f);
        var dark = new Color(0.31f, 0.50f, 0.17f);
        var line = new Color(0.93f, 0.95f, 0.91f);
        float metersPerPixel = PlaneWidth / PitchTexWidth;

        var pixels = new Color32[PitchTexWidth * PitchTexHeight];
        for (int j = 0; j < PitchTexHeight; j++)
        {
            float z = (j + 0.5f) / PitchTexHeight * PlaneLength - PlaneLength / 2;
            int stripe = Mathf.FloorToInt((z + HalfPitchLength) / (2 * HalfPitchLength / 20));
            Color stripeColor = (stripe & 1) == 0 ? light : dark;

            for (int i = 0; i < PitchTexWidth; i++)
            {
                float x = (i + 0.5f) / PitchTexWidth * PlaneWidth - PlaneWidth / 2;

                // Large clumps, small tufts and per-pixel grain, so the sampled blades vary.
                float noise = (Mathf.PerlinNoise(x * 0.35f + 11.3f, z * 0.35f + 7.1f) - 0.5f) * 0.12f
                            + (Mathf.PerlinNoise(x * 2.5f + 3.7f, z * 2.5f + 1.9f) - 0.5f) * 0.10f
                            + (Hash(i, j) - 0.5f) * 0.08f;
                Color c = stripeColor * (1 + noise);

                float coverage = Mathf.Clamp01((LineHalfWidth - DistanceToMarkings(x, z)) / metersPerPixel + 0.5f);
                c = Color.Lerp(c, line * (1 + noise * 0.3f), coverage);
                c.a = 1;
                pixels[j * PitchTexWidth + i] = c;
            }
        }

        var tex = new Texture2D(PitchTexWidth, PitchTexHeight, TextureFormat.RGB24, false);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(PitchTexturePath, tex.EncodeToJPG(92));
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(PitchTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PitchTexturePath);
        importer.maxTextureSize = 4096;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    static float DistanceToMarkings(float x, float z)
    {
        float d = float.MaxValue;
        float hx = HalfPitchWidth, hz = HalfPitchLength;

        d = Mathf.Min(d, Segment(x, z, -hx, -hz, -hx, hz));
        d = Mathf.Min(d, Segment(x, z, hx, -hz, hx, hz));
        d = Mathf.Min(d, Segment(x, z, -hx, -hz, hx, -hz));
        d = Mathf.Min(d, Segment(x, z, -hx, hz, hx, hz));
        d = Mathf.Min(d, Segment(x, z, -hx, 0, hx, 0));
        d = Mathf.Min(d, Ring(x, z, 0, 0, 9.15f));
        d = Mathf.Min(d, Spot(x, z, 0, 0, 0.15f));

        for (int s = -1; s <= 1; s += 2)
        {
            d = Mathf.Min(d, Box(x, z, s, 20.16f, 16.5f));
            d = Mathf.Min(d, Box(x, z, s, 9.16f, 5.5f));
            d = Mathf.Min(d, Spot(x, z, 0, s * (hz - 11f), 0.11f));
            if (s * z < hz - 16.5f) d = Mathf.Min(d, Ring(x, z, 0, s * (hz - 11f), 9.15f));

            for (int t = -1; t <= 1; t += 2)
                if (Mathf.Abs(x) <= hx && Mathf.Abs(z) <= hz)
                    d = Mathf.Min(d, Ring(x, z, t * hx, s * hz, 1f));
        }
        return d;
    }

    // Three sides of a penalty/goal area open towards the goal line at end s (+1 / -1).
    static float Box(float x, float z, int s, float halfWidth, float depth)
    {
        float zLine = s * HalfPitchLength, zFront = s * (HalfPitchLength - depth);
        float d = Segment(x, z, -halfWidth, zFront, halfWidth, zFront);
        d = Mathf.Min(d, Segment(x, z, -halfWidth, zLine, -halfWidth, zFront));
        return Mathf.Min(d, Segment(x, z, halfWidth, zLine, halfWidth, zFront));
    }

    static float Segment(float px, float pz, float ax, float az, float bx, float bz)
    {
        var p = new Vector2(px - ax, pz - az);
        var ab = new Vector2(bx - ax, bz - az);
        float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
        return (p - ab * t).magnitude;
    }

    static float Ring(float x, float z, float cx, float cz, float radius) =>
        Mathf.Abs(new Vector2(x - cx, z - cz).magnitude - radius);

    static float Spot(float x, float z, float cx, float cz, float radius) =>
        Mathf.Max(0, new Vector2(x - cx, z - cz).magnitude - radius + LineHalfWidth);

    static float Hash(int i, int j)
    {
        uint h = (uint)i * 374761393u + (uint)j * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    static void GenerateNetTexture()
    {
        const int size = 256, cells = 8, thickness = 3;
        const int cell = size / cells;
        var pixels = new Color32[size * size];
        for (int j = 0; j < size; j++)
        for (int i = 0; i < size; i++)
        {
            bool onCord = i % cell < thickness || j % cell < thickness;
            pixels[j * size + i] = onCord ? new Color32(245, 245, 245, 255) : new Color32(245, 245, 245, 0);
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(NetTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(NetTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(NetTexturePath);
        importer.alphaIsTransparency = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.5f;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- scene

    static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        RenderSettings.sun = sun;

        Camera mainCamera = CreateMainCamera();
        CreatePitch();
        CreateGoal(new Vector3(0, 0, HalfPitchLength), 0);
        CreateGoal(new Vector3(0, 0, -HalfPitchLength), 180);

        GrassSystem grassSystem = CreateGrassSystem();

        var demo = new GameObject("GrassDemo");
        var bootstrap = demo.AddComponent<GrassDemoBootstrap>();
        bootstrap.grassSystem = grassSystem;
        var capture = demo.AddComponent<DemoCapture>();
        capture.bootstrap = bootstrap;
        capture.shots = new[]
        {
            new DemoCapture.Shot { name = "penalty_box", position = mainCamera.transform.position, lookAt = new Vector3(0, 0.7f, HalfPitchLength) },
            new DemoCapture.Shot { name = "close_up", position = new Vector3(4.5f, 0.32f, 38.5f), lookAt = new Vector3(6.5f, 0f, 41.5f) },
            new DemoCapture.Shot { name = "six_yard_box", position = new Vector3(-11.2f, 0.75f, 44.2f), lookAt = new Vector3(-9.16f, 0f, 47f) },
            new DemoCapture.Shot { name = "overview", position = new Vector3(14f, 9f, 28f), lookAt = new Vector3(4f, 0f, 41f) },
        };

        // Blades are ~1 cm wide; without MSAA they shimmer badly.
        int currentLevel = QualitySettings.GetQualityLevel();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.antiAliasing = 4;
        }
        QualitySettings.SetQualityLevel(currentLevel, false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    static Camera CreateMainCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 600f;
        cam.fieldOfView = 60f;
        go.AddComponent<AudioListener>();
        go.AddComponent<DemoFlyCamera>();

        var pos = new Vector3(-2.2f, 1.65f, 44.5f);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0, 0.7f, HalfPitchLength) - pos));
        return cam;
    }

    static void CreatePitch()
    {
        // Unlit on purpose: the grass camera snapshots this plane and the blades inherit its colors,
        // so the 3D grass and the flat field stay identical where they meet.
        var mat = SaveMaterial(new Material(Shader.Find("Unlit/Texture"))
        {
            mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PitchTexturePath),
        }, "Pitch");

        var pitch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pitch.name = "Pitch";
        pitch.layer = FieldLayer;
        Object.DestroyImmediate(pitch.GetComponent<Collider>());
        pitch.transform.rotation = Quaternion.Euler(90, 0, 0);
        pitch.transform.localScale = new Vector3(PlaneWidth, PlaneLength, 1);
        var renderer = pitch.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CreateGoal(Vector3 goalLineCenter, float yaw)
    {
        var goal = new GameObject("Goal").transform;
        goal.SetPositionAndRotation(goalLineCenter, Quaternion.Euler(0, yaw, 0));

        var frame = SaveMaterial(new Material(Shader.Find("Standard")) { color = Color.white }, "GoalFrame");
        var support = SaveMaterial(new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.57f, 0.6f) }, "GoalSupport");

        float postX = GoalHalfWidth + PostDiameter / 2, barY = GoalHeight + PostDiameter / 2;
        Rod(goal, frame, new Vector3(-postX, 0, 0), new Vector3(-postX, barY + PostDiameter / 2, 0), PostDiameter);
        Rod(goal, frame, new Vector3(postX, 0, 0), new Vector3(postX, barY + PostDiameter / 2, 0), PostDiameter);
        Rod(goal, frame, new Vector3(-postX - PostDiameter / 2, barY, 0), new Vector3(postX + PostDiameter / 2, barY, 0), PostDiameter);

        const float rod = 0.04f;
        float top = GoalHeight, back = GoalDepth;
        foreach (float sx in new[] { -GoalHalfWidth, GoalHalfWidth })
        {
            Rod(goal, support, new Vector3(sx, top, 0), new Vector3(sx, top, back), rod);
            Rod(goal, support, new Vector3(sx, 0, back), new Vector3(sx, top, back), rod);
            Rod(goal, support, new Vector3(sx, 0.02f, 0), new Vector3(sx, 0.02f, back), rod);
        }
        Rod(goal, support, new Vector3(-GoalHalfWidth, top, back), new Vector3(GoalHalfWidth, top, back), rod);
        Rod(goal, support, new Vector3(-GoalHalfWidth, 0.02f, back), new Vector3(GoalHalfWidth, 0.02f, back), rod);

        float width = 2 * GoalHalfWidth;
        NetPanel(goal, "NetBack", new Vector3(0, top / 2, back), Quaternion.identity, new Vector2(width, top));
        NetPanel(goal, "NetTop", new Vector3(0, top, back / 2), Quaternion.Euler(90, 0, 0), new Vector2(width, back));
        NetPanel(goal, "NetLeft", new Vector3(-GoalHalfWidth, top / 2, back / 2), Quaternion.Euler(0, 90, 0), new Vector2(back, top));
        NetPanel(goal, "NetRight", new Vector3(GoalHalfWidth, top / 2, back / 2), Quaternion.Euler(0, 90, 0), new Vector2(back, top));
    }

    static void Rod(Transform parent, Material mat, Vector3 from, Vector3 to, float diameter)
    {
        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.name = "Bar";
        Object.DestroyImmediate(rod.GetComponent<Collider>());
        rod.transform.SetParent(parent, false);
        rod.transform.localPosition = (from + to) / 2;
        rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        rod.transform.localScale = new Vector3(diameter, (to - from).magnitude / 2, diameter);
        rod.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void NetPanel(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector2 size)
    {
        var mat = SaveMaterial(new Material(Shader.Find("Unlit/Transparent Cutout"))
        {
            mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(NetTexturePath),
            mainTextureScale = size / NetTileMeters,
        }, $"GoalNet_{size.x:0.##}x{size.y:0.##}");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(localPos, localRot);
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.AddComponent<MeshFilter>().sharedMesh = DoubleSidedQuad();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Mesh DoubleSidedQuad()
    {
        const string path = Root + "/Meshes/DoubleSidedQuad.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        mesh = new Mesh
        {
            name = "DoubleSidedQuad",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f),
            },
            uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1),
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1, 4, 5, 6, 6, 5, 7 },
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material SaveMaterial(Material mat, string name)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mat, existing);
            return existing;
        }
        mat.name = name;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Rebuilds the GrassSystem prefab layout from Goal Poacher: the system at the world origin,
    // a template blade (GrassOne + 3 vertex points) and the top-down orthographic grass camera.
    static GrassSystem CreateGrassSystem()
    {
        var root = new GameObject("GrassSystem");
        var grass = root.AddComponent<GrassSystem>();

        var grassOne = new GameObject("GrassOne").transform;
        grassOne.SetParent(root.transform, false);
        grassOne.localRotation = new Quaternion(0, 0.5583886f, 0, -0.82957953f);
        Transform Point(string name, Vector3 localPos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(grassOne, false);
            p.localPosition = localPos;
            return p;
        }

        var camGo = new GameObject("CameraGrass");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.SetLocalPositionAndRotation(new Vector3(0, 3, 0), Quaternion.Euler(90, 0, 0));
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.cullingMask = (1 << FieldLayer) | (1 << GrassCamLineLayer);
        cam.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(GrassRoot + "/GrassCameraRenderTexture.renderTexture");
        cam.allowMSAA = false;
        // Only rendered on demand through grassCamera.Render() when the grass patch moves.
        cam.enabled = false;

        grass.shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GrassRoot + "/ComputeGrass.compute");
        grass.material = AssetDatabase.LoadAssetAtPath<Material>(GrassRoot + "/Unlit_OneGrassUnlitShader.mat");
        grass.grassRenderTexture = cam.targetTexture;
        grass.grassCamera = cam;
        grass.grassFieldLength = 12;
        grass.noiseMultiplier = 0.07f;
        grass.noiseRotateMultiplier = 124124;
        grass.grassOne = grassOne;
        grass.point1 = Point("Point1", new Vector3(-0.005f, 0, 0));
        grass.point2 = Point("Point2", new Vector3(0.005f, 0, 0));
        grass.point3 = Point("Point3", new Vector3(0, 0.03f, 0));

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, GrassRoot + "/GrassSystem.prefab", InteractionMode.AutomatedAction);
        return grass;
    }
}

// This file is generated using AI. Please ignore it.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Generates the demo scene (line markings, goal, net, materials, scene, GrassSystem prefab). The pitch
// surface and the ball are Goal Poacher assets, copied into Assets/Demo/GoalPoacherPitch and GoalPoacherBall.
// Menu: Tools/Stadium Grass Demo. Batch mode: -executeMethod DemoSceneBuilder.BuildAll
public static class DemoSceneBuilder
{
    const string Root = "Assets/Demo";
    const string GrassRoot = "Assets/GrassRendering";
    const string ScenePath = Root + "/Scenes/StadiumGrassDemo.unity";
    // Pitch surface from Goal Poacher: its pitch mesh, grass shader and the grass texture of the Beach stadium.
    const string PitchMeshPath = Root + "/GoalPoacherPitch/MaiRemAllStadiums/OldStadiumAssets/GrassFieldCorrectedSize2.fbx";
    const string PitchGrassTexturePath = Root + "/GoalPoacherPitch/AllAssets/FootballStadium/Textures/chatgptgrassfixYellow.psd";
    const string PitchNoiseTexturePath = Root + "/Textures/PitchNoise.png";
    const string LineTexturePath = Root + "/Textures/PitchLine.png";
    const string LineMeshPath = Root + "/Meshes/PitchLines.asset";
    const string NetTexturePath = Root + "/Textures/GoalNet.png";
    const string BallModelPath = Root + "/GoalPoacherBall/BallRecolor_03.prefab"; // a ball skin from Goal Poacher
    const float BallDiameter = 0.22f;
    const string BlobShadowTexturePath = Root + "/Textures/BlobShadow.png";
    static Vector3 BallPosition => new Vector3(0, 0.11f, HalfPitchLength - 11f); // penalty spot

    // FIFA pitch (68 x 105), length along z.
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

        GenerateNetTexture();
        GenerateLineTexture();
        GeneratePitchNoiseTexture();
        GenerateBlobShadowTexture();
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

    // White paint across the strip (v), soft at the edges, with a little grain along it (u).
    static void GenerateLineTexture()
    {
        const int width = 64, height = 32;
        var pixels = new Color32[width * height];
        for (int j = 0; j < height; j++)
        for (int i = 0; i < width; i++)
        {
            float across = Mathf.Abs((j + 0.5f) / height - 0.5f) * 2; // 0 at the center, 1 at the edge
            float alpha = Mathf.SmoothStep(1, 0, Mathf.InverseLerp(0.6f, 1f, across)) * (0.9f + 0.1f * Hash(j, i));
            byte shade = (byte)(255 * (0.9f + 0.1f * Hash(i, j)));
            pixels[j * width + i] = new Color32(shade, shade, shade, (byte)(255 * alpha));
        }

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(LineTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(LineTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(LineTexturePath);
        importer.alphaIsTransparency = true;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
    }

    // Tileable grey value noise that breaks up the repetition of the grass texture over the pitch.
    // Tuned to mean 0.58 and spread 0.126, the levels the pitch materials were set up for.
    static void GeneratePitchNoiseTexture()
    {
        const int size = 512;
        var values = new float[size * size];
        double sum = 0, sumSq = 0;
        for (int j = 0; j < size; j++)
        for (int i = 0; i < size; i++)
        {
            float v = 0, amplitude = 1;
            for (int cells = 4; cells <= 64; cells *= 2, amplitude *= 0.55f)
                v += amplitude * TileableValueNoise(i * cells / (float)size, j * cells / (float)size, cells);
            values[j * size + i] = v;
            sum += v;
            sumSq += v * v;
        }
        float mean = (float)(sum / values.Length);
        float spread = Mathf.Sqrt((float)(sumSq / values.Length) - mean * mean);

        var pixels = new Color32[values.Length];
        for (int k = 0; k < values.Length; k++)
        {
            byte g = (byte)(255 * Mathf.Clamp01(0.58f + (values[k] - mean) / spread * 0.126f));
            pixels[k] = new Color32(g, g, g, 255);
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(PitchNoiseTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(PitchNoiseTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PitchNoiseTexturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    // Smoothly interpolated random values on a cells x cells lattice that wraps around.
    static float TileableValueNoise(float x, float y, int cells)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float Corner(int cx, int cy) => Hash(((cx % cells) + cells) % cells + cells * 7919, ((cy % cells) + cells) % cells);
        return Mathf.Lerp(
            Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx),
            Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx), fy);
    }

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

    static void GenerateBlobShadowTexture()
    {
        const int size = 128;
        var pixels = new Color32[size * size];
        for (int j = 0; j < size; j++)
        for (int i = 0; i < size; i++)
        {
            float r = new Vector2(i + 0.5f - size / 2f, j + 0.5f - size / 2f).magnitude / (size / 2f);
            float a = Mathf.SmoothStep(1, 0, r) * 0.75f;
            pixels[j * size + i] = new Color32(0, 0, 0, (byte)(a * 255));
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(BlobShadowTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(BlobShadowTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(BlobShadowTexturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
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

        CreatePitch();
        CreateBall();
        CreateMainCamera();
        CreateGoal(new Vector3(0, 0, HalfPitchLength), 0);
        CreateGoal(new Vector3(0, 0, -HalfPitchLength), 180);

        GrassSystem grassSystem = CreateGrassSystem();

        var demo = new GameObject("GrassDemo");
        var bootstrap = demo.AddComponent<GrassDemoBootstrap>();
        bootstrap.grassSystem = grassSystem;
        var penaltyBox = new GrassDemoBootstrap.Viewpoint { name = "penalty_box", position = FromGoalLine(-2.2f, 1.65f, 15f), lookAt = FromGoalLine(0, 0.7f, 0) };
        var ball = new GrassDemoBootstrap.Viewpoint { name = "ball", position = BallPosition + new Vector3(-0.9f, 0.35f, -1.1f), lookAt = BallPosition };
        var closeUp = new GrassDemoBootstrap.Viewpoint { name = "close_up", position = FromGoalLine(4.5f, 0.32f, 14f), lookAt = FromGoalLine(6.5f, 0f, 11f) };
        var sixYardBox = new GrassDemoBootstrap.Viewpoint { name = "six_yard_box", position = FromGoalLine(-11.2f, 0.75f, 8.3f), lookAt = FromGoalLine(-9.16f, 0f, 5.5f) };
        var penaltyArc = new GrassDemoBootstrap.Viewpoint { name = "penalty_arc", position = FromGoalLine(-5.5f, 1.1f, 23f), lookAt = FromGoalLine(0f, 0f, 17.5f) };
        var corner = new GrassDemoBootstrap.Viewpoint { name = "corner", position = FromGoalLine(HalfPitchWidth - 3.5f, 1.2f, 4f), lookAt = FromGoalLine(HalfPitchWidth, 0f, 0f) };

        // The demo loops through the close views; the README screenshots add a few wider ones.
        bootstrap.viewpoints = new[] { ball, closeUp, sixYardBox };
        var capture = demo.AddComponent<DemoCapture>();
        capture.bootstrap = bootstrap;
        capture.shots = new[] { ball, closeUp, sixYardBox, penaltyBox, penaltyArc, corner };

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

    // A point in front of the +z goal line, distance in meters towards the center spot.
    static Vector3 FromGoalLine(float x, float y, float distance) => new Vector3(x, y, HalfPitchLength - distance);

    static void CreateMainCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 600f;
        cam.fieldOfView = 60f;
        // GrassCamLine objects (grass cut-off edges, thinner lines) are only for the grass snapshot camera.
        cam.cullingMask = ~(1 << GrassCamLineLayer);
        go.AddComponent<AudioListener>();
    }

    static void CreateBall()
    {
        var ball = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BallModelPath));
        ball.name = "Ball";
        ball.transform.SetPositionAndRotation(BallPosition, Quaternion.Euler(20, 35, 10));
        var bounds = ball.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        float meshDiameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * ball.GetComponentInChildren<MeshFilter>().transform.lossyScale.x;
        ball.transform.localScale = ball.transform.localScale * (BallDiameter / meshDiameter);
        // Re-center so the ball rests on the grass regardless of the model's pivot.
        ball.transform.position += BallPosition - ball.GetComponentInChildren<Renderer>().bounds.center;

        // Contact shadow on the Field layer: the grass camera snapshots it too, so the blades
        // under the ball come out darker, not just the flat plane.
        var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "BallBlobShadow";
        shadow.layer = FieldLayer;
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        shadow.transform.SetPositionAndRotation(new Vector3(BallPosition.x, 0.002f, BallPosition.z), Quaternion.Euler(90, 0, 0));
        shadow.transform.localScale = Vector3.one * 0.38f;
        var shadowRenderer = shadow.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = SaveMaterial(new Material(Shader.Find("Unlit/Transparent"))
        {
            mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BlobShadowTexturePath),
        }, "BallBlobShadow");
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CreatePitch()
    {
        var pitch = new GameObject("Pitch");

        var surface = new GameObject("PitchSurface").transform;
        surface.SetParent(pitch.transform, false);
        // The pitch itself and a larger surround drawn just below it, used as in the game: the meshes
        // straight from the FBX, without its node transforms.
        // Same settings as the Beach stadium materials in the game.
        Material field = PitchMaterial("PitchField", 2f, new Color(1, 1, 1, 0), new Color(0.868f, 0.868f, 0.868f, 0));
        Material outer = PitchMaterial("PitchFieldOuter", 2.5f, new Color(0.914f, 0.925f, 1, 1), new Color(0.566f, 0.566f, 0.566f, 0));
        var parts = new List<Renderer>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PitchMeshPath))
        {
            if (!(asset is Mesh mesh)) continue;
            bool isOuter = mesh.name.Contains("Outer");
            var part = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.layer = FieldLayer;
            part.transform.SetParent(surface, false);
            part.transform.localPosition = isOuter ? Vector3.down * 0.01f : Vector3.zero;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = part.GetComponent<MeshRenderer>();
            r.sharedMaterial = isOuter ? outer : field;
            r.shadowCastingMode = ShadowCastingMode.Off;
            parts.Add(r);
            Debug.Log($"[DemoSceneBuilder] pitch surface part {mesh.name}: {r.bounds.size}");
        }
        // The game lays the pitch out along x; the demo has its length along z.
        if (SurfaceBounds(parts).size.x > SurfaceBounds(parts).size.z)
            surface.rotation = Quaternion.Euler(0, 90, 0);

        // Black strips around the surface on the GrassCamLine layer: only the grass camera sees them,
        // and blades over black texels are collapsed, so the 3D grass stops at the edge of the surface.
        var cutoff = SaveMaterial(new Material(Shader.Find("Unlit/Color")) { color = Color.black }, "GrassCutoff");
        Bounds b = SurfaceBounds(parts);
        Vector3 c = b.center, e = b.extents;
        CreateCutoffStrip(pitch.transform, new Vector3(c.x, 0, c.z + e.z + 1), new Vector2(2 * e.x + 4, 2), cutoff);
        CreateCutoffStrip(pitch.transform, new Vector3(c.x, 0, c.z - e.z - 1), new Vector2(2 * e.x + 4, 2), cutoff);
        CreateCutoffStrip(pitch.transform, new Vector3(c.x + e.x + 1, 0, c.z), new Vector2(2, 2 * e.z + 4), cutoff);
        CreateCutoffStrip(pitch.transform, new Vector3(c.x - e.x - 1, 0, c.z), new Vector2(2, 2 * e.z + 4), cutoff);

        CreateLineMarkings(pitch.transform);
    }

    static Material PitchMaterial(string name, float brightness, Color tint, Color noiseColor)
    {
        var mat = new Material(Shader.Find("AmplifyGrassShader"));
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(PitchGrassTexturePath));
        mat.SetTextureScale("_MainTex", new Vector2(30, 60));
        mat.SetTexture("_NoiseWhite", AssetDatabase.LoadAssetAtPath<Texture2D>(PitchNoiseTexturePath));
        mat.SetTextureScale("_NoiseWhite", new Vector2(3, 3));
        mat.SetTextureOffset("_NoiseWhite", new Vector2(1.22f, 0));
        mat.SetFloat("_Brightness", brightness);
        mat.SetColor("_TintColor", tint);
        mat.SetColor("_NoiseColor", noiseColor);
        return SaveMaterial(mat, name);
    }

    static Bounds SurfaceBounds(List<Renderer> parts)
    {
        Bounds bounds = parts[0].bounds;
        foreach (var r in parts) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static void CreateCutoffStrip(Transform parent, Vector3 center, Vector2 size, Material mat)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
        strip.name = "GrassCutoffEdge";
        strip.layer = GrassCamLineLayer;
        Object.DestroyImmediate(strip.GetComponent<Collider>());
        strip.transform.SetParent(parent, false);
        strip.transform.SetPositionAndRotation(center + Vector3.up * 0.002f, Quaternion.Euler(90, 0, 0));
        strip.transform.localScale = new Vector3(size.x, size.y, 1);
        var r = strip.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- line markings

    // FIFA markings as flat strips. They sit on the Field layer, so the grass camera snapshots them too
    // and the blades standing on a line turn white.
    static void CreateLineMarkings(Transform parent)
    {
        var lines = new LineMeshBuilder(LineHalfWidth * 1.3f);
        float hx = HalfPitchWidth, hz = HalfPitchLength;

        lines.Segment(new Vector2(-hx, -hz), new Vector2(-hx, hz));
        lines.Segment(new Vector2(hx, -hz), new Vector2(hx, hz));
        lines.Segment(new Vector2(-hx, 0), new Vector2(hx, 0));
        lines.Arc(Vector2.zero, 9.15f, 0, 360);
        lines.Spot(Vector2.zero, 0.15f);

        for (int s = -1; s <= 1; s += 2)
        {
            float goalLine = s * hz;
            lines.Segment(new Vector2(-hx, goalLine), new Vector2(hx, goalLine));
            Box(lines, s, 20.16f, 16.5f);
            Box(lines, s, 9.16f, 5.5f);

            var spot = new Vector2(0, s * (hz - 11f));
            lines.Spot(spot, 0.11f);
            // Penalty arc: the part of the 9.15 m circle around the spot that lies outside the box.
            float halfAngle = Mathf.Acos((16.5f - 11f) / 9.15f) * Mathf.Rad2Deg;
            float towardsCenter = -90f * s;
            lines.Arc(spot, 9.15f, towardsCenter - halfAngle, towardsCenter + halfAngle);

            // Corner arcs: a quarter circle of 1 m, inside the pitch.
            for (int t = -1; t <= 1; t += 2)
            {
                float alongGoalLine = t > 0 ? 180 : 0;
                float alongTouchline = s > 0 ? -90 : 90;
                lines.Arc(new Vector2(t * hx, goalLine), 1f, alongGoalLine, alongGoalLine + Mathf.DeltaAngle(alongGoalLine, alongTouchline));
            }
        }

        Mesh mesh = lines.Build("PitchLines");
        AssetDatabase.DeleteAsset(LineMeshPath);
        AssetDatabase.CreateAsset(mesh, LineMeshPath);

        var go = new GameObject("LineMarkings", typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = FieldLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.006f;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = SaveMaterial(new Material(Shader.Find("Stadium Grass Demo/Pitch Line"))
        {
            mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LineTexturePath),
            mainTextureScale = new Vector2(2, 1),
        }, "PitchLine");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Three sides of a penalty/goal area, open towards the goal line at end s (+1 / -1).
    static void Box(LineMeshBuilder lines, int s, float halfWidth, float depth)
    {
        float goalLine = s * HalfPitchLength, front = s * (HalfPitchLength - depth);
        lines.Segment(new Vector2(-halfWidth, front), new Vector2(halfWidth, front));
        lines.Segment(new Vector2(-halfWidth, goalLine), new Vector2(-halfWidth, front));
        lines.Segment(new Vector2(halfWidth, goalLine), new Vector2(halfWidth, front));
    }

    // Builds flat strips in the xz plane. u runs along a strip in meters, v across it (0..1).
    class LineMeshBuilder
    {
        readonly float halfWidth;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public LineMeshBuilder(float halfWidth) => this.halfWidth = halfWidth;

        public void Segment(Vector2 a, Vector2 b)
        {
            // Extended by half a width so that lines meeting at a corner close it.
            Vector2 extend = (b - a).normalized * halfWidth;
            Strip(new[] { a - extend, b + extend });
        }

        // Angles in degrees, measured from +x towards +z.
        public void Arc(Vector2 center, float radius, float fromDegrees, float toDegrees)
        {
            int steps = Mathf.Max(4, Mathf.CeilToInt(radius * Mathf.Abs(toDegrees - fromDegrees) * Mathf.Deg2Rad / 0.25f));
            var points = new Vector2[steps + 1];
            for (int k = 0; k <= steps; k++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, k / (float)steps) * Mathf.Deg2Rad;
                points[k] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            Strip(points);
        }

        // A disc: v = 0.5 (solid paint) at the center, v = 0 (faded) on the rim.
        public void Spot(Vector2 center, float radius)
        {
            const int steps = 24;
            int first = vertices.Count;
            vertices.Add(new Vector3(center.x, 0, center.y));
            uvs.Add(new Vector2(0, 0.5f));
            radius += halfWidth;
            for (int k = 0; k <= steps; k++)
            {
                float a = k * 2 * Mathf.PI / steps;
                vertices.Add(new Vector3(center.x + Mathf.Cos(a) * radius, 0, center.y + Mathf.Sin(a) * radius));
                uvs.Add(new Vector2(k / (float)steps, 0));
            }
            for (int k = 0; k < steps; k++)
                triangles.AddRange(new[] { first, first + k + 2, first + k + 1 });
        }

        void Strip(IList<Vector2> points)
        {
            int first = vertices.Count;
            float u = 0;
            for (int k = 0; k < points.Count; k++)
            {
                Vector2 tangent = (points[Mathf.Min(k + 1, points.Count - 1)] - points[Mathf.Max(k - 1, 0)]).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x) * halfWidth;
                if (k > 0) u += Vector2.Distance(points[k - 1], points[k]);
                vertices.Add(new Vector3(points[k].x - side.x, 0, points[k].y - side.y));
                vertices.Add(new Vector3(points[k].x + side.x, 0, points[k].y + side.y));
                uvs.Add(new Vector2(u, 0));
                uvs.Add(new Vector2(u, 1));
            }
            for (int k = 0; k < points.Count - 1; k++)
            {
                int i = first + 2 * k;
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 2, i + 1, i + 3 });
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
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
            existing.name = name;
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

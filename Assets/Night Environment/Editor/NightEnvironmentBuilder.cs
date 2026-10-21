using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Tạo nền đêm cho scene đang mở: bầu trời sao, sao băng, dãy núi xa có rừng thông,
// mặt đất đồi và tảng đá dưới chân nhân vật.
// Chạy lại menu sẽ xoá và tạo lại "Night Environment". Material đã có sẽ được giữ nguyên
// (xoá file .mat trong Generated nếu muốn reset về mặc định).
public static class NightEnvironmentBuilder
{
    const string RootName = "Night Environment";
    const string RootFolder = "Assets/Night Environment";
    const string GeneratedFolder = RootFolder + "/Generated";
    const int Seed = 1337;

    const float StarDistance = 800f;
    const float GroundRadius = 175f;

    static readonly Vector3 MoonDirection = new Vector3(0.441f, 0.469f, 0.765f).normalized;
    static readonly Vector3 MilkyWayNormal = new Vector3(-0.791f, 0.361f, -0.493f).normalized;

    // Vị trí mặc định trong SampleScene nếu không tìm thấy MenuCharacterReaction
    static readonly Vector3 DefaultHome = new Vector3(6.6f, -0.027f, -7.16f);
    static readonly Vector3 DefaultQuit = new Vector3(8.48f, -0.027f, -8.76f);

    static System.Random rng;
    static Vector2 groundNoiseOffset;
    static Vector2 rockCenterXZ;
    static float hillTopY;

    class RidgeLayer
    {
        public string name;
        public float radius;
        public float minElevation;
        public float maxElevation;
        public float frequency;
        public Vector2 offset;
        public Color topColor;
        public Color bottomColor;
        public Color hazeColor;
        public float hazeStrength;
        public int treeCount;
        public Vector2 treeHeight;
    }

    [MenuItem("Tools/Night Environment/Build Night Scene")]
    static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Hãy thoát Play Mode trước khi tạo nền đêm.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Camera cam = FindMainCamera(scene);
        if (cam == null)
        {
            Debug.LogError("Không tìm thấy Main Camera trong scene.");
            return;
        }

        rng = new System.Random(Seed);
        groundNoiseOffset = new Vector2(Range(0f, 500f), Range(0f, 500f));
        EnsureFolders();

        ReadCharacterSpots(scene, out Vector3 home, out Vector3 quit);

        GameObject old = FindRoot(scene, RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Night Environment");

        Vector3 eye = cam.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        Material skyMaterial = BuildSky(root.transform, cam, eye);
        BuildMountains(root.transform, eye);
        BuildGroundAndRock(root.transform, home, quit, eye, forward);
        BuildLights(scene, root.transform, home, eye);
        SetupRenderSettings(skyMaterial);
        SetupCamera(cam);
        SetupPostProcessing(scene, cam, root.transform);
        HideOldPlane(scene);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        Debug.Log("Đã tạo nền đêm. Nhớ lưu scene (Ctrl+S).");
    }

    // ---------------------------------------------------------------- Bầu trời

    static Material BuildSky(Transform root, Camera cam, Vector3 eye)
    {
        Material skyMaterial = GetOrCreateMaterial("Night Sky", "Night/Skybox", m =>
        {
            m.SetVector("_MoonDirection", MoonDirection);
            m.SetVector("_BandNormal", MilkyWayNormal);
        });

        Material starMaterial = GetOrCreateMaterial("Night Stars", "Night/Stars", null);
        Material trailMaterial = GetOrCreateMaterial("Shooting Star", "Night/Glow Line", m =>
        {
            m.SetColor("_Color", new Color(0.8f, 0.9f, 1f, 1f));
            m.SetFloat("_Intensity", 5f);
        });

        GameObject sky = new GameObject("Sky");
        sky.transform.SetParent(root, false);

        GameObject stars = CreateRenderer(sky.transform, "Star Field", SaveMesh(BuildStarMesh()),
            new[] { starMaterial }, false, false);
        stars.transform.position = eye;

        GameObject shooting = new GameObject("Shooting Stars");
        shooting.transform.SetParent(sky.transform, false);
        ShootingStars spawner = shooting.AddComponent<ShootingStars>();

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("targetCamera").objectReferenceValue = cam;
        so.FindProperty("trailMaterial").objectReferenceValue = trailMaterial;
        so.ApplyModifiedPropertiesWithoutUndo();

        return skyMaterial;
    }

    static Mesh BuildStarMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<Vector2> corners = new List<Vector2>();
        List<Vector4> data = new List<Vector4>();
        List<int> triangles = new List<int>();

        Vector2[] quad = { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) };

        void AddStar(Vector3 dir, float brightness, float size, float twinkleAmount)
        {
            // Không đặt sao đè lên mặt trăng
            if (Vector3.Angle(dir, MoonDirection) < 4f) return;

            float tintRoll = Rand();
            Color tint = tintRoll < 0.15f ? new Color(1f, 0.85f, 0.7f)
                       : tintRoll < 0.4f ? new Color(0.75f, 0.85f, 1f)
                       : new Color(0.95f, 0.97f, 1f);
            tint = ToVertexColor(tint);
            tint.a = brightness;

            Vector4 twinkle = new Vector4(Range(0f, Mathf.PI * 2f), Range(1.2f, 4f), twinkleAmount, size);
            int start = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(dir * StarDistance);
                colors.Add(tint);
                corners.Add(quad[i]);
                data.Add(twinkle);
            }

            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        // Sao rải đều trên bán cầu trên
        for (int i = 0; i < 2200; i++)
        {
            float y = Mathf.Lerp(-0.05f, 1f, Rand());
            float phi = Rand() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - y * y);
            Vector3 dir = new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi));

            float b = Mathf.Pow(Rand(), 4f);
            bool blinking = Rand() < 0.25f;
            AddStar(dir,
                Mathf.Lerp(0.12f, 1f, b),
                Mathf.Lerp(0.6f, 2f, b) * Range(0.85f, 1.15f),
                blinking ? Range(0.7f, 0.95f) : Range(0.15f, 0.45f));
        }

        // Sao mờ dày đặc dọc theo dải Ngân Hà
        Vector3 u = Vector3.Cross(MilkyWayNormal, Vector3.up).normalized;
        Vector3 v = Vector3.Cross(MilkyWayNormal, u);
        for (int i = 0; i < 1000; i++)
        {
            float a = Rand() * Mathf.PI * 2f;
            Vector3 dir = (Mathf.Cos(a) * u + Mathf.Sin(a) * v + MilkyWayNormal * Gaussian() * 0.1f).normalized;
            if (dir.y < -0.05f) continue;

            AddStar(dir, Range(0.08f, 0.3f), Range(0.5f, 0.8f), Range(0.2f, 0.6f));
        }

        Mesh mesh = new Mesh { name = "Night Stars", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, data);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Núi xa

    static void BuildMountains(Transform root, Vector3 eye)
    {
        RidgeLayer[] layers =
        {
            new RidgeLayer
            {
                name = "Mountains Far", radius = 420f, minElevation = 1f, maxElevation = 15f, frequency = 1.6f,
                topColor = new Color(0.2f, 0.22f, 0.38f), bottomColor = new Color(0.1f, 0.12f, 0.25f),
                hazeColor = new Color(0.17f, 0.16f, 0.33f), hazeStrength = 0.6f,
                treeCount = 0,
            },
            new RidgeLayer
            {
                name = "Mountains Mid", radius = 280f, minElevation = 0f, maxElevation = 9f, frequency = 2.2f,
                topColor = new Color(0.075f, 0.09f, 0.19f), bottomColor = new Color(0.05f, 0.06f, 0.14f),
                hazeColor = new Color(0.12f, 0.12f, 0.26f), hazeStrength = 0.55f,
                treeCount = 1100, treeHeight = new Vector2(3f, 5f),
            },
            new RidgeLayer
            {
                name = "Mountains Near", radius = 180f, minElevation = -0.5f, maxElevation = 5f, frequency = 3f,
                topColor = new Color(0.025f, 0.035f, 0.07f), bottomColor = new Color(0.03f, 0.04f, 0.09f),
                hazeColor = new Color(0.08f, 0.08f, 0.18f), hazeStrength = 0.4f,
                treeCount = 900, treeHeight = new Vector2(2.5f, 4.5f),
            },
        };

        GameObject parent = new GameObject("Mountains");
        parent.transform.SetParent(root, false);

        foreach (RidgeLayer layer in layers)
        {
            layer.offset = new Vector2(Range(0f, 300f), Range(0f, 300f));

            float bottomY = eye.y + layer.radius * Mathf.Tan((layer.minElevation - 2f) * Mathf.Deg2Rad);
            float topY = eye.y + layer.radius * Mathf.Tan(layer.maxElevation * Mathf.Deg2Rad);

            Material material = GetOrCreateMaterial(layer.name, "Night/Silhouette", m =>
            {
                m.SetColor("_TopColor", layer.topColor);
                m.SetColor("_BottomColor", layer.bottomColor);
                m.SetColor("_HazeColor", layer.hazeColor);
                m.SetFloat("_TopHeight", topY);
                m.SetFloat("_BottomHeight", bottomY);
                m.SetFloat("_HazeHeight", (topY - bottomY) * 0.45f);
                m.SetFloat("_HazeStrength", layer.hazeStrength);
            });

            MeshBuilder builder = new MeshBuilder(1);
            AddRidgeRing(builder, layer, eye);
            AddRidgeTrees(builder, layer, eye);

            CreateRenderer(parent.transform, layer.name, SaveMesh(builder.ToMesh(layer.name)),
                new[] { material }, false, false);
        }
    }

    static float RidgeElevation(RidgeLayer layer, float theta)
    {
        // Lấy noise trên vòng tròn để dãy núi khép kín không bị nối
        float c = Mathf.Cos(theta);
        float s = Mathf.Sin(theta);
        float f = layer.frequency;
        Vector2 o = layer.offset;

        float broad = Mathf.PerlinNoise(o.x + c * f, o.y + s * f);
        float peaks = Mathf.PerlinNoise(o.x + 40f + c * f * 3f, o.y + 40f + s * f * 3f);
        peaks = 1f - Mathf.Abs(peaks * 2f - 1f);
        float detail = Mathf.PerlinNoise(o.x + 90f + c * f * 12f, o.y + 90f + s * f * 12f);
        float jagged = Mathf.PerlinNoise(o.x + 150f + c * f * 40f, o.y + 150f + s * f * 40f);

        float n = Mathf.Clamp01(broad * 0.55f + peaks * peaks * 0.35f + detail * 0.12f + jagged * 0.04f);
        return Mathf.Lerp(layer.minElevation, layer.maxElevation, n);
    }

    static float RidgeHeight(RidgeLayer layer, float theta, Vector3 eye)
    {
        return eye.y + layer.radius * Mathf.Tan(RidgeElevation(layer, theta) * Mathf.Deg2Rad);
    }

    static void AddRidgeRing(MeshBuilder builder, RidgeLayer layer, Vector3 eye)
    {
        const int segments = 1024;
        const float baseY = -80f;
        int previousBase = -1;
        int previousTop = -1;

        for (int k = 0; k <= segments; k++)
        {
            float theta = Mathf.PI * 2f * (k % segments) / segments;
            Vector3 radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * layer.radius;

            int baseIndex = builder.AddVertex(new Vector3(eye.x + radial.x, baseY, eye.z + radial.z));
            int topIndex = builder.AddVertex(new Vector3(eye.x + radial.x, RidgeHeight(layer, theta, eye), eye.z + radial.z));

            if (k > 0)
            {
                builder.AddTriangle(previousBase, previousTop, baseIndex);
                builder.AddTriangle(previousTop, topIndex, baseIndex);
            }

            previousBase = baseIndex;
            previousTop = topIndex;
        }
    }

    static void AddRidgeTrees(MeshBuilder builder, RidgeLayer layer, Vector3 eye)
    {
        for (int i = 0; i < layer.treeCount; i++)
        {
            float theta = Rand() * Mathf.PI * 2f;
            float height = Range(layer.treeHeight.x, layer.treeHeight.y);
            float ridgeY = RidgeHeight(layer, theta, eye);

            Vector3 radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            Vector3 tangent = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            Vector3 basePos = new Vector3(eye.x, ridgeY - height * Range(0.15f, 0.45f), eye.z)
                            + radial * (layer.radius - 0.5f);

            // Thân cây
            float trunkW = height * 0.05f;
            float trunkH = height * 0.25f;
            int t0 = builder.AddVertex(basePos - tangent * trunkW);
            int t1 = builder.AddVertex(basePos - tangent * trunkW + Vector3.up * trunkH);
            int t2 = builder.AddVertex(basePos + tangent * trunkW + Vector3.up * trunkH);
            int t3 = builder.AddVertex(basePos + tangent * trunkW);
            builder.AddTriangle(t0, t1, t2);
            builder.AddTriangle(t0, t2, t3);

            // 3 tầng lá hình tam giác
            for (int tier = 0; tier < 3; tier++)
            {
                float y0 = height * (0.2f + tier * 0.22f);
                float y1 = height * (0.6f + tier * 0.2f);
                float w = height * (0.32f - tier * 0.075f);

                int a = builder.AddVertex(basePos - tangent * w + Vector3.up * y0);
                int b = builder.AddVertex(basePos + Vector3.up * y1);
                int c = builder.AddVertex(basePos + tangent * w + Vector3.up * y0);
                builder.AddTriangle(a, b, c);
            }
        }
    }

    // ---------------------------------------------------------------- Mặt đất, tảng đá, rừng

    static void BuildGroundAndRock(Transform root, Vector3 home, Vector3 quit, Vector3 eye, Vector3 forward)
    {
        // Tảng đá dài theo đường nhân vật đi từ vị trí đứng tới Quit Spot
        Vector3 center = (home + quit) * 0.5f;
        Vector3 line = quit - home;
        line.y = 0f;
        float halfLength = line.magnitude * 0.5f;
        float yaw = line.sqrMagnitude > 0.0001f ? -Mathf.Atan2(line.z, line.x) * Mathf.Rad2Deg : 0f;

        float rockTopY = home.y;
        Vector3 rockRadii = new Vector3(halfLength + 1.3f, 1.1f, 1.5f);
        Vector3 rockCenter = new Vector3(center.x, rockTopY - 0.45f, center.z);

        rockCenterXZ = new Vector2(center.x, center.z);
        hillTopY = rockTopY - 0.52f;

        Material groundMaterial = GetOrCreateMaterial("Night Ground", "Universal Render Pipeline/Lit",
            Lit(new Color(0.07f, 0.1f, 0.09f), 0.05f));
        Material rockMaterial = GetOrCreateMaterial("Night Rock", "Universal Render Pipeline/Lit",
            Lit(new Color(0.22f, 0.23f, 0.26f), 0.15f));
        Material trunkMaterial = GetOrCreateMaterial("Pine Trunk", "Universal Render Pipeline/Lit",
            Lit(new Color(0.12f, 0.08f, 0.06f), 0.05f));
        Material leavesMaterial = GetOrCreateMaterial("Pine Leaves", "Universal Render Pipeline/Lit",
            Lit(new Color(0.05f, 0.11f, 0.08f), 0.05f));

        // Mặt đất
        CreateRenderer(root, "Ground", SaveMesh(BuildGroundMesh()), new[] { groundMaterial }, false, true);

        // Tảng đá chính (mặt trên phẳng đúng độ cao chân nhân vật)
        MeshBuilder rock = new MeshBuilder(1);
        AddRock(rock, rockCenter, rockRadii, yaw, rockTopY, 0.12f, new Vector2(Range(0f, 100f), Range(0f, 100f)), 12, 22);
        GameObject rockObject = CreateRenderer(root, "Rock", SaveMesh(rock.ToMesh("Night Rock")),
            new[] { rockMaterial }, true, true);
        rockObject.AddComponent<MeshCollider>();

        // Đá nhỏ xung quanh
        MeshBuilder pebbles = new MeshBuilder(1);
        for (int i = 0, placed = 0; i < 60 && placed < 9; i++)
        {
            float angle = Rand() * Mathf.PI * 2f;
            float dist = Range(3.2f, 10f);
            float x = center.x + Mathf.Cos(angle) * dist;
            float z = center.z + Mathf.Sin(angle) * dist;
            if (new Vector2(x - eye.x, z - eye.z).magnitude < 2f) continue;

            float size = Range(0.25f, 0.75f);
            Vector3 radii = new Vector3(size * Range(1f, 1.6f), size * Range(0.5f, 0.9f), size);
            Vector3 pos = new Vector3(x, GroundHeight(x, z) - radii.y * 0.3f, z);
            AddRock(pebbles, pos, radii, Range(0f, 360f), float.MaxValue, 0.18f,
                new Vector2(Range(0f, 100f), Range(0f, 100f)), 7, 10);
            placed++;
        }
        CreateRenderer(root, "Small Rocks", SaveMesh(pebbles.ToMesh("Night Small Rocks")),
            new[] { rockMaterial }, true, true);

        // Rừng thông trên sườn đồi
        MeshBuilder forest = new MeshBuilder(2);
        Vector2 clusterOffset = new Vector2(Range(0f, 300f), Range(0f, 300f));

        for (int attempt = 0, placed = 0; attempt < 5000 && placed < 650; attempt++)
        {
            float r = Mathf.Lerp(7f, 150f, Mathf.Pow(Rand(), 0.8f));
            float a = Rand() * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(a) * r;
            float z = center.z + Mathf.Sin(a) * r;

            // Cây mọc thành cụm, chừa khoảng trống
            if (Mathf.PerlinNoise(x * 0.03f + clusterOffset.x, z * 0.03f + clusterOffset.y) < 0.42f) continue;

            float height = Range(4.5f, 9f) * Mathf.Lerp(0.9f, 1.25f, r / 150f);
            Vector3 basePos = new Vector3(x, GroundHeight(x, z) - 0.2f, z);

            Vector3 toTree = basePos - eye;
            toTree.y = 0f;
            float flatDistance = toTree.magnitude;
            if (flatDistance < 7f) continue;

            // Giữ tầm nhìn chính thoáng: cây phía trước phải thấp hơn đường chân trời
            float azimuth = Vector3.SignedAngle(forward, toTree, Vector3.up);
            float topElevation = Mathf.Atan2(basePos.y + height - eye.y, flatDistance) * Mathf.Rad2Deg;
            if (Mathf.Abs(azimuth) < 30f && topElevation > -2f) continue;

            AddPine(forest, basePos, height, Range(0f, 360f));
            placed++;
        }

        CreateRenderer(root, "Forest", SaveMesh(forest.ToMesh("Night Forest")),
            new[] { trunkMaterial, leavesMaterial }, true, true);
    }

    static float GroundHeight(float x, float z)
    {
        float r = Vector2.Distance(new Vector2(x, z), rockCenterXZ);

        // Đỉnh đồi phẳng quanh tảng đá, dốc xuống thung lũng, rồi dâng lên chân núi
        float h = hillTopY - 7.5f * SmoothStep(3.5f, 50f, r) + 6f * SmoothStep(110f, 180f, r);

        float n = Mathf.PerlinNoise(x * 0.035f + groundNoiseOffset.x, z * 0.035f + groundNoiseOffset.y) - 0.5f;
        n += 0.5f * (Mathf.PerlinNoise(x * 0.09f + groundNoiseOffset.y, z * 0.09f + groundNoiseOffset.x) - 0.5f);
        return h + n * 3.5f * SmoothStep(5f, 20f, r);
    }

    static Mesh BuildGroundMesh()
    {
        const int rings = 70;
        const int segments = 128;
        MeshBuilder builder = new MeshBuilder(1);

        int center = builder.AddVertex(new Vector3(rockCenterXZ.x, GroundHeight(rockCenterXZ.x, rockCenterXZ.y), rockCenterXZ.y));

        // Vòng dày ở gần, thưa ở xa
        for (int k = 1; k <= rings; k++)
        {
            float t = k / (float)rings;
            float r = GroundRadius * t * t;
            for (int s = 0; s < segments; s++)
            {
                float theta = Mathf.PI * 2f * s / segments;
                float x = rockCenterXZ.x + Mathf.Cos(theta) * r;
                float z = rockCenterXZ.y + Mathf.Sin(theta) * r;
                builder.AddVertex(new Vector3(x, GroundHeight(x, z), z));
            }
        }

        int Index(int ring, int segment) => 1 + (ring - 1) * segments + (segment % segments);

        for (int s = 0; s < segments; s++)
            builder.AddTriangle(center, Index(1, s + 1), Index(1, s));

        for (int k = 1; k < rings; k++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = Index(k, s);
                int i1 = Index(k, s + 1);
                int o0 = Index(k + 1, s);
                int o1 = Index(k + 1, s + 1);
                builder.AddTriangle(i0, i1, o0);
                builder.AddTriangle(i1, o1, o0);
            }
        }

        return builder.ToMesh("Night Ground");
    }

    static void AddRock(MeshBuilder builder, Vector3 center, Vector3 radii, float yaw, float topY,
        float jag, Vector2 noiseOffset, int rings, int segments)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 Vertex(int j, int i)
        {
            float theta = Mathf.PI * j / rings;
            float phi = Mathf.PI * 2f * (i % segments) / segments;
            Vector3 dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));

            float n = Mathf.PerlinNoise(dir.x * 1.6f + noiseOffset.x, dir.y * 1.6f + dir.z * 0.7f + noiseOffset.y)
                    + Mathf.PerlinNoise(dir.z * 3.1f + noiseOffset.y, dir.x * 3.1f - dir.y + noiseOffset.x) * 0.5f;
            n = (n / 1.5f - 0.5f) * 2f;

            Vector3 p = center + rotation * (Vector3.Scale(dir, radii) * (1f + jag * n));
            if (p.y > topY) p.y = topY;
            return p;
        }

        for (int j = 0; j < rings; j++)
        {
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = Vertex(j, i);
                Vector3 b = Vertex(j, i + 1);
                Vector3 c = Vertex(j + 1, i);
                Vector3 d = Vertex(j + 1, i + 1);
                builder.AddFlatTriangle(a, b, c, 0);
                builder.AddFlatTriangle(b, d, c, 0);
            }
        }
    }

    static void AddPine(MeshBuilder builder, Vector3 basePos, float height, float yaw)
    {
        const int sides = 7;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 Ring(int k, float radius, float y)
        {
            float a = Mathf.PI * 2f * (k % sides) / sides;
            return basePos + Vector3.up * y + rotation * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        // Thân cây (submesh 0)
        float trunkRadius = height * 0.045f;
        float trunkHeight = height * 0.22f;
        for (int k = 0; k < sides; k++)
        {
            Vector3 b0 = Ring(k, trunkRadius, 0f);
            Vector3 b1 = Ring(k + 1, trunkRadius, 0f);
            Vector3 t0 = Ring(k, trunkRadius, trunkHeight);
            Vector3 t1 = Ring(k + 1, trunkRadius, trunkHeight);
            builder.AddFlatTriangle(t0, t1, b0, 0);
            builder.AddFlatTriangle(t1, b1, b0, 0);
        }

        // 3 tầng lá hình nón (submesh 1)
        for (int tier = 0; tier < 3; tier++)
        {
            float y0 = height * (0.17f + tier * 0.22f);
            float y1 = tier == 2 ? height : y0 + height * (0.45f - tier * 0.06f);
            float radius = height * (0.27f - tier * 0.065f);
            Vector3 apex = basePos + Vector3.up * y1;
            Vector3 bottom = basePos + Vector3.up * y0;

            for (int k = 0; k < sides; k++)
            {
                Vector3 p0 = Ring(k, radius, y0);
                Vector3 p1 = Ring(k + 1, radius, y0);
                builder.AddFlatTriangle(apex, p1, p0, 1);
                builder.AddFlatTriangle(bottom, p0, p1, 1);
            }
        }
    }

    // ---------------------------------------------------------------- Ánh sáng & render

    static void BuildLights(Scene scene, Transform root, Vector3 home, Vector3 eye)
    {
        Light moon = RenderSettings.sun;
        if (moon == null)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Light light in go.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional)
                    {
                        moon = light;
                        break;
                    }
                }
                if (moon != null) break;
            }
        }

        if (moon != null)
        {
            Undo.RecordObject(moon, "Night Environment");
            Undo.RecordObject(moon.transform, "Night Environment");
            moon.useColorTemperature = false;
            moon.color = new Color(0.62f, 0.72f, 1f);
            moon.intensity = 0.8f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;
            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
            RenderSettings.sun = moon;
        }
        else
        {
            Debug.LogWarning("Không tìm thấy Directional Light để làm ánh trăng.");
        }

        // Đèn phụ xanh lạnh chiếu vào mặt nhân vật (trăng ở phía sau lưng)
        GameObject fill = new GameObject("Character Fill Light");
        fill.transform.SetParent(root, false);
        fill.transform.position = Vector3.Lerp(home, eye, 0.5f) + Vector3.up * 1.2f;
        Light fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.color = new Color(0.6f, 0.7f, 1f);
        fillLight.intensity = 1.5f;
        fillLight.range = 6f;
        fillLight.shadows = LightShadows.None;
    }

    static void SetupRenderSettings(Material skyMaterial)
    {
        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.2f, 0.36f);
        RenderSettings.ambientEquatorColor = new Color(0.08f, 0.09f, 0.17f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.05f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.1f);
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 260f;

        DynamicGI.UpdateEnvironment();
    }

    static void SetupCamera(Camera cam)
    {
        Undo.RecordObject(cam, "Night Environment");
        cam.clearFlags = CameraClearFlags.Skybox;
        if (cam.farClipPlane < StarDistance + 150f)
            cam.farClipPlane = 1000f;

        UniversalAdditionalCameraData cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            Undo.RecordObject(cameraData, "Night Environment");
            cameraData.renderPostProcessing = true;
        }
    }

    static void SetupPostProcessing(Scene scene, Camera cam, Transform root)
    {
        Volume volume = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            foreach (Volume v in go.GetComponentsInChildren<Volume>(true))
            {
                if (v.isGlobal && v.sharedProfile != null)
                {
                    volume = v;
                    break;
                }
            }
            if (volume != null) break;
        }

        if (volume == null)
        {
            string path = GeneratedFolder + "/Night Post Processing.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            GameObject go = new GameObject("Night Post Processing");
            go.transform.SetParent(root, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // Chỉ thêm hiệu ứng còn thiếu, không ghi đè thiết lập sẵn có
        VolumeProfile target = volume.sharedProfile;
        AddOverride<Bloom>(target, b =>
        {
            b.threshold.Override(0.85f);
            b.intensity.Override(0.9f);
            b.scatter.Override(0.7f);
        });
        AddOverride<Tonemapping>(target, t => t.mode.Override(TonemappingMode.Neutral));
        AddOverride<Vignette>(target, v =>
        {
            v.intensity.Override(0.3f);
            v.smoothness.Override(0.45f);
        });
        EditorUtility.SetDirty(target);
    }

    static void AddOverride<T>(VolumeProfile profile, System.Action<T> setup) where T : VolumeComponent
    {
        if (profile.Has<T>()) return;

        T component = profile.Add<T>();
        component.name = typeof(T).Name;
        setup(component);

        if (EditorUtility.IsPersistent(profile))
            AssetDatabase.AddObjectToAsset(component, profile);
    }

    static void HideOldPlane(Scene scene)
    {
        GameObject plane = FindRoot(scene, "Plane");
        if (plane == null || !plane.activeSelf) return;

        Undo.RecordObject(plane, "Night Environment");
        plane.SetActive(false);
    }

    // ---------------------------------------------------------------- Tiện ích

    static void ReadCharacterSpots(Scene scene, out Vector3 home, out Vector3 quit)
    {
        home = DefaultHome;
        quit = DefaultQuit;

        MenuCharacterReaction reaction = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            reaction = go.GetComponentInChildren<MenuCharacterReaction>(true);
            if (reaction != null) break;
        }

        if (reaction == null)
        {
            Debug.LogWarning("Không tìm thấy MenuCharacterReaction, dùng vị trí tảng đá mặc định.");
            return;
        }

        SerializedObject so = new SerializedObject(reaction);
        Transform characterRoot = so.FindProperty("characterRoot").objectReferenceValue as Transform;
        Transform quitSpot = so.FindProperty("quitSpot").objectReferenceValue as Transform;

        home = (characterRoot != null ? characterRoot : reaction.transform).position;
        quit = quitSpot != null ? quitSpot.position : home;
    }

    static Camera FindMainCamera(Scene scene)
    {
        if (Camera.main != null) return Camera.main;

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            Camera cam = go.GetComponentInChildren<Camera>(true);
            if (cam != null) return cam;
        }
        return null;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets", "Night Environment");
        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder(RootFolder, "Generated");
    }

    static GameObject CreateRenderer(Transform parent, string name, Mesh mesh, Material[] materials,
        bool castShadows, bool receiveShadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = receiveShadows;
        return go;
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        string path = GeneratedFolder + "/" + mesh.name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material GetOrCreateMaterial(string name, string shaderName, System.Action<Material> setup)
    {
        string path = GeneratedFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("Không tìm thấy shader '" + shaderName + "'.");
            return null;
        }

        material = new Material(shader) { name = name };
        setup?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static System.Action<Material> Lit(Color color, float smoothness)
    {
        return m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        };
    }

    static Color ToVertexColor(Color color)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
    }

    static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static float Rand() => (float)rng.NextDouble();

    static float Range(float min, float max) => Mathf.Lerp(min, max, Rand());

    static float Gaussian()
    {
        float u1 = Mathf.Max(1e-6f, Rand());
        float u2 = Rand();
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int>[] triangles;
        bool flat;

        public MeshBuilder(int subMeshCount)
        {
            triangles = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
                triangles[i] = new List<int>();
        }

        public int AddVertex(Vector3 position)
        {
            vertices.Add(position);
            return vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c, int subMesh = 0)
        {
            triangles[subMesh].Add(a);
            triangles[subMesh].Add(b);
            triangles[subMesh].Add(c);
        }

        // Tam giác flat-shaded: mỗi tam giác có đỉnh và normal riêng (phong cách low-poly)
        public void AddFlatTriangle(Vector3 a, Vector3 b, Vector3 c, int subMesh)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-10f) return;
            normal.Normalize();

            flat = true;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            AddTriangle(start, start + 1, start + 2, subMesh);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i);

            if (flat)
                mesh.SetNormals(normals);
            else
                mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

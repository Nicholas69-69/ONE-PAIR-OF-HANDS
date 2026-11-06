using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OnePairOfHands.EditorTools
{
    /// <summary>
    /// Builds (or rebuilds) the One Pair of Hands gameplay setup in the "Level Design" scene:
    /// home cluster, task locations, Ataa, camera, HUD, world labels and the NavMesh bake.
    /// Only touches objects under "OnePairOfHands" (plus the HUD/EventSystem it creates),
    /// adds the follow script to Main Camera, and disables the one tree at the camp centre.
    /// Menu: Tools/One Pair of Hands/Rebuild Gameplay Setup
    /// </summary>
    public static class OnePairOfHandsBuilder
    {
        private const string Root = "OnePairOfHands";
        private const string MatFolder = "Assets/OnePairOfHands/Materials";
        private const string NavFolder = "Assets/OnePairOfHands/NavMesh";
        private const string SkydenPrefabs = "Assets/Skyden_Games/Low Poly Environment/Prefabs/";
        private const int IgnoreRaycastLayer = 2;

        // Layout (world space). Camp centre = old "Tree 2  (36)".
        private static readonly Vector3 CampCentre = new Vector3(-118.4f, 0f, 127.5f);
        private static readonly Vector3 FirePos = new Vector3(-118.4f, 0f, 128.5f);
        private static readonly Vector3 CookStand = new Vector3(-118.4f, 0f, 125.3f);
        private static readonly Vector3 AtaaSpawn = new Vector3(-121.5f, 0f, 124.5f);
        private static readonly Vector3 WorkStand = new Vector3(-107f, 0f, 140.3f);
        private static readonly Vector3 WorkCaravan = new Vector3(-107f, 0f, 147f);
        private static readonly Vector3 WaterStand = new Vector3(-44f, 0f, 130.5f);
        private static readonly Vector3 TeachStand = new Vector3(-86.4f, 0f, 172.1f);

        private static readonly (string prefab, Vector3 pos, string name)[] HomeHouses =
        {
            ("House 1 ", new Vector3(-129.5f, 0f, 136f), "Home_House_A"),
            ("House 3 ", new Vector3(-128f, 0f, 119.5f), "Home_House_B"),
            ("House 5 ", new Vector3(-116f, 0f, 143f), "Home_House_C"),
        };

        [MenuItem("Tools/One Pair of Hands/Rebuild Gameplay Setup")]
        public static void Build()
        {
            var root = GameObject.Find(Root) ?? new GameObject(Root);
            foreach (var n in new[] { "Systems", "Home", "Tasks", "Ataa", "HUD" })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // The one tree at the camp centre is disabled (not deleted).
            var tree = FindModel("Tree 2  (36)");
            if (tree != null) tree.SetActive(false);
            Physics.SyncTransforms();

            var mats = BuildMaterials();

            // ---------- Systems ----------
            var systems = Child(root, "Systems");
            var clock = systems.AddComponent<GameClock>();
            var manager = systems.AddComponent<TaskManager>();

            // ---------- Home cluster ----------
            var home = Child(root, "Home");
            home.transform.position = Ground(CampCentre);
            foreach (var h in HomeHouses)
                PlaceHouse(h.prefab, h.pos, h.name, home.transform, FirePos);

            var homeZone = Child(home, "HomeZone");
            homeZone.layer = IgnoreRaycastLayer;
            homeZone.transform.position = Ground(new Vector3(-120f, 0f, 129f));
            var hzCol = homeZone.AddComponent<BoxCollider>();
            hzCol.isTrigger = true;
            hzCol.size = new Vector3(20f, 8f, 20f);
            hzCol.center = new Vector3(0f, 3f, 0f);
            var hz = homeZone.AddComponent<HomeZone>();

            // ---------- Tasks ----------
            var tasks = Child(root, "Tasks");

            // 1 Water — at the existing "Water Well" across the bridge.
            var water = MakeTask(tasks, "Task_Water", WaterStand, TaskType.Water, 1, "Collect water", 0.0030f);
            var waterAction = water.AddComponent<WaterTaskAction>();
            var well = FindModel("Water Well ");
            Vector3 wellTop = well != null ? Bounds(well).center + Vector3.up * (Bounds(well).extents.y + 0.5f) : water.transform.position + Vector3.up * 6f;
            MakeLabel(water.transform, "1 Collect water", wellTop);

            // 2 Cook — a cooking fire beside the home caravans.
            var cook = MakeTask(tasks, "Task_Cook", CookStand, TaskType.Cook, 2, "Cook food", 0.0035f);
            cook.AddComponent<CookTaskAction>();
            BuildFire(cook.transform, Ground(FirePos), mats);
            MakeLabel(cook.transform, "2 Cook food", Ground(FirePos) + Vector3.up * 2.5f);

            // 3 Teach — the existing tent nearest home ("Tent 2 ").
            var teach = MakeTask(tasks, "Task_Teach", TeachStand, TaskType.Teach, 3, "Teach the children", 0.0030f);
            teach.AddComponent<TeachTaskAction>();
            var tent = FindModel("Tent 2 ");
            Vector3 tentTop = tent != null ? Bounds(tent).center + Vector3.up * (Bounds(tent).extents.y + 0.5f) : teach.transform.position + Vector3.up * 4f;
            MakeLabel(teach.transform, "3 Teach the children", tentTop);

            // 4 Work — one caravan in the home cluster, set apart, with a glowing screen.
            var work = MakeTask(tasks, "Task_Work", WorkStand, TaskType.Work, 4, "Work online", 0.0035f);
            var workAction = work.AddComponent<WorkTaskAction>();
            var caravan = PlaceHouse("House 4 ", WorkCaravan, "Work_Caravan", work.transform, WorkStand);
            var (screen, screenLight) = BuildScreen(work.transform, mats);
            MakeLabel(work.transform, "4 Work online", screen.transform.position + Vector3.up * 1.2f);

            // ---------- Bake NavMesh (before Ataa exists; triggers are on Ignore Raycast and excluded) ----------
            BakeNavMesh(root);

            // ---------- Ataa ----------
            var ataa = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ataa.name = "Ataa";
            ataa.transform.SetParent(root.transform);
            Vector3 spawn = Ground(AtaaSpawn);
            if (NavMesh.SamplePosition(spawn, out var navHit, 3f, NavMesh.AllAreas)) spawn = navHit.position;
            ataa.transform.position = spawn + Vector3.up * 1f;
            ataa.GetComponent<Renderer>().sharedMaterial = mats["Ataa_Warm"];
            var rb = ataa.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var agent = ataa.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.baseOffset = 1f;
            agent.speed = 2.2f;
            agent.angularSpeed = 540f;
            agent.acceleration = 7f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            var controller = ataa.AddComponent<AtaaController>();

            // ---------- Camera ----------
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<TopDownCameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<TopDownCameraFollow>();
                Set(follow, "target", ataa.transform);
                if (cam.farClipPlane < 400f) cam.farClipPlane = 400f;
                cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
                cam.transform.position = ataa.transform.position + Quaternion.Euler(50f, 0f, 0f) * Vector3.back * 26f + Vector3.up;
                Set(controller, "viewCamera", cam);
            }

            // ---------- Wiring ----------
            Set(hz, "water", waterAction);
            Set(workAction, "screenRenderer", screen);
            Set(workAction, "screenLight", screenLight);
            var so = new SerializedObject(manager);
            var arr = so.FindProperty("tasks");
            arr.arraySize = 4;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = water.GetComponent<TaskState>();
            arr.GetArrayElementAtIndex(1).objectReferenceValue = cook.GetComponent<TaskState>();
            arr.GetArrayElementAtIndex(2).objectReferenceValue = teach.GetComponent<TaskState>();
            arr.GetArrayElementAtIndex(3).objectReferenceValue = work.GetComponent<TaskState>();
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("ataa").objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- HUD ----------
            BuildHUD(root, manager, clock);
            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[OnePairOfHands] Gameplay setup rebuilt.");
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject FindModel(string name)
        {
            var models = GameObject.Find("Models");
            if (models == null) return null;
            var t = models.transform.Find(name);
            return t != null ? t.gameObject : null;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Snap an XZ position to the top of the environment ground (ignores our own objects and triggers).</summary>
        private static Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var n = h.collider.transform.root.name;
                if (n == Root && !h.collider.name.StartsWith("Stairs") && !h.collider.name.StartsWith("BridgeWalkway")) continue;
                if (h.collider.name.StartsWith("Tree")) continue;
                return h.point;
            }
            return p;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[OnePairOfHands] No field '{field}' on {target}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject PlaceHouse(string prefabName, Vector3 pos, string name, Transform parent, Vector3 faceTowards)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkydenPrefabs + prefabName + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = Ground(pos);
            Vector3 dir = faceTowards - pos;
            dir.y = 0f;
            // Skyden houses have their door on local -Z... rotate so the front faces the given point.
            go.transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up) * Quaternion.Euler(0f, HouseYawOffset, 0f);
            return go;
        }

        /// <summary>Extra yaw applied to Skyden houses so their door faces the camp. Tuned by eye.</summary>
        private const float HouseYawOffset = 0f;

        private static GameObject MakeTask(GameObject parent, string name, Vector3 pos, TaskType type, int number, string display, float drain)
        {
            var go = Child(parent, name);
            go.layer = IgnoreRaycastLayer;
            Vector3 p = Ground(pos);
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) p = hit.position;
            go.transform.position = p;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 4f, 3f);
            col.center = new Vector3(0f, 1.5f, 0f);
            var state = go.AddComponent<TaskState>();
            var so = new SerializedObject(state);
            so.FindProperty("type").enumValueIndex = (int)type;
            so.FindProperty("number").intValue = number;
            so.FindProperty("displayName").stringValue = display;
            so.FindProperty("drainPerSecond").floatValue = drain;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void MakeLabel(Transform parent, string text, Vector3 worldPos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 11f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.98f, 0.96f, 0.9f);
            tmp.fontSharedMaterial = LabelMaterial(tmp);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(24f, 4f);
            go.AddComponent<WorldLabel>();
        }

        /// <summary>Shared outlined font material for the floating world labels (avoids per-label instances).</summary>
        private static Material LabelMaterial(TextMeshPro tmp)
        {
            string path = $"{MatFolder}/WorldLabel_Outline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(tmp.font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.25f);
            m.SetColor("_OutlineColor", new Color32(30, 32, 40, 255));
            m.SetFloat("_FaceDilate", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/OnePairOfHands", "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var d = new Dictionary<string, Material>
            {
                ["Ataa_Warm"] = Mat("Ataa_Warm", lit, new Color(0.90f, 0.56f, 0.32f), Color.black),
                ["Log_Wood"] = Mat("Log_Wood", lit, new Color(0.38f, 0.25f, 0.15f), Color.black),
                ["Fire_Glow"] = Mat("Fire_Glow", lit, new Color(1f, 0.55f, 0.15f), new Color(1f, 0.45f, 0.1f) * 3f),
                ["Screen_Glow"] = Mat("Screen_Glow", lit, new Color(0.55f, 0.75f, 1f), new Color(0.55f, 0.75f, 1f) * 2f),
                ["Screen_Stand"] = Mat("Screen_Stand", lit, new Color(0.25f, 0.24f, 0.26f), Color.black),
            };
            AssetDatabase.SaveAssets();
            return d;
        }

        private static Material Mat(string name, Shader shader, Color baseColor, Color emission)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseColor);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void BuildFire(Transform parent, Vector3 centre, Dictionary<string, Material> mats)
        {
            var fire = new GameObject("CookingFire");
            fire.transform.SetParent(parent, false);
            fire.transform.position = centre;

            var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkydenPrefabs + "Rock 1 .prefab");
            const int stones = 8;
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                var r = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, fire.transform);
                r.name = $"FireStone_{i}";
                r.transform.position = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f;
                r.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                r.transform.localScale = Vector3.one * 0.11f;
            }

            for (int i = 0; i < 3; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = $"Log_{i}";
                Object.DestroyImmediate(log.GetComponent<Collider>());
                log.transform.SetParent(fire.transform, false);
                log.transform.position = centre + Vector3.up * 0.18f;
                log.transform.rotation = Quaternion.Euler(0f, i * 60f, 80f);
                log.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);
                log.GetComponent<Renderer>().sharedMaterial = mats["Log_Wood"];
            }

            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            flame.transform.SetParent(fire.transform, false);
            flame.transform.position = centre + Vector3.up * 0.55f;
            flame.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
            flame.GetComponent<Renderer>().sharedMaterial = mats["Fire_Glow"];
            flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // NavMesh obstacle footprint so Ataa walks around the fire rather than through it.
            var block = new GameObject("FireFootprint");
            block.transform.SetParent(fire.transform, false);
            block.transform.position = centre + Vector3.up * 0.5f;
            var bc = block.AddComponent<BoxCollider>();
            bc.size = new Vector3(2.2f, 1f, 2.2f);

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(fire.transform, false);
            lightGo.transform.position = centre + Vector3.up * 1.2f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.22f);
            light.intensity = 6f;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }

        private static (Renderer, Light) BuildScreen(Transform parent, Dictionary<string, Material> mats)
        {
            var stand = new GameObject("WorkScreen");
            stand.transform.SetParent(parent, false);
            Vector3 basePos = Ground(new Vector3(WorkStand.x + 1.9f, 0f, WorkStand.z + 1.3f));
            stand.transform.position = basePos;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Stand";
            post.transform.SetParent(stand.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            post.transform.localScale = new Vector3(1.1f, 1f, 0.6f);
            post.GetComponent<Renderer>().sharedMaterial = mats["Screen_Stand"];

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ScreenQuad";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(stand.transform, false);
            quad.transform.localPosition = new Vector3(0f, 1.35f, -0.05f);
            quad.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            quad.transform.localScale = new Vector3(1.0f, 0.65f, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mats["Screen_Glow"];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("ScreenLight");
            lightGo.transform.SetParent(stand.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.4f, -0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.78f, 1f);
            light.intensity = 2f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
            return (r, light);
        }

        private static void BakeNavMesh(GameObject root)
        {
            var navTf = root.transform.Find("NavMesh");
            var navGo = navTf != null ? navTf.gameObject : null;
            if (navGo == null)
            {
                navGo = Child(root, "NavMesh");
            }
            var surface = navGo.GetComponent<NavMeshSurface>();
            if (surface == null) surface = navGo.AddComponent<NavMeshSurface>();
            // Playable area only: home plateau, river + bridge, well bank, and the tent terrace.
            navGo.transform.position = new Vector3(-82f, -2f, 145f);
            navGo.transform.rotation = Quaternion.identity;
            surface.collectObjects = CollectObjects.Volume;
            surface.size = new Vector3(120f, 30f, 100f);
            surface.center = Vector3.zero;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << IgnoreRaycastLayer) | (1 << 5));
            surface.ignoreNavMeshAgent = true;

            // Water is never walkable.
            foreach (var n in new[] { "Lake", "Lake (1)", "Lake (2)", "Water Fall " })
            {
                var w = FindModel(n);
                if (w == null) continue;
                var mod = w.GetComponent<NavMeshModifier>();
                if (mod == null) mod = w.AddComponent<NavMeshModifier>();
                mod.overrideArea = true;
                mod.area = NavMesh.GetAreaFromName("Not Walkable");
            }

            Physics.SyncTransforms();
            surface.BuildNavMesh();

            if (!AssetDatabase.IsValidFolder(NavFolder)) AssetDatabase.CreateFolder("Assets/OnePairOfHands", "NavMesh");
            string path = $"{NavFolder}/LevelDesign_NavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ HUD

        private static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.78f);
        private static readonly Color TextMain = new Color(0.96f, 0.94f, 0.89f, 1f);
        private static readonly Color TextSoft = new Color(0.78f, 0.78f, 0.80f, 1f);
        private static readonly Color BarBg = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color BarFill = new Color(0.62f, 0.72f, 0.88f, 1f);

        private static void BuildHUD(GameObject root, TaskManager manager, GameClock clock)
        {
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(root.transform, false);
            hudGo.layer = 5;
            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = hudGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            hudGo.AddComponent<GraphicRaycaster>();
            var hud = hudGo.AddComponent<HUDController>();
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var white = WhiteSprite();

            // Clock (top centre)
            var clockPanel = Panel(hudGo.transform, "ClockPanel", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(420f, 72f), sprite);
            var clockText = Text(clockPanel, "ClockText", "Day 1 · 08:00", 40, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            var carry = Text(hudGo.transform, "CarryText", "Carrying water home", 28, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(900f, 44f));
            carry.color = TextSoft;
            carry.gameObject.SetActive(false);

            // Current task (bottom centre)
            var taskPanel = Panel(hudGo.transform, "TaskPanel", new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(880f, 236f), sprite);
            var taskName = Text(taskPanel, "TaskName", "1 Collect water", 40, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -16f), new Vector2(-60f, 52f));
            var leftLbl = Text(taskPanel, "CarriedLabel", "carried so far", 22, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(15f, -76f), new Vector2(-30f, 30f));
            leftLbl.color = TextSoft;
            var rightLbl = Text(taskPanel, "EnoughLabel", "enough for now", 22, TextAlignmentOptions.Right,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-15f, -76f), new Vector2(-30f, 30f));
            rightLbl.color = TextSoft;
            var bar = Bar(taskPanel, "CarriedBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -122f), new Vector2(-60f, 28f), white, out var taskFill);
            var hint = Text(taskPanel, "HintText", "Tap to stir the pot.", 26, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(-60f, 72f));
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            taskPanel.gameObject.SetActive(false);

            // Meanwhile strip (top left)
            var mw = Panel(hudGo.transform, "MeanwhilePanel", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 380f), sprite);
            mw.pivot = new Vector2(0f, 1f);
            var header = Text(mw, "Header", "Meanwhile", 28, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -12f), new Vector2(-40f, 38f));
            header.color = TextSoft;
            var rows = new List<(GameObject, TMP_Text, TMP_Text, Image)>();
            for (int i = 0; i < 4; i++)
            {
                var row = new GameObject($"Row_{i}", typeof(RectTransform));
                row.layer = 5;
                var rt = (RectTransform)row.transform;
                rt.SetParent(mw, false);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -60f - i * 78f);
                rt.sizeDelta = new Vector2(-40f, 70f);
                var n = Text(rt, "Name", "1 Collect water", 26, TextAlignmentOptions.Left,
                    new Vector2(0f, 1f), new Vector2(0.68f, 1f), new Vector2(0f, 0f), new Vector2(0f, 36f));
                n.rectTransform.pivot = new Vector2(0.5f, 1f);
                var s = Text(rt, "Status", "not been", 24, TextAlignmentOptions.Right,
                    new Vector2(0.68f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 36f));
                s.rectTransform.pivot = new Vector2(0.5f, 1f);
                Bar(rt, "Density", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(0f, 14f), white, out var f);
                rows.Add((row, n, s, f));
            }

            // End of day panel (full screen, blocks clicks)
            var endGo = new GameObject("EndPanel", typeof(RectTransform));
            endGo.layer = 5;
            var endRt = (RectTransform)endGo.transform;
            endRt.SetParent(hudGo.transform, false);
            endRt.anchorMin = Vector2.zero;
            endRt.anchorMax = Vector2.one;
            endRt.offsetMin = endRt.offsetMax = Vector2.zero;
            var dim = endGo.AddComponent<Image>();
            dim.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
            dim.raycastTarget = true;
            var endTitle = Text(endRt, "Title", "16:00 — the day ends", 48, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 70f));
            var endList = Text(endRt, "List", "", 36, TextAlignmentOptions.Top,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 260f));
            endList.lineSpacing = 20f;
            var closing = Text(endRt, "Closing", "This is a small part of Ataa's reality, not the whole story.", 32, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1400f, 60f));
            closing.fontStyle = FontStyles.Italic;
            closing.color = TextSoft;
            endGo.SetActive(false);

            // Wire HUDController
            var so = new SerializedObject(hud);
            so.FindProperty("taskManager").objectReferenceValue = manager;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("clockText").objectReferenceValue = clockText;
            so.FindProperty("carryText").objectReferenceValue = carry;
            so.FindProperty("taskPanel").objectReferenceValue = taskPanel.gameObject;
            so.FindProperty("taskNameText").objectReferenceValue = taskName;
            so.FindProperty("taskFill").objectReferenceValue = taskFill;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("meanwhileHeader").objectReferenceValue = header;
            so.FindProperty("meanwhilePanel").objectReferenceValue = mw;
            var rowsProp = so.FindProperty("meanwhileRows");
            rowsProp.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var e = rowsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].Item1;
                e.FindPropertyRelative("nameText").objectReferenceValue = rows[i].Item2;
                e.FindPropertyRelative("statusText").objectReferenceValue = rows[i].Item3;
                e.FindPropertyRelative("fill").objectReferenceValue = rows[i].Item4;
            }
            so.FindProperty("endPanel").objectReferenceValue = endGo;
            so.FindProperty("endListText").objectReferenceValue = endList;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = PanelBg;
            img.raycastTarget = false;
            return rt;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var t = Text(parent, name, text, size, align, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TextMain;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private static RectTransform Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Sprite sprite, out Image fill)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var bg = go.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Simple;
            bg.color = BarBg;
            bg.raycastTarget = false;

            var fGo = new GameObject("Fill", typeof(RectTransform));
            fGo.layer = 5;
            var frt = (RectTransform)fGo.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            fill = fGo.AddComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.color = BarFill;
            fill.raycastTarget = false;
            return rt;
        }

        /// <summary>A plain white sprite asset so filled bars draw as flat colour.</summary>
        private static Sprite WhiteSprite()
        {
            const string path = "Assets/OnePairOfHands/UI/White.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/OnePairOfHands/UI")) AssetDatabase.CreateFolder("Assets/OnePairOfHands", "UI");
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Object.DestroyImmediate(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}

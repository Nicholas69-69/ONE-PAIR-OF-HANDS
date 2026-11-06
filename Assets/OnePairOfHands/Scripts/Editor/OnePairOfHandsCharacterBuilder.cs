using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OnePairOfHands.EditorTools
{
    /// <summary>
    /// Sets up Ataa's Mixamo character: humanoid import, URP materials, Animator ("Ataa_Animator"),
    /// and replaces the capsule visuals under the "Ataa" object (agent, colliders and controller untouched).
    /// Menu: Tools/One Pair of Hands/Setup Ataa Character
    /// </summary>
    public static class OnePairOfHandsCharacterBuilder
    {
        public const string Folder = "Assets/OnePairOfHands/Character";
        public const string NoMinimapLayer = "NoMinimap";
        private const float TargetHeight = 1.65f;

        [MenuItem("Tools/One Pair of Hands/Setup Ataa Character")]
        public static void Setup()
        {
            EditorSceneManager.SaveOpenScenes();
            EnsureLayer(NoMinimapLayer);

            var files = FindFiles(out string modelPath, out string idlePath, out string walkPath);
            Debug.Log($"[OnePairOfHands] Character files: model={modelPath}, idle={idlePath}, walk={walkPath}. {files}");

            ConfigureModel(modelPath);
            var mat = SetupMaterial(modelPath); // reimports the model, so load the avatar after this
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First();
            ConfigureClip(idlePath, avatar, "Idle");
            ConfigureClip(walkPath, avatar, "Walking");

            var idle = LoadClip(idlePath, "Idle");
            var walk = LoadClip(walkPath, "Walking");

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            float scale = MeasureScale(modelPrefab, avatar);
            float walkSpeed = MeasureWalkSpeed(modelPrefab, avatar, walk, scale);
            Debug.Log($"[OnePairOfHands] Ataa scale {scale:F3}, Walking clip natural ground speed {walkSpeed:F2} m/s");

            var controller = BuildController(idle, walk, walkSpeed);

            var scene = EditorSceneManager.OpenScene(OnePairOfHandsUIBuilder.GameScenePath, OpenSceneMode.Single);
            HookUp(modelPrefab, avatar, controller, scale, walkSpeed, mat);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[OnePairOfHands] Ataa character set up.");
        }

        // ------------------------------------------------------------------ files

        /// <summary>Finds the model (has no clip) and the two clips by content, not just by name.</summary>
        private static string FindFiles(out string model, out string idle, out string walk)
        {
            model = idle = walk = null;
            var fbx = Directory.GetFiles(Folder, "*.fbx").Select(p => p.Replace('\\', '/')).ToList();
            var withClip = new List<(string path, float len)>();
            foreach (var p in fbx)
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
                if (clips.Count == 0) { if (model == null || Path.GetFileNameWithoutExtension(p) == "Ataa") model = p; }
                else withClip.Add((p, clips[0].length));
            }
            // Fallback: if every file has a clip, the one named Ataa (or the first with a SkinnedMeshRenderer) is the model.
            if (model == null)
            {
                model = fbx.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "Ataa")
                        ?? fbx.First(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentInChildren<SkinnedMeshRenderer>() != null);
            }
            string modelFile = model;
            withClip.RemoveAll(c => c.path == modelFile);
            walk = withClip.FirstOrDefault(c => c.path.ToLower().Contains("walk")).path ?? withClip.OrderBy(c => c.len).First().path;
            string w = walk;
            idle = withClip.FirstOrDefault(c => c.path.ToLower().Contains("idle")).path ?? withClip.First(c => c.path != w).path;
            return "(found by content: the model is the file with no animation clip)";
        }

        private static AnimationClip LoadClip(string path, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == name);

        // ------------------------------------------------------------------ import

        private static void ConfigureModel(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SaveAndReimport();

            string texDir = Folder + "/Textures";
            if (!AssetDatabase.IsValidFolder(texDir)) AssetDatabase.CreateFolder(Folder, "Textures");
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
        }

        private static void ConfigureClip(string path, Avatar source, string clipName)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = source;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = true;
            imp.SaveAndReimport();

            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = clipName;
                c.loopTime = true;
                c.lockRootRotation = true;        // Bake Into Pose: root rotation
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;         // Bake Into Pose: root Y
                c.keepOriginalPositionY = true;
                // Not baked: Walking.fbx actually travels forward (~1.6 m per cycle), so XZ goes to root
                // motion, which is off. Baking it would make her walk ahead and snap back each loop.
                c.lockRootPositionXZ = false;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        private static Material SetupMaterial(string modelPath)
        {
            string matDir = Folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(Folder, "Materials");

            var embedded = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList();
            Material result = null;
            foreach (var em in embedded)
            {
                string dst = $"{matDir}/{em.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null)
                {
                    string err = AssetDatabase.ExtractAsset(em, dst);
                    if (!string.IsNullOrEmpty(err)) Debug.LogWarning("Extract material: " + err);
                    AssetDatabase.WriteImportSettingsIfDirty(modelPath);
                    AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
                }
                result = AssetDatabase.LoadAssetAtPath<Material>(dst);
            }
            if (result == null) return null;

            // Find the extracted textures by name.
            Texture2D diffuse = null, normal = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder + "/Textures" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p).ToLower();
                if (n.Contains("diffuse") || n.Contains("albedo") || n.Contains("basecolor")) diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (n.Contains("normal"))
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                    if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
            }

            result.shader = Shader.Find("Universal Render Pipeline/Lit");
            if (diffuse != null) result.SetTexture("_BaseMap", diffuse);
            // Muted, warm tint so she sits in the low-poly camp palette.
            result.SetColor("_BaseColor", new Color(0.90f, 0.84f, 0.76f, 1f));
            if (normal != null)
            {
                result.SetTexture("_BumpMap", normal);
                result.SetFloat("_BumpScale", 0.6f);
                result.EnableKeyword("_NORMALMAP");
            }
            result.SetFloat("_Metallic", 0f);
            result.SetFloat("_Smoothness", 0.15f);
            result.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(result);
            return result;
        }

        // ------------------------------------------------------------------ measurement

        private static float MeasureScale(GameObject prefab, Avatar avatar)
        {
            var go = Object.Instantiate(prefab);
            try
            {
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true);
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var v in mesh.vertices)
                {
                    float y = smr.transform.TransformPoint(v).y;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                Object.DestroyImmediate(mesh);
                return TargetHeight / (maxY - minY);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Ground speed implied by the in-place walk: how fast the planted (lower) foot slides
        /// backwards relative to the body, at the final scale.
        /// </summary>
        private static float MeasureWalkSpeed(GameObject prefab, Avatar avatar, AnimationClip walk, float scale)
        {
            var go = Object.Instantiate(prefab);
            go.transform.localScale = Vector3.one * scale;
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = avatar;
            // Sampling with root motion extracted keeps the body over the root, so the planted foot
            // slides back at the ground speed. (At runtime root motion is off and simply discarded.)
            anim.applyRootMotion = true;
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            const int steps = 120;
            float sum = 0f;
            int count = 0;
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            try
            {
                AnimationMode.StartAnimationMode();
                for (int i = 0; i <= steps; i++)
                {
                    float t = walk.length * i / steps;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, walk, t);
                    AnimationMode.EndSampling();
                    Vector3 l = go.transform.InverseTransformPoint(lf.position) * scale;
                    Vector3 r = go.transform.InverseTransformPoint(rf.position) * scale;
                    if (i > 0)
                    {
                        bool leftPlanted = l.y < r.y;
                        float dz = leftPlanted ? (prevL.z - l.z) : (prevR.z - r.z);
                        float dt = walk.length / steps;
                        float v = dz / dt;
                        if (v > 0f) { sum += v; count++; }
                    }
                    prevL = l;
                    prevR = r;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
            float speed = count > 0 ? sum / count : 1.3f;
            return Mathf.Clamp(speed, 0.6f, 2.5f);
        }

        // ------------------------------------------------------------------ animator

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, float walkSpeed)
        {
            string path = Folder + "/Ataa_Animator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ac.AddParameter("Working", AnimatorControllerParameterType.Bool);

            var layers = ac.layers;
            layers[0].iKPass = true; // procedural working pose uses Animator IK
            layers[0].name = "Base Layer";
            ac.layers = layers;

            var state = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, walkSpeed);
            state.speedParameterActive = true;
            state.speedParameter = "AnimSpeed";
            state.writeDefaultValues = true;
            ac.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ------------------------------------------------------------------ scene

        private static void HookUp(GameObject prefab, Avatar avatar, AnimatorController controller, float scale, float walkSpeed, Material mat)
        {
            var ataa = GameObject.Find("OnePairOfHands/Ataa");
            var agent = ataa.GetComponent<NavMeshAgent>();
            var old = ataa.transform.Find("Ataa_Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Capsule visuals off; collider, agent and controller stay as they are.
            var mr = ataa.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ataa.transform);
            model.name = "Ataa_Model";
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale / ataa.transform.lossyScale.y;

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (mat != null) smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;

            // Feet on the ground: ground = agent position - baseOffset (in the idle pose).
            float groundY = ataa.transform.position.y - agent.baseOffset * ataa.transform.lossyScale.y;
            model.transform.position = new Vector3(ataa.transform.position.x, groundY, ataa.transform.position.z);
            var idle = controller.layers[0].stateMachine.defaultState.motion as BlendTree;
            var idleClip = idle != null ? idle.children[0].motion as AnimationClip : null;
            float footMin;
            if (idleClip != null)
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(model, idleClip, 0f);
                AnimationMode.EndSampling();
                footMin = LowestPoint(smr);
                AnimationMode.StopAnimationMode();
            }
            else footMin = LowestPoint(smr);
            model.transform.position += Vector3.up * (groundY - footMin);

            // Jerrycan in the right hand (muted yellow), shown only while carrying.
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var can = BuildJerrycan();
            can.transform.position = hand.position + Vector3.down * 0.2f;
            can.transform.rotation = model.transform.rotation;
            can.transform.SetParent(hand, true);
            can.SetActive(false);

            SetLayerRecursive(model, LayerMask.NameToLayer(NoMinimapLayer));

            var driver = model.AddComponent<AtaaAnimatorDriver>();
            var so = new SerializedObject(driver);
            so.FindProperty("agent").objectReferenceValue = agent;
            so.FindProperty("walkClipSpeed").floatValue = walkSpeed;
            so.FindProperty("jerrycan").objectReferenceValue = can;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Reset the editor pose so the scene isn't saved mid-sample.
            model.transform.localRotation = Quaternion.identity;
        }

        private static float LowestPoint(SkinnedMeshRenderer smr)
        {
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            float minY = float.MaxValue;
            foreach (var v in mesh.vertices) minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
            Object.DestroyImmediate(mesh);
            return minY;
        }

        private static GameObject BuildJerrycan()
        {
            string matPath = "Assets/OnePairOfHands/Materials/Jerrycan_MutedYellow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetColor("_BaseColor", new Color(0.80f, 0.70f, 0.42f));
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);

            var root = new GameObject("Jerrycan"); // built in world space (metres), then parented to the hand
            void Part(PrimitiveType type, string name, Vector3 pos, Vector3 size)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = name;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = pos;
                p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }
            // Sizes are in metres; the root is unscaled in world space.
            Part(PrimitiveType.Cube, "Body", new Vector3(0f, -0.06f, 0f), new Vector3(0.12f, 0.30f, 0.24f));
            Part(PrimitiveType.Cube, "Handle", new Vector3(0f, 0.12f, 0.02f), new Vector3(0.03f, 0.04f, 0.14f));
            Part(PrimitiveType.Cylinder, "Spout", new Vector3(0f, 0.11f, -0.09f), new Vector3(0.045f, 0.03f, 0.045f));
            return root;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = name;
                    tm.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            Debug.LogError("No free layer for " + name);
        }
    }
}

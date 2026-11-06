using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OnePairOfHands.EditorTools
{
    /// <summary>Minimap part of the UI builder: map camera, HUD panel, numbered task icons, Ataa's arrow.</summary>
    public static partial class OnePairOfHandsUIBuilder
    {
        private static Sprite circle, ring, arrowSprite;

        private static void BuildMinimapSprites()
        {
            circle = ShapeSprite("Circle", (x, y) => Disk(x, y, 30f));
            ring = ShapeSprite("Ring", (x, y) => Mathf.Min(Disk(x, y, 30f), 1f - Disk(x, y, 24f)));
            arrowSprite = ArrowSprite();
        }

        private static void BuildMinimap(GameObject hudGo)
        {
            var root = GameObject.Find("OnePairOfHands");
            var oldCam = root.transform.Find("MinimapCamera");
            if (oldCam != null) Object.DestroyImmediate(oldCam.gameObject);
            var oldMap = hudGo.transform.Find("Minimap");
            if (oldMap != null) Object.DestroyImmediate(oldMap.gameObject);
            var oldCtl = hudGo.GetComponent<Minimap>();
            if (oldCtl != null) Object.DestroyImmediate(oldCtl);

            // Keep world labels and the character off the map.
            int noMap = LayerMask.NameToLayer(OnePairOfHandsCharacterBuilder.NoMinimapLayer);
            var tasks = GameObject.Find("OnePairOfHands/Tasks");
            foreach (var l in tasks.GetComponentsInChildren<WorldLabel>(true)) l.gameObject.layer = noMap;

            // Orthographic, north-up camera framing home, river, bridge, well and the tent terrace.
            var camGo = new GameObject("MinimapCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = new Vector3(-88f, 60f, 149f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 58f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIPalette.DeepTealSolid;
            cam.depth = -10;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            // Ground, water, bridge, buildings, trees: everything except UI, triggers, FX and the NoMinimap layer.
            cam.cullingMask = ~((1 << 5) | (1 << 2) | (1 << 1) | (1 << noMap));
            var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderShadows = false;
            urp.renderPostProcessing = false;
            urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

            // Panel (top right, under the pause button)
            var panel = new GameObject("Minimap", typeof(RectTransform));
            panel.layer = 5;
            var prt = (RectTransform)panel.transform;
            prt.SetParent(hudGo.transform, false);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-24f, -112f);
            prt.sizeDelta = new Vector2(220f, 220f);
            var frame = panel.AddComponent<Image>();
            frame.sprite = rounded;
            frame.type = Image.Type.Sliced;
            frame.color = UIPalette.DeepTealSolid;
            frame.raycastTarget = true; // clicks on the map never fall through to the world

            var mrt = Stretch(prt, "MapMask", 6f);
            var mimg = mrt.gameObject.AddComponent<Image>();
            mimg.sprite = rounded;
            mimg.type = Image.Type.Sliced;
            mimg.raycastTarget = false;
            mrt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var maprt = Stretch(mrt, "Map", 0f);
            var raw = maprt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(0.95f, 0.92f, 0.86f); // a touch of warmth over the render

            var irt = Stretch(prt, "Icons", 6f);

            var numberMat = NumberMaterial();
            var manager = Object.FindFirstObjectByType<TaskManager>();
            var tasksProp = new SerializedObject(manager).FindProperty("tasks");
            var icons = new List<MinimapIcon>();
            for (int i = 0; i < tasksProp.arraySize; i++)
                icons.Add(BuildIcon(irt, (TaskState)tasksProp.GetArrayElementAtIndex(i).objectReferenceValue, numberMat));

            // Ataa's arrow, drawn under the icons so a task's number stays readable while she stands at it
            // (the current-task ring marks where she is then).
            var arrowGo = new GameObject("AtaaArrow", typeof(RectTransform));
            arrowGo.layer = 5;
            var art = (RectTransform)arrowGo.transform;
            art.SetParent(irt, false);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(28f, 28f);
            var ai = arrowGo.AddComponent<Image>();
            ai.sprite = arrowSprite;
            ai.raycastTarget = false;
            art.SetAsFirstSibling();

            // Hover label just under the map
            var hover = new GameObject("HoverLabel", typeof(RectTransform));
            hover.layer = 5;
            var hrt = (RectTransform)hover.transform;
            hrt.SetParent(prt, false);
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -6f);
            hrt.sizeDelta = new Vector2(90f, 44f);
            var hbg = hover.AddComponent<Image>();
            hbg.sprite = rounded;
            hbg.type = Image.Type.Sliced;
            hbg.color = UIPalette.DeepTeal;
            hbg.raycastTarget = false;
            var htxt = Label(hrt, "Text", "", 24, UIPalette.SandLight, TextAlignmentOptions.Center);
            var ht = htxt.rectTransform;
            ht.anchorMin = Vector2.zero;
            ht.anchorMax = Vector2.one;
            ht.offsetMin = ht.offsetMax = Vector2.zero;
            htxt.GetComponent<LayoutElement>().ignoreLayout = true;
            htxt.textWrappingMode = TextWrappingModes.NoWrap;
            hover.SetActive(false);

            // Controller lives on the always-on HUD so the panel can be switched off by the setting.
            var mm = hudGo.AddComponent<Minimap>();
            var so = new SerializedObject(mm);
            so.FindProperty("mapCamera").objectReferenceValue = cam;
            so.FindProperty("panel").objectReferenceValue = prt;
            so.FindProperty("mapImage").objectReferenceValue = raw;
            so.FindProperty("iconArea").objectReferenceValue = irt;
            so.FindProperty("arrow").objectReferenceValue = art;
            SetArray(so, "icons", icons.ToArray());
            so.FindProperty("hoverLabel").objectReferenceValue = htxt;
            var model = GameObject.Find("OnePairOfHands/Ataa/Ataa_Model");
            so.FindProperty("facingSource").objectReferenceValue = model != null ? model.transform : null;
            so.FindProperty("ataa").objectReferenceValue = Object.FindFirstObjectByType<AtaaController>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Stretch(RectTransform parent, string name, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        private static MinimapIcon BuildIcon(RectTransform parent, TaskState task, Material numberMat)
        {
            var go = new GameObject($"Icon_{task.Number}", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(40f, 40f);

            Image Img(string name, Sprite sp, Color c, float size, bool raycast)
            {
                var g = new GameObject(name, typeof(RectTransform));
                g.layer = 5;
                var r = (RectTransform)g.transform;
                r.SetParent(rt, false);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(size, size);
                var im = g.AddComponent<Image>();
                im.sprite = sp;
                im.color = c;
                im.raycastTarget = raycast;
                return im;
            }

            var current = Img("CurrentRing", ring, UIPalette.SandLight, 58f, false);
            Img("Base", circle, UIPalette.DeepTealSolid, 40f, true); // hit area
            var fill = Img("Fill", circle, UIPalette.Sand, 40f, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            Img("Outline", ring, UIPalette.Sand, 40f, false);

            var num = Label(rt, "Number", task.Number.ToString(), 24, UIPalette.SandLight, TextAlignmentOptions.Center, FontStyles.Bold);
            var nrt = num.rectTransform;
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            num.GetComponent<LayoutElement>().ignoreLayout = true;
            num.fontSharedMaterial = numberMat;
            num.textWrappingMode = TextWrappingModes.NoWrap;

            var icon = go.AddComponent<MinimapIcon>();
            var so = new SerializedObject(icon);
            so.FindProperty("task").objectReferenceValue = task;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("currentRing").objectReferenceValue = current;
            so.ApplyModifiedPropertiesWithoutUndo();
            return icon;
        }

        private static Material NumberMaterial()
        {
            const string path = "Assets/OnePairOfHands/Materials/UI_NumberOutline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(TMP_Settings.defaultFontAsset.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.3f);
            m.SetColor("_OutlineColor", new Color32(12, 40, 44, 255));
            m.SetFloat("_FaceDilate", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ generated sprites

        private static float Disk(int x, int y, float r)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        private static Sprite ShapeSprite(string name, System.Func<int, int, float> alpha)
        {
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    px[y * 64 + x] = new Color(1f, 1f, 1f, alpha(x, y));
            return SaveSprite(name, px);
        }

        /// <summary>Arrow pointing up (north): sand-light with a deep-teal edge so it reads on any ground.</summary>
        private static Sprite ArrowSprite()
        {
            Vector2 a = new Vector2(32, 60), b = new Vector2(8, 6), c = new Vector2(32, 20), d = new Vector2(56, 6);
            float Inside(Vector2 p) => Mathf.Max(Tri(p, a, b, c), Tri(p, a, c, d));
            var px = new Color32[64 * 64];
            Color fillC = UIPalette.SandLight, edge = UIPalette.DeepTealSolid;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float outer = Inside(p);
                    float inner = Mathf.Min(Mathf.Min(Inside(p + new Vector2(4, 0)), Inside(p - new Vector2(4, 0))),
                                            Mathf.Min(Inside(p + new Vector2(0, 4)), Inside(p - new Vector2(0, 4))));
                    var col = Color.Lerp(edge, fillC, inner);
                    col.a = outer;
                    px[y * 64 + x] = col;
                }
            return SaveSprite("Arrow", px);
        }

        private static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float E(Vector2 u, Vector2 v) => (v.x - u.x) * (p.y - u.y) - (v.y - u.y) * (p.x - u.x);
            float e1 = E(a, b), e2 = E(b, c), e3 = E(c, a);
            return (e1 >= 0 && e2 >= 0 && e3 >= 0) || (e1 <= 0 && e2 <= 0 && e3 <= 0) ? 1f : 0f;
        }

        private static Sprite SaveSprite(string name, Color32[] px)
        {
            string path = $"{UIFolder}/{name}.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

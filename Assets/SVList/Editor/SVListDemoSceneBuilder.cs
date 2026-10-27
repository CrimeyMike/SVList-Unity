using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using SVList;
using SVList.Examples;

namespace SVList.EditorTools
{
    /// <summary>
    /// 一键（重新）生成聊天演示场景：
    /// 真实层级 + ChatItemExample 预制体 + 场景内接好线的按钮/驱动脚本。
    /// 场景被误改后可用此菜单重建。菜单：Tools → SVList → 重建聊天演示场景
    /// </summary>
    public static class SVListDemoSceneBuilder
    {
        private const string SceneRelPath = "Examples/Scenes/SVListChatDemo.unity";
        private const string PrefabRelPath = "Examples/Prefabs/ChatItemExample.prefab";
        private const string ImagePrefabRelPath = "Examples/Prefabs/ImageMessageItem.prefab";
        private const string FontAssetRelPath = "Examples/Fonts/ChatFont SDF.asset";

        private static string _packageRoot;

        /// <summary>包根（含 Runtime/Editor/Examples 的目录）。从本脚本自身位置推导，
        /// 包放在 Assets 下任意路径（如 Assets/SVList 或 Assets/ThirdParty/SVList）都能定位。</summary>
        private static string PackageRoot
        {
            get
            {
                if (_packageRoot != null) return _packageRoot;
                foreach (var guid in AssetDatabase.FindAssets("SVListDemoSceneBuilder t:Script"))
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(guid); // <root>/Editor/SVListDemoSceneBuilder.cs
                    var editorDir = scriptPath.Substring(0, scriptPath.LastIndexOf('/'));
                    _packageRoot = editorDir.Substring(0, editorDir.LastIndexOf('/'));
                    return _packageRoot;
                }
                Debug.LogError("[SVList] 定位不到 SVListDemoSceneBuilder 脚本自身，无法推导包根路径。");
                return null;
            }
        }

        private static string ScenePath => PackageRoot + "/" + SceneRelPath;
        private static string PrefabPath => PackageRoot + "/" + PrefabRelPath;
        private static string ImagePrefabPath => PackageRoot + "/" + ImagePrefabRelPath;
        private static string FontAssetPath => PackageRoot + "/" + FontAssetRelPath;

        [MenuItem("Tools/SVList/重建聊天演示场景", false, 100)]
        public static void Rebuild()
        {
            if (PackageRoot == null) return;
            EnsureFolders();

            var fontAsset = LoadOrCreateFontAsset();
            var prefab = LoadOrCreateItemPrefab(fontAsset);
            if (fontAsset == null || prefab == null)
            {
                Debug.LogError("[SVList] 演示场景构建失败：字体或预制体创建失败，见上方日志。");
                return;
            }

            // 场景不存在（包刚导入全新项目）时新建，避免 OpenScene 报错
            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearOld(scene);

            var driver = BuildScene(scene, fontAsset, prefab);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = driver.gameObject;

            Debug.Log($"[SVList] 演示场景已重建并保存：{ScenePath}\n" +
                      "打开后直接 Play 即可；Item 模板是真实预制体 " + PrefabPath);
        }

        #region 资产准备

        private static void EnsureFolders()
        {
            EnsureFolder(PackageRoot, "Examples");
            EnsureFolder(PackageRoot + "/Examples", "Prefabs");
            EnsureFolder(PackageRoot + "/Examples", "Fonts");
            EnsureFolder(PackageRoot + "/Examples", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>从项目内找一个可用的源 Font 生成 TMP 字体资产。
        /// 不硬编码字体路径（那会把包绑死在某个工程上）；跳过 TMP 内置的西文字体（中文会显示方框）。</summary>
        private static Font FindFallbackFont()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Font"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("TextMesh Pro")) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }
            return null;
        }

        /// <summary>从项目 TTF 生成（或加载已有的）TMP 动态字体资产，中文按需光栅化。
        /// 注意：material 与 atlasTexture 必须一并存为子资产，否则加载后引用丢失。</summary>
        private static TMP_FontAsset LoadOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && existing.atlasTexture != null) return existing;

            var source = FindFallbackFont();
            if (source == null)
            {
                Debug.LogError("[SVList] 项目内找不到任何 Font 资产，无法生成 TMP 演示字体。" +
                               "请先把一个 .ttf/.otf（演示含中文时需中文字体）导入项目再重试。");
                return null;
            }
            Debug.Log($"[SVList] 包内字体资产缺失，已用项目字体生成：{AssetDatabase.GetAssetPath(source)}" +
                      "（若演示出现中文方框，请换用含中文的字体后删除 ChatFont SDF.asset 再重建）。");

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source, 36, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            var atlas = fontAsset.atlasTexture;
            if (atlas != null)
            {
                atlas.name = "ChatFont SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SVList] TMP 动态字体已生成：{FontAssetPath}（atlas={(atlas != null ? "已保存" : "缺失!")}）");
            return fontAsset;
        }

        /// <summary>加载已有的聊天 Item 预制体；不存在时构建（保证引用稳定，不覆盖）</summary>
        private static GameObject LoadOrCreateItemPrefab(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            return BuildChatItemPrefab(font);
        }

        /// <summary>构建（覆盖保存）文本消息 Item 预制体。
        /// 静态布局即所见：Name 顶部整行、Bubble 左缘竖拉伸锚（="他人消息"形态，宽度 BubbleWidth）、
        /// Msg 在 Bubble 内按内边距拉伸 —— prefab 预览即正确结构；
        /// IsSelf 的左右翻转属于运行时状态（ChatItemExample.OnBind），不在静态布局里。
        /// 供「重建聊天演示场景」与「重建列表模板」共用。</summary>
        public static GameObject BuildChatItemPrefab(TMP_FontAsset font)
        {
            var item = new GameObject("ChatItemExample", typeof(RectTransform));

            var name = UiText("Name", item.transform, "", ChatExampleMetrics.NameFontSize, new Color(0.6f, 0.75f, 0.95f), font);
            SetRect(name.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -36f), new Vector2(-28f, -4f));

            var bubbleGo = new GameObject("Bubble", typeof(RectTransform));
            bubbleGo.transform.SetParent(item.transform, false);
            var bubble = bubbleGo.AddComponent<Image>();
            bubble.color = new Color(0.16f, 0.19f, 0.24f);
            bubble.raycastTarget = false;
            SetRect(bubble.rectTransform,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(24f, 0f),
                new Vector2(24f + ChatExampleMetrics.BubbleWidth, -ChatExampleMetrics.NameRowHeight));

            var msg = UiText("Msg", bubbleGo.transform, "", ChatExampleMetrics.MsgFontSize, Color.white, font);
            msg.enableWordWrapping = true;
            SetRect(msg.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-16f, -10f));

            var comp = item.AddComponent<ChatItemExample>();
            comp.NameText = name;
            comp.MsgText = msg;
            comp.BubbleRect = bubbleGo.GetComponent<RectTransform>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(item, PrefabPath);
            Object.DestroyImmediate(item);
            return prefab;
        }

        /// <summary>构建（覆盖保存）图片消息 Item 预制体（模拟表情/图片发送）。
        /// 静态布局即所见：Name 顶部整行、Picture 左侧定点锚（="他人消息"形态，固定 ImageSize×ImageSize）。
        /// IsSelf 的左右翻转属于运行时状态（ImageMessageItem.OnBind）。</summary>
        public static GameObject BuildImageMessagePrefab(TMP_FontAsset font)
        {
            var item = new GameObject("ImageMessageItem", typeof(RectTransform));

            var name = UiText("Name", item.transform, "", ChatExampleMetrics.NameFontSize, new Color(0.6f, 0.75f, 0.95f), font);
            SetRect(name.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -36f), new Vector2(-28f, -4f));

            var picGo = new GameObject("Picture", typeof(RectTransform));
            picGo.transform.SetParent(item.transform, false);
            var picture = picGo.AddComponent<Image>();
            picture.color = new Color(0.30f, 0.50f, 0.80f);
            picture.raycastTarget = false;
            var picRect = picGo.GetComponent<RectTransform>();
            picRect.anchorMin = new Vector2(0f, 0.5f);
            picRect.anchorMax = new Vector2(0f, 0.5f);
            picRect.pivot = new Vector2(0f, 0.5f);
            picRect.anchoredPosition = new Vector2(24f, -22f);
            picRect.sizeDelta = new Vector2(ChatExampleMetrics.ImageSize, ChatExampleMetrics.ImageSize);

            var comp = item.AddComponent<ImageMessageItem>();
            comp.NameText = name;
            comp.Picture = picture;

            var prefab = PrefabUtility.SaveAsPrefabAsset(item, ImagePrefabPath);
            Object.DestroyImmediate(item);
            return prefab;
        }

        #endregion

        #region 场景构建

        private static void ClearOld(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "ChatDemo" || root.name == "DemoCanvas" || root.name == "EventSystem")
                    Object.DestroyImmediate(root);
            }
        }

        private static SVListChatDemo BuildScene(Scene scene, TMP_FontAsset font, GameObject itemPrefab)
        {
            _ = scene;

            // Canvas
            var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasT = canvasGo.GetComponent<RectTransform>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // 背景与标题
            Stretch(Bg(canvasT.transform, new Color(0.09f, 0.10f, 0.13f)).rectTransform);
            var title = UiText("Title", canvasT, "SVList 联盟聊天 Demo", 40, new Color(1f, 0.85f, 0.3f), font);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -84f), new Vector2(-24f, -20f));
            var sub = UiText("Sub", canvasT, "不等高消息 · 顶部加载历史不跳屏 · 底部自动跟滚", 26, new Color(0.7f, 0.75f, 0.8f), font);
            SetRect(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -118f), new Vector2(-24f, -88f));
            var stats = UiText("Stats", canvasT, "", 24, new Color(0.65f, 0.85f, 1f), font);
            SetRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -148f), new Vector2(-24f, -118f));

            // 列表层级（结构与 GameObject/UI/SVList 菜单一致）
            var listGo = new GameObject("SVList_Chat", typeof(RectTransform), typeof(ScrollRect));
            var listRect = listGo.GetComponent<RectTransform>();
            listRect.SetParent(canvasT, false);
            Stretch(listRect);
            listRect.offsetMin = new Vector2(12f, 150f);
            listRect.offsetMax = new Vector2(-12f, -128f);

            var scroll = listGo.GetComponent<ScrollRect>();
            scroll.vertical = true;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;

            // Viewport：RectMask2D 裁剪 + 透明 Image 承接射线（否则拖不动，见 README FAQ）
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var vpRect = vpGo.GetComponent<RectTransform>();
            vpRect.SetParent(listRect, false);
            Stretch(vpRect);
            var vpImage = vpGo.GetComponent<Image>();
            vpImage.color = new Color(0f, 0f, 0f, 0f);
            vpImage.raycastTarget = true;
            scroll.viewport = vpRect;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.SetParent(vpRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            scroll.content = contentRect;

            var view = listGo.AddComponent<SVListView>();
            view.SetConfig(new SVConfig
            {
                Direction = SVDirection.Vertical,
                DynamicHeight = true,
                EstimateHeight = 90f,
                Spacing = 10f,
                PaddingTop = 12f,
                PaddingBottom = 12f,
                PreWarmCount = 16,
                MaxInstantiatePerFrame = 30,
                JumpDuration = 0.18f,
            });
            var so = new SerializedObject(view);
            var prefabs = so.FindProperty("_itemPrefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = itemPrefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            // 隐藏测量文本（字体/字号与消息文本一致，供数据源预计算高度）。
            // 注意：不能 SetActive(false) —— 场景加载时 inactive 的 TMP 不会跑 Awake/OnEnable，
            // 内部状态未初始化，GetPreferredValues 会返回错误值。
            // 改为保持激活：移出画布 + 全透明 + 不挡射线。
            var measureGo = new GameObject("MeasureText", typeof(RectTransform));
            measureGo.transform.SetParent(canvasT, false);
            var measureRect = measureGo.GetComponent<RectTransform>();
            measureRect.anchorMin = new Vector2(0.5f, 0.5f);
            measureRect.anchorMax = new Vector2(0.5f, 0.5f);
            measureRect.anchoredPosition = new Vector2(0f, -99999f); // 视野外
            measureRect.sizeDelta = new Vector2(ChatExampleMetrics.TextWidth, 40f);
            var measure = measureGo.AddComponent<TextMeshProUGUI>();
            measure.font = font;
            measure.fontSize = ChatExampleMetrics.MsgFontSize;
            measure.enableWordWrapping = true;
            measure.color = new Color(0f, 0f, 0f, 0f);
            measure.raycastTarget = false;

            // 驱动脚本
            var driverGo = new GameObject("ChatDemo");
            var driver = driverGo.AddComponent<SVListChatDemo>();
            driver.View = view;
            driver.StatsText = stats;
            driver.MeasureText = measure;
            EditorUtility.SetDirty(driver);

            // 按钮（持久监听直接存进场景）
            MakeButton(canvasT, "Btn_Send", "发送(随机)", -350f, driver.SendRandomMessage, font);
            MakeButton(canvasT, "Btn_Prepend", "顶部加载30条", 0f, driver.PrependHistoryPage, font);
            MakeButton(canvasT, "Btn_Bottom", "回到底部", 350f, driver.JumpToBottom, font);

            return driver;
        }

        private static void MakeButton(Transform canvasT, string goName, string label, float x,
            UnityEngine.Events.UnityAction call, TMP_FontAsset font)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasT, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(330f, 84f);

            go.GetComponent<Image>().color = new Color(0.22f, 0.32f, 0.48f);

            var text = UiText("Label", rt, label, 30, Color.white, font);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            var btn = go.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, call);
        }

        #endregion

        #region UI 小工具

        private static TMP_Text UiText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static Image Bg(Transform parent, Color color)
        {
            var go = new GameObject("BG", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        #endregion
    }
}

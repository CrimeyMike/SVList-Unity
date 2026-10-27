using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SVList;
using SVList.Examples;

namespace SVList.EditorTools
{
    /// <summary>
    /// 生成/重建 Examples/Templates 下的三个开箱即用列表模板：
    /// VerticalListTemplate / HorizontalListTemplate（等高，SimpleListItem）
    /// ChatListTemplate（不等高，ChatItemExample）。
    /// 模板被误改后可用菜单 Tools → SVList → 重建列表模板 一键重建。
    /// 所有路径从本脚本位置推导包根（与 SVListDemoSceneBuilder 一致），兼容任意安装路径。
    /// </summary>
    public static class SVListTemplateGenerator
    {
        private const string TemplatesDir = "Examples/Templates";
        private const string SimpleItemRelPath = TemplatesDir + "/SimpleListItem.prefab";
        private const string ChatItemRelPath = "Examples/Prefabs/ChatItemExample.prefab";
        private const string FontAssetRelPath = "Examples/Fonts/ChatFont SDF.asset";

        private static string _packageRoot;

        private static string PackageRoot
        {
            get
            {
                if (_packageRoot != null) return _packageRoot;
                foreach (var guid in AssetDatabase.FindAssets("SVListTemplateGenerator t:Script"))
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(guid); // <root>/Editor/SVListTemplateGenerator.cs
                    var editorDir = scriptPath.Substring(0, scriptPath.LastIndexOf('/'));
                    _packageRoot = editorDir.Substring(0, editorDir.LastIndexOf('/'));
                    return _packageRoot;
                }
                Debug.LogError("[SVList] 定位不到 SVListTemplateGenerator 脚本自身，无法推导包根路径。");
                return null;
            }
        }

        [MenuItem("Tools/SVList/重建列表模板", false, 101)]
        public static void Rebuild()
        {
            if (PackageRoot == null) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PackageRoot + "/" + FontAssetRelPath);
            if (font == null)
            {
                Debug.LogError($"[SVList] 找不到包内字体资产 {FontAssetRelPath}，无法生成模板。");
                return;
            }

            EnsureFolders();

            var simpleItem = BuildSimpleItemPrefab(font);

            var chatItem = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "/" + ChatItemRelPath);
            if (chatItem == null)
            {
                Debug.LogError($"[SVList] 找不到 {ChatItemRelPath}，可先用菜单「重建聊天演示场景」生成后重试。");
                return;
            }

            BuildTemplate("VerticalListTemplate", SVDirection.Vertical, chat: false, simpleItem);
            BuildTemplate("HorizontalListTemplate", SVDirection.Horizontal, chat: false, simpleItem);
            BuildTemplate("ChatListTemplate", SVDirection.Vertical, chat: true, chatItem);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SVList] 列表模板已生成：{PackageRoot}/{TemplatesDir}\n" +
                      "把模板 prefab 拖进 Canvas 下直接 Play 即可；改数据请看同目录驱动脚本。");
        }

        #region 资产构建

        private static void EnsureFolders()
        {
            EnsureFolder(PackageRoot, "Examples");
            EnsureFolder(PackageRoot + "/Examples", "Templates");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>通用等高 Item：LayoutElement 定尺寸（垂直高 90 / 水平宽 320）+ 背景 + 文本</summary>
        private static GameObject BuildSimpleItemPrefab(TMP_FontAsset font)
        {
            var item = new GameObject("SimpleListItem", typeof(RectTransform));

            var layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;
            layout.preferredWidth = 320f;

            var bgGo = new GameObject("BG", typeof(RectTransform));
            bgGo.transform.SetParent(item.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.19f, 0.24f);
            bg.raycastTarget = false;
            Stretch(bg.rectTransform);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(bgGo.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.font = font;
            title.fontSize = 34f;
            title.color = Color.white;
            title.raycastTarget = false;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(28f, 0f);
            titleRect.offsetMax = new Vector2(-28f, 0f);

            var comp = item.AddComponent<SimpleListItem>();
            var so = new SerializedObject(comp);
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_background").objectReferenceValue = bg;
            so.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(item, PackageRoot + "/" + SimpleItemRelPath);
            Object.DestroyImmediate(item);
            return prefab;
        }

        /// <summary>完整列表模板：层级 + 配置 + Item Prefab 已挂 + 驱动脚本已接，拖进 Canvas 即 Play</summary>
        private static void BuildTemplate(string name, SVDirection direction, bool chat, GameObject itemPrefab)
        {
            bool isVertical = direction != SVDirection.Horizontal;

            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            var rootRect = go.GetComponent<RectTransform>();
            Stretch(rootRect); // 拖到 Canvas 下自动铺满父级

            var scroll = go.GetComponent<ScrollRect>();
            scroll.vertical = isVertical;
            scroll.horizontal = !isVertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;

            // Viewport：RectMask2D 裁剪 + 透明 Image 承接射线（否则拖不动，见包内 README FAQ）
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var vpRect = vpGo.GetComponent<RectTransform>();
            vpRect.SetParent(rootRect, false);
            Stretch(vpRect);
            var vpImage = vpGo.GetComponent<Image>();
            vpImage.color = new Color(0f, 0f, 0f, 0f);
            vpImage.raycastTarget = true;
            scroll.viewport = vpRect;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.SetParent(vpRect, false);
            if (isVertical)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                contentRect.anchorMin = new Vector2(0f, 0f);
                contentRect.anchorMax = new Vector2(0f, 1f);
                contentRect.pivot = new Vector2(0f, 0.5f);
            }
            contentRect.sizeDelta = Vector2.zero;
            scroll.content = contentRect;

            var view = go.AddComponent<SVListView>();
            var cfg = new SVConfig { Direction = direction };
            if (chat)
            {
                cfg.DynamicHeight = true;
                cfg.EstimateHeight = 90f;
                cfg.Spacing = 10f;
                cfg.PaddingTop = 12f;
                cfg.PaddingBottom = 12f;
            }
            else
            {
                cfg.DynamicHeight = false;
                cfg.EstimateHeight = 90f;          // 垂直：条目高，与 SimpleListItem.LayoutElement 一致
                cfg.DefaultItemSize = 320f;        // 水平：条目宽，与 SimpleListItem.LayoutElement 一致
                cfg.Spacing = 8f;
            }
            view.SetConfig(cfg);

            var so = new SerializedObject(view);
            var prefabs = so.FindProperty("_itemPrefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = itemPrefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            MonoBehaviour driver;
            if (chat) driver = go.AddComponent<ChatListDriver>();
            else driver = go.AddComponent<SimpleListDriver>();
            var dso = new SerializedObject(driver);
            dso.FindProperty("_view").objectReferenceValue = view;
            dso.ApplyModifiedProperties();
            EditorUtility.SetDirty(driver);

            PrefabUtility.SaveAsPrefabAsset(go, PackageRoot + "/" + TemplatesDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion
    }
}

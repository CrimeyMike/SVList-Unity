using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SVList.EditorTools
{
    /// <summary>
    /// 一键创建 SVList 标准层级（Hierarchy 右键 → UI → SVList）
    ///
    /// 生成结构（以垂直为例，锚点已按库要求搭好）：
    /// SVList_Vertical (ScrollRect + SVListView + SVScrollController[运行时自动])
    /// └─ Viewport (RectMask2D)
    ///    └─ Content (顶部锚定/pivot 顶部)
    /// </summary>
    public static class SVListHierarchyMenu
    {
        [MenuItem("GameObject/UI/SVList/垂直列表（通用）", false, 10)]
        public static void CreateVerticalList() => Create("SVList_Vertical", SVDirection.Vertical, chatPreset: false);

        [MenuItem("GameObject/UI/SVList/聊天列表（不等高·动态高度）", false, 11)]
        public static void CreateChatList() => Create("SVList_Chat", SVDirection.Vertical, chatPreset: true);

        [MenuItem("GameObject/UI/SVList/水平列表", false, 12)]
        public static void CreateHorizontalList() => Create("SVList_Horizontal", SVDirection.Horizontal, chatPreset: false);

        private static void Create(string name, SVDirection direction, bool chatPreset)
        {
            var parent = Selection.activeGameObject != null
                ? Selection.activeGameObject.transform as RectTransform
                : null;

            if (parent == null)
            {
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    canvas = canvasGo.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    var scaler = canvasGo.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080f, 1920f);

                    if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                    {
                        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                            typeof(UnityEngine.EventSystems.StandaloneInputModule));
                    }

                    Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                }
                parent = canvas.transform as RectTransform;
            }

            // 根节点：ScrollRect + SVListView
            var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            StretchFull(rootRect);

            var scroll = root.GetComponent<ScrollRect>();
            bool isVertical = direction != SVDirection.Horizontal;
            scroll.vertical = isVertical;
            scroll.horizontal = !isVertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;

            // Viewport：RectMask2D 裁剪 + 透明 Image 承接射线。
            // ScrollRect 的拖动依赖 EventSystem 射线，Viewport 内必须存在
            // raycastTarget=true 的 Graphic（RectMask2D 本身不参与射线检测），
            // 否则列表无法拖动。
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var vpRect = vpGo.GetComponent<RectTransform>();
            vpRect.SetParent(rootRect, false);
            StretchFull(vpRect);
            var vpImage = vpGo.GetComponent<Image>();
            vpImage.color = new Color(0f, 0f, 0f, 0f);
            vpImage.raycastTarget = true;
            scroll.viewport = vpRect;

            // Content（锚点是接入最容易错的一步，这里按方向搭好）
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

            // SVListView + 预设
            var view = root.AddComponent<SVListView>();
            var cfg = new SVConfig { Direction = direction };
            if (chatPreset)
            {
                cfg.DynamicHeight = true;
                cfg.EstimateHeight = 90f;
                cfg.Spacing = 10f;
                cfg.PaddingTop = 12f;
                cfg.PaddingBottom = 12f;
            }
            view.SetConfig(cfg);

            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            Undo.SetTransformParent(rootRect, parent, "Parent " + name);
            Selection.activeGameObject = root;

            Debug.Log($"[SVList] 已创建 {name}。下一步：\n" +
                      "1. 写一个继承 SVItemBase<T> 的 Item 脚本挂到模板预制体上（只需重写 OnBind）\n" +
                      "2. 把预制体拖进 SVListView 的 Item Prefabs（多模板按顺序，PrefabSelector 返回下标）\n" +
                      "3. 代码里 view.Initialize(数据源) —— 详细见包内 README.md；开箱即用模板见 Examples/Templates/");
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SVList.EditorTools
{
    /// <summary>
    /// 设计时预览的渲染后端(v1.3.0):
    /// 基于 UnityEditor.PreviewRenderUtility(官方编辑器预览管线,内部托管 preview scene、
    /// 相机与 RenderTexture 生命周期——编辑器模式下手动 camera.Render() 不可靠,勿自建)。
    /// 实例化 Item prefab 并注入测试数据,收集实测高度与元素几何供窗口叠加标注。
    /// </summary>
    internal sealed class SVItemPreviewScene : System.IDisposable
    {
        public struct ElementRect
        {
            public RectTransform Rt;
            public bool IsRoot;
            public bool OverRoot;
            public bool Overlap;
        }

        public struct ItemInfo
        {
            public RectTransform Root;
            public float PreferredHeight;
            public List<ElementRect> Elements;
            public string BindError;
        }

        private readonly UnityEditor.PreviewRenderUtility _pru;
        private readonly Canvas _canvas;
        private readonly List<ItemInfo> _items = new List<ItemInfo>();
        private RenderTexture _last;
        private Rect _lastRect;

        public RenderTexture Texture => _last;
        public IReadOnlyList<ItemInfo> Items => _items;
        public float CanvasWidth { get; private set; } = 720f;

        public SVItemPreviewScene()
        {
            _pru = new UnityEditor.PreviewRenderUtility();
            var cam = _pru.camera;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.075f, 0.09f, 1f);
            cam.cullingMask = 1 << 22;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            var canvasGo = new GameObject("SVList_PreviewCanvas");
            canvasGo.layer = 22;
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1080f, 800f);
            _pru.AddSingleGO(canvasGo); // 进入 PRU 托管的 preview scene
        }

        /// <summary>用给定 prefab 与测试数据重建预览内容(不渲染,渲染在 Render(rect))。</summary>
        public void Build(GameObject prefabAsset, float panelWidth, IList<object> testDatas)
        {
            ClearItems();
            CanvasWidth = panelWidth;

            float y = 0f;
            for (int i = 0; i < testDatas.Count; i++)
            {
                var inst = Object.Instantiate(prefabAsset);
                SetLayerRecursive(inst.transform);
                _pru.AddSingleGO(inst);

                var root = inst.transform as RectTransform;
                if (root == null) { Object.DestroyImmediate(inst); continue; }

                root.SetParent(_canvas.transform, false);
                root.anchorMin = new Vector2(0.5f, 1f);
                root.anchorMax = new Vector2(0.5f, 1f);
                root.pivot = new Vector2(0.5f, 1f);
                root.anchoredPosition = new Vector2(0f, -y);
                root.sizeDelta = new Vector2(panelWidth, 90f);

                ForceLayout(root);

                var info = new ItemInfo { Root = root, Elements = new List<ElementRect>() };
                var rendererComp = FindRenderer(inst);
                if (rendererComp != null)
                {
                    try
                    {
                        rendererComp.OnBind(testDatas[i], i);
                        ForceLayout(root);
                        float h = rendererComp.GetPreferredHeight();
                        if (h > 0f)
                        {
                            root.sizeDelta = new Vector2(panelWidth, h);
                            ForceLayout(root);
                            info.PreferredHeight = h;
                        }
                    }
                    catch (System.Exception e) { info.BindError = e.Message; }
                }
                else info.BindError = "未找到 ISVItemRenderer(挂 SVItemBase<T> 子类?)";

                CollectGeometry(ref info);
                _items.Add(info);
                y += root.rect.height + 16f;
            }
        }

        /// <summary>渲染到给定视口尺寸,返回 RT(PRU 托管,调用方不要释放)。
        /// URP 下手动 camera.Render() 走内置管线路径会静默黑屏,须走
        /// UniversalRenderPipeline.RenderSingleCamera(条件编译,内置管线项目不受影响)。</summary>
        public RenderTexture Render(Rect viewportRect)
        {
            float worldH = Mathf.Max(200f, ContentHeight());
            var cam = _pru.camera;
            cam.aspect = viewportRect.width / Mathf.Max(1f, viewportRect.height);
            cam.orthographicSize = worldH * 0.5f;
            cam.transform.position = new Vector3(0f, -worldH * 0.5f, -100f);

            _pru.BeginPreview(viewportRect, GUIStyle.none);
#if UNITY_URP_PRESENT
            var ctx = new UnityEngine.Rendering.ScriptableRenderContext();
            UnityEngine.Rendering.Universal.UniversalRenderPipeline.RenderSingleCamera(ctx, cam);
#else
            _pru.camera.Render();
#endif
            _last = (RenderTexture)_pru.EndPreview();
            _lastRect = viewportRect;
            return _last;
        }

        /// <summary>世界坐标 → 视口像素坐标(叠加绘制标注用)。</summary>
        public Vector2 WorldToPixel(Vector3 world)
        {
            var vp = _pru.camera.WorldToViewportPoint(world);
            return new Vector2(vp.x * _lastRect.width, (1f - vp.y) * _lastRect.height);
        }

        public float ContentHeight()
        {
            float bottom = 0f;
            foreach (var it in _items)
            {
                if (it.Root == null) continue;
                float b = -it.Root.anchoredPosition.y + it.Root.rect.height;
                if (b > bottom) bottom = b;
            }
            return bottom + 8f;
        }

        public void Dispose()
        {
            ClearItems();
            _pru.Cleanup();
        }

        // ---- 内部 ----

        private void ClearItems()
        {
            if (_canvas == null) return;
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            {
                var c = _canvas.transform.GetChild(i);
                Object.DestroyImmediate(c.gameObject);
            }
            _items.Clear();
        }

        private static void SetLayerRecursive(Transform t)
        {
            t.gameObject.layer = 22;
            foreach (Transform c in t) SetLayerRecursive(c);
        }

        private static ISVItemRenderer FindRenderer(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is ISVItemRenderer r && mb.gameObject == go) return r;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is ISVItemRenderer r) return r;
            return null;
        }

        private static void ForceLayout(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private void CollectGeometry(ref ItemInfo info)
        {
            var root = info.Root;
            info.Elements.Add(new ElementRect { Rt = root, IsRoot = true });

            var rootCorners = new Vector3[4];
            root.GetWorldCorners(rootCorners);
            float x0 = rootCorners[0].x, x1 = rootCorners[2].x, y0 = rootCorners[0].y, y1 = rootCorners[2].y;
            const float pad = 1.5f;

            var sibs = new List<RectTransform>();
            foreach (Transform c in root) sibs.Add(c as RectTransform);

            foreach (Transform c in root)
            {
                if (!(c is RectTransform crt)) continue;

                var corners = new Vector3[4];
                crt.GetWorldCorners(corners);
                bool over = corners[0].x < x0 - pad || corners[2].x > x1 + pad ||
                            corners[0].y < y0 - pad || corners[2].y > y1 + pad;

                bool overlap = false;
                foreach (var other in sibs)
                {
                    if (other == crt || other == null) continue;
                    var oc = new Vector3[4];
                    other.GetWorldCorners(oc);
                    bool ovl = corners[0].x < oc[2].x - pad && corners[2].x > oc[0].x + pad &&
                               corners[0].y < oc[2].y - pad && corners[2].y > oc[0].y + pad;
                    if (ovl) { overlap = true; break; }
                }

                info.Elements.Add(new ElementRect { Rt = crt, IsRoot = false, OverRoot = over, Overlap = overlap });
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SVList.EditorTools
{
    /// <summary>
    /// 设计时预览的渲染后端(v1.3.0 一期):
    /// 自建 隐藏相机 + RenderTexture + WorldSpace Canvas,实例化 Item prefab 并注入测试数据,
    /// 收集每个条目的实测高度与元素几何(供窗口叠加绘制边界/越界/重叠标注)。
    /// 所有对象 hideFlags=HideAndDontSave,不进用户场景/不产生资产文件(零污染)。
    /// </summary>
    internal sealed class SVItemPreviewScene : System.IDisposable
    {
        public struct ElementRect
        {
            public RectTransform Rt;
            public bool IsRoot;
            public bool OverRoot;   // 超出条目根矩形
            public bool Overlap;    // 与兄弟元素重叠
        }

        public struct ItemInfo
        {
            public RectTransform Root;
            public float PreferredHeight;   // ISVItemRenderer.GetPreferredHeight()(与运行时兜底同源)
            public List<ElementRect> Elements;
            public string BindError;
        }

        private Camera _camera;
        private Canvas _canvas;
        private readonly RectTransform _stack;
        private RenderTexture _rt;
        private readonly List<ItemInfo> _items = new List<ItemInfo>();

        private const int RtWidth = 560;
        private const float CanvasHeight = 800f;

        public RenderTexture Texture => _rt;
        public IReadOnlyList<ItemInfo> Items => _items;
        public float CanvasWidth { get; private set; } = 560f;

        public SVItemPreviewScene()
        {
            var camGo = new GameObject("SVList_PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.075f, 0.075f, 0.09f, 1f);
            _camera.cullingMask = 1 << 22; // 独立层(LayerMask 自选的预览层,不与用户场景冲突)
            camGo.layer = 22;

            var canvasGo = new GameObject("SVList_PreviewCanvas") { hideFlags = HideFlags.HideAndDontSave };
            canvasGo.layer = 22;
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(RtWidth, CanvasHeight);
            // 顶边对齐 y=0:条目从画布顶锚(y=0)向下生长到负值,
            // Render() 的相机视野 y∈[-worldH,0] 才能恒覆盖内容带。
            // (此前画布中心在原点、内容悬在 y>0 上半区,短内容整条漏出相机视野→画布空白)
            rt.position = new Vector3(0f, -CanvasHeight * 0.5f, 0f);
            _stack = rt;
        }

        /// <summary>用给定 prefab 与测试数据重建预览内容。</summary>
        public void Build(GameObject prefabAsset, float panelWidth, IList<object> testDatas)
        {
            ClearItems();
            CanvasWidth = panelWidth;

            float y = 0f;
            for (int i = 0; i < testDatas.Count; i++)
            {
                var inst = Object.Instantiate(prefabAsset, _stack);
                inst.hideFlags = HideFlags.HideAndDontSave;
                inst.layer = 22;
                SetLayerRecursive(inst.transform, 22);

                var root = inst.transform as RectTransform;
                if (root == null) { Object.DestroyImmediate(inst); continue; }

                // 模拟列表系统对根的控制:宽度=面板宽、顶部锚定、从上往下排
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
                    catch (System.Exception e)
                    {
                        info.BindError = e.Message;
                    }
                }
                else
                {
                    info.BindError = "未找到 ISVItemRenderer(挂 SVItemBase<T> 子类?)";
                }

                CollectGeometry(ref info);
                _items.Add(info);
                y += root.rect.height + 16f;
            }

            Render();
        }

        public void Render()
        {
            // 先解绑再释放,否则触发 "Releasing render texture that is set as Camera.targetTexture!"
            _camera.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }

            float worldH = Mathf.Max(400f, ContentHeight());
            _rt = new RenderTexture(RtWidth, Mathf.Max(400, Mathf.RoundToInt(worldH * RtWidth / CanvasWidth)), 0, RenderTextureFormat.ARGB32)
            { hideFlags = HideFlags.HideAndDontSave };

            _camera.targetTexture = _rt;
            _camera.aspect = (float)RtWidth / _rt.height;
            _camera.orthographicSize = worldH * 0.5f;
            _camera.transform.position = new Vector3(0f, -worldH * 0.5f, -100f);
            _camera.Render();
        }

        /// <summary>世界坐标 → RT 像素坐标(叠加绘制标注用)。</summary>
        public Vector2 WorldToPixel(Vector3 world)
        {
            var vp = _camera.WorldToViewportPoint(world);
            return new Vector2(vp.x * RtWidth, (1f - vp.y) * _rt.height);
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
            // 与 Render() 同规矩:先解绑再释放,避免 "Releasing render texture that is set as Camera.targetTexture!"
            if (_camera != null) _camera.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
        }

        // ---- 内部 ----

        private void ClearItems()
        {
            if (_stack == null) return;
            for (int i = _stack.childCount - 1; i >= 0; i--)
            {
                var c = _stack.GetChild(i);
                Object.DestroyImmediate(c.gameObject);
            }
            _items.Clear();
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            foreach (Transform c in t) SetLayerRecursive(c, layer);
        }

        private static ISVItemRenderer FindRenderer(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ISVItemRenderer r && mb.gameObject == go) return r; // 优先根上的
            }
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ISVItemRenderer r) return r;
            }
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

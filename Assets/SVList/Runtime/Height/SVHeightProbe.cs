using System.Collections.Generic;
using UnityEngine;

#if UNITY_TEXTMESHPRO_PRESENT
using TMPro;
#endif

namespace SVList
{
#if UNITY_TEXTMESHPRO_PRESENT
    /// <summary>
    /// 自动高度探针（v1.2.0 自校准高度系统 ①：布局指纹自动测量环境）。
    ///
    /// 维护一个离屏的隐藏测量 TMP（保持激活 —— inactive TMP 的 GetPreferredValues
    /// 会返回垃圾值），按模板记录从真实实例采集的布局参数：
    ///   - 动态文本 = 模板内矩形面积最大的 TMP_Text（聊天即消息正文；
    ///     名字行等小文本归入固定增量）
    ///   - FixedDelta = 实例总高 − 动态文本高（名字行、内边距等固定部分）
    ///   - 无 TMP 的模板（纯图片等）记为固定高度模板，Measure 返回常量
    /// 所有参数来自实例化后的真实组件 —— Inspector 改布局，指纹自动跟随，
    /// 无需常量同源。布局变化 → 指纹变化 → 高度缓存自动不命中。
    /// 条件编译：无 TextMeshPro 的项目自动裁剪，保持 Runtime 零 TMP 依赖。
    /// </summary>
    public class SVHeightProbe
    {
        private class TemplateInfo
        {
            public SVLayoutFingerprint Fingerprint;
            public float FixedDelta;
            public float FixedHeight = -1f;    // >= 0 表示固定高度模板
            public float Width;
            public bool Ready;

            // 探针参数（测量时按此同步探针）
            public TMP_FontAsset Font;
            public float FontSize;
            public bool RichText;
            public bool WordWrap;
        }

        private readonly Dictionary<int, TemplateInfo> _templates = new Dictionary<int, TemplateInfo>();
        private readonly Transform _parent;

        private TextMeshProUGUI _probe;

        public SVHeightProbe(Transform parent) => _parent = parent;

        /// <summary>从实例化后的真实 Item 上采集模板参数。
        /// 条目刚创建时 Canvas 布局可能尚未 rebuild（rect.width=0），此时返回 false，
        /// 调用方应下一帧重试（见 SVAutoHeightProvider.Tick）。</summary>
        public bool Capture(ISVItemRenderer renderer, int prefabId)
        {
            if (!(renderer is Component comp)) return false;

            var info = GetInfo(prefabId);
            var dynamicText = FindDynamicText(comp);
            float instanceHeight = renderer.GetPreferredHeight();

            if (dynamicText == null || instanceHeight <= 0f)
            {
                // 无动态文本：固定高度模板（图片消息等）
                info.FixedHeight = Mathf.Max(instanceHeight, 0f);
                info.Ready = info.FixedHeight > 0f;
                return info.Ready;
            }

            float textWidth = dynamicText.rectTransform.rect.width;
            if (textWidth <= 0f) return false; // 布局未就绪：留待下一帧重试

            info.Font = dynamicText.font;
            info.FontSize = dynamicText.enableAutoSizing ? dynamicText.fontSizeMax : dynamicText.fontSize;
            info.RichText = dynamicText.richText;
            info.WordWrap = dynamicText.enableWordWrapping;
            info.Width = textWidth;

            SyncProbe(info);
            float textHeight = MeasureRaw(dynamicText.text, textWidth);

            info.FixedDelta = Mathf.Max(instanceHeight - textHeight, 0f);
            info.Fingerprint = SVLayoutFingerprint.Create(
                FontGuid(info.Font), info.FontSize, info.Width, info.RichText, info.WordWrap, info.FixedDelta);
            info.Ready = true;
            return true;
        }

        public bool IsReady(int prefabId)
            => _templates.TryGetValue(prefabId, out var i) && i.Ready;

        /// <summary>任意模板已完成采集（决定预热请求可否立即执行）。</summary>
        public bool AnyReady
        {
            get
            {
                foreach (var t in _templates.Values) if (t.Ready) return true;
                return false;
            }
        }

        /// <summary>该模板的内容寻址键（布局指纹 × 文本）。</summary>
        public ulong ContentKey(int prefabId, string text)
            => _templates[prefabId].Fingerprint.ContentKey(text ?? string.Empty);

        /// <summary>测量文本高度（含固定增量）。模板未就绪返回 -1（回落估算/实测修正）。</summary>
        public float Measure(int prefabId, string text)
        {
            if (!_templates.TryGetValue(prefabId, out var info) || !info.Ready) return -1f;
            if (info.FixedHeight >= 0f) return info.FixedHeight;

            SyncProbe(info);
            return MeasureRaw(text ?? string.Empty, info.Width) + info.FixedDelta;
        }

        // ---- 内部 ----

        private TemplateInfo GetInfo(int prefabId)
            => _templates.TryGetValue(prefabId, out var i) ? i : (_templates[prefabId] = new TemplateInfo());

        /// <summary>已就绪的模板数量（供增量触发精确高度灌入）。</summary>
        public int ReadyCount
        {
            get
            {
                int n = 0;
                foreach (var t in _templates.Values) if (t.Ready) n++;
                return n;
            }
        }

        /// <summary>启发式定位动态文本：优先"垂直锚拉伸的 TMP"（高度随内容变的那个，
        /// 如气泡正文），取其中面积最大者；全部为固定锚（如图片消息只有顶部名字行）
        /// 则视为无动态文本 → 固定高度模板。</summary>
        private static TMP_Text FindDynamicText(Component root)
        {
            TMP_Text bestStretched = null; float bestStretchedArea = 0f;
            TMP_Text bestAny = null; float bestAnyArea = 0f;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var ra = t.rectTransform.anchorMin;
                var rb = t.rectTransform.anchorMax;
                bool vStretch = Mathf.Abs(rb.y - ra.y) > 0.01f;
                var r = t.rectTransform.rect;
                float area = Mathf.Max(r.width * r.height, 1f);
                if (area > bestAnyArea) { bestAnyArea = area; bestAny = t; }
                if (vStretch && area > bestStretchedArea) { bestStretchedArea = area; bestStretched = t; }
            }
            return bestStretched; // 无拉伸 TMP → null → 固定高度模板
        }

        private static string FontGuid(TMP_FontAsset font)
        {
            if (font == null) return null;
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(font);
            if (!string.IsNullOrEmpty(path))
            {
                var g = UnityEditor.AssetDatabase.GUIDFromAssetPath(path);
                if (!g.Empty()) return g.ToString();
            }
#endif
            return font.GetInstanceID().ToString(); // 打包后无 GUID：实例 ID 兜底（会话内稳定）
        }

        private void SyncProbe(TemplateInfo info)
        {
            var p = EnsureProbe();
            if (_probeState == null || !_probeState.Equals(info))
            {
                p.font = info.Font;
                p.fontSize = info.FontSize;
                p.richText = info.RichText;
                p.enableWordWrapping = info.WordWrap;
                _probeState = info;
            }
        }

        private TemplateInfo _probeState; // 上次同步的模板（参数一致时跳过重设）

        private TextMeshProUGUI EnsureProbe()
        {
            if (_probe != null) return _probe;
            var host = new GameObject("SVList_HeightProbe", typeof(RectTransform));
            host.transform.SetParent(_parent, false);
            host.hideFlags = HideFlags.HideAndDontSave; // 纯运行时对象，不进场景序列化
            var rt = (RectTransform)host.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -99999f); // 视野外，但保持激活
            rt.sizeDelta = new Vector2(2048f, 64f);
            _probe = host.AddComponent<TextMeshProUGUI>();
            _probe.color = new Color(0f, 0f, 0f, 0f);
            _probe.raycastTarget = false;
            return _probe;
        }

        private float MeasureRaw(string text, float width)
            => EnsureProbe().GetPreferredValues(text, width, 0f).y;
    }
#endif
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 自动高度 Provider（v1.2.0 自校准高度系统 ②的组合出口）：
    /// 布局指纹探针（<see cref="SVHeightProbe"/>）+ 内容寻址持久化缓存
    /// （<see cref="SVPersistentHeightCache"/>）—— 用户零公式、零常量。
    ///
    /// 工作流：首个 Item 实例化后从真实组件采集布局指纹（字体/字号/宽度/固定增量，
    /// 详见 SVHeightProbe）→ 之后的每条消息在进入布局前即可得到精确高度：
    /// 先查持久化缓存（跨会话命中），未命中用离屏探针测量并写缓存。
    /// Inspector 改布局 → 指纹变 → 缓存自动不命中 → 重新采集，无需任何失效操作。
    ///
    /// 文本成员启发式（数据对象里谁是"正文"）：优先名 text/content/msg/message/body
    /// 的 string 字段/属性，否则第一个 string 字段，再否则第一个 string 属性。
    /// 启发式不适用时（多动态文本等复杂模板）请回退手写 ISVItemHeightProvider。
    /// 无 TextMeshPro 的运行环境自动退化为恒 -1（回落估算 + 实测修正），不产生错误。
    /// </summary>
    public sealed class SVAutoHeightProvider : ISVItemHeightProvider, ISVTemplateCapture
    {
        private readonly SVListView _view;

#if UNITY_TEXTMESHPRO_PRESENT
        private readonly SVHeightProbe _probe;
#endif

        private readonly Dictionary<Type, MemberInfo> _textAccessors = new Dictionary<Type, MemberInfo>();
        private ISVDataSource _dataSource;
        private Func<int, int> _selector;
        private int _pendingFrom = -1, _pendingTo = -1;

        /// <summary>诊断：持久化缓存命中次数</summary>
        public long CacheHits { get; private set; }

        /// <summary>诊断：探针测量次数（缓存未命中）</summary>
        public long ProbeMeasures { get; private set; }

        public SVAutoHeightProvider(SVListView view)
        {
            _view = view;
#if UNITY_TEXTMESHPRO_PRESENT
            _probe = new SVHeightProbe(view.transform);
#endif
        }

        void ISVTemplateCapture.OnItemInstantiated(ISVItemRenderer renderer, int prefabId)
        {
#if UNITY_TEXTMESHPRO_PRESENT
            _probe.Capture(renderer, prefabId);

            // 预热请求可能早于首帧实例化（driver 在 Start 里调用）：探针就绪后补跑
            if (_pendingFrom >= 0 && _probe.IsReady(prefabId))
            {
                int from = _pendingFrom, to = _pendingTo;
                _pendingFrom = -1;
                Premeasure(from, to);
            }
#endif
        }

        public float GetItemHeight(int index)
        {
#if !UNITY_TEXTMESHPRO_PRESENT
            return -1f;
#else
            if (_dataSource == null) _dataSource = _view.DataSource;
            var data = _dataSource?.GetItemData(index);
            if (data == null) return -1f;

            if (_selector == null)
                _selector = _view.PrefabSelector ?? (_ => 0);
            int prefabId = _selector(index);

            if (!_probe.IsReady(prefabId)) return -1f; // 首条实例化前：回落估算 + 实测修正

            string text = ExtractText(data);
            if (text == null)
            {
                // 无文本成员：固定高度模板（图片等），不进缓存（与内容无关）
                return _probe.Measure(prefabId, string.Empty);
            }

            ulong key = _probe.ContentKey(prefabId, text);
            if (SVPersistentHeightCache.TryGet(key, out var h))
            {
                CacheHits++;
                return h;
            }

            float measured = _probe.Measure(prefabId, text);
            if (measured > 0f)
            {
                SVPersistentHeightCache.Set(key, measured);
                ProbeMeasures++;
                return measured;
            }
            return -1f;
#endif
        }

        /// <summary>预热一段范围（历史页加载后调用；一页几十条为毫秒级，更大范围请自行分帧）。
        /// 若早于首个模板实例化调用，会在探针就绪后自动补跑。</summary>
        public void Premeasure(int fromInclusive, int toInclusive)
        {
            if (_dataSource == null) _dataSource = _view.DataSource;

#if UNITY_TEXTMESHPRO_PRESENT
            // 探针尚未就绪：挂起，等首个模板实例化采集后补跑
            if (!_probe.AnyReady)
            {
                _pendingFrom = fromInclusive;
                _pendingTo = toInclusive;
                return;
            }
#endif
            for (int i = fromInclusive; i <= toInclusive; i++) GetItemHeight(i);
        }

        // ---- 文本成员启发式 ----

        private string ExtractText(object data)
        {
            var type = data.GetType();
            if (!_textAccessors.TryGetValue(type, out var member))
                _textAccessors[type] = member = FindTextMember(type);
            if (member == null) return null;

            return member is FieldInfo f ? f.GetValue(data) as string
                 : member is PropertyInfo p ? p.GetValue(data) as string
                 : null;
        }

        private static readonly string[] PreferredNames = { "text", "content", "msg", "message", "body" };

        private static MemberInfo FindTextMember(Type t)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var name in PreferredNames)
            {
                var f = t.GetField(name, flags);
                if (f != null && f.FieldType == typeof(string)) return f;
                var p = t.GetProperty(name, flags);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead) return p;
            }

            foreach (var f in t.GetFields(flags))
                if (f.FieldType == typeof(string)) return f;

            foreach (var p in t.GetProperties(flags))
                if (p.PropertyType == typeof(string) && p.CanRead) return p;

            return null;
        }
    }
}

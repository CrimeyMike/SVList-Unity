using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SVList
{
    /// <summary>
    /// 滚动事件控制器
    ///
    /// 桥接 UGUI ScrollRect 的 onValueChanged 到 SVListCore。
    /// 负责将 ScrollRect.normalizedPosition 转换为实际的滚动偏移量。
    /// </summary>
    public class SVScrollController : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private ScrollRect _scrollRect;

        /// <summary>滚动偏移量变化回调</summary>
        public event Action<float> OnScrollOffsetChanged;

        /// <summary>Content RectTransform</summary>
        public RectTransform Content => _scrollRect != null ? _scrollRect.content : null;

        /// <summary>Viewport RectTransform</summary>
        public RectTransform Viewport => _scrollRect != null ? _scrollRect.viewport : null;

        /// <summary>是否反转方向</summary>
        public bool Reverse { get; set; }

        /// <summary>是否为垂直滚动</summary>
        public bool IsVertical { get; set; } = true;

        /// <summary>当前滚动偏移量</summary>
        public float ScrollOffset { get; private set; }

        /// <summary>Viewport 尺寸</summary>
        public float ViewportSize
        {
            get
            {
                if (_scrollRect != null)
                {
                    Rect rect = (_scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)_scrollRect.transform).rect;
                    return IsVertical ? rect.height : rect.width;
                }
                return 0f;
            }
        }

        private void Awake()
        {
            if (_scrollRect == null)
            {
                _scrollRect = GetComponent<ScrollRect>();
            }

            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            }
        }

        private void OnDestroy()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            }
        }

        /// <summary>
        /// 初始化控制器
        /// </summary>
        public void Initialize(bool isVertical, bool reverse)
        {
            IsVertical = isVertical;
            Reverse = reverse;

            if (_scrollRect != null)
            {
                _scrollRect.vertical = isVertical;
                _scrollRect.horizontal = !isVertical;
            }
        }

        private bool _isSettingContentSize;

        /// <summary>
        /// ScrollRect 值变化回调
        /// </summary>
        private void OnScrollValueChanged(Vector2 normalizedPosition)
        {
            // 防止 SetContentSize 触发的 layout change 产生反馈
            if (_isSettingContentSize) return;
            float contentSize = IsVertical
                ? (_scrollRect.content != null ? _scrollRect.content.rect.height : 0f)
                : (_scrollRect.content != null ? _scrollRect.content.rect.width : 0f);

            float viewportSize = ViewportSize;

            if (contentSize <= 0f || viewportSize <= 0f)
            {
                ScrollOffset = 0f;
                return;
            }

            if (contentSize <= viewportSize)
            {
                ScrollOffset = 0f;
                OnScrollOffsetChanged?.Invoke(0f);
                return;
            }

            float maxScroll = contentSize - viewportSize;

            if (IsVertical)
            {
                float normalizedY = Reverse ? normalizedPosition.y : (1f - normalizedPosition.y);
                ScrollOffset = normalizedY * maxScroll;
            }
            else
            {
                float normalizedX = Reverse ? (1f - normalizedPosition.x) : normalizedPosition.x;
                ScrollOffset = normalizedX * maxScroll;
            }

            OnScrollOffsetChanged?.Invoke(ScrollOffset);
        }

        /// <summary>
        /// 设置 Content 大小以匹配虚拟列表计算出的总大小
        /// </summary>
        public void SetContentSize(float size)
        {
            if (_scrollRect == null || _scrollRect.content == null) return;

            _isSettingContentSize = true;

            if (IsVertical)
            {
                _scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
            }
            else
            {
                _scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            }

            _isSettingContentSize = false;
        }

        /// <summary>
        /// 设置滚动位置到指定偏移量
        /// </summary>
        public void SetScrollOffset(float offset)
        {
            if (_scrollRect == null || _scrollRect.content == null) return;

            float contentSize = GetContentSize();
            float viewportSize = ViewportSize;

            if (contentSize <= viewportSize) return;

            float maxScroll = contentSize - viewportSize;
            offset = Mathf.Clamp(offset, 0f, maxScroll);
            float normalizedPos = maxScroll > 0f ? offset / maxScroll : 0f;

            if (IsVertical)
            {
                normalizedPos = Reverse ? normalizedPos : (1f - normalizedPos);
                _scrollRect.verticalNormalizedPosition = normalizedPos;
            }
            else
            {
                normalizedPos = Reverse ? (1f - normalizedPos) : normalizedPos;
                _scrollRect.horizontalNormalizedPosition = normalizedPos;
            }

            ScrollOffset = offset;
        }

        /// <summary>
        /// 获取 Content 当前尺寸
        /// </summary>
        private float GetContentSize()
        {
            if (_scrollRect == null || _scrollRect.content == null) return 0f;
            Rect rect = _scrollRect.content.rect;
            return IsVertical ? rect.height : rect.width;
        }

        /// <summary>
        /// 禁用滚动（用于动画期间）
        /// </summary>
        public void SetScrollEnabled(bool enabled)
        {
            if (_scrollRect != null)
            {
                _scrollRect.enabled = enabled;
            }
        }

        /// <summary>
        /// IScrollHandler 实现（可选，用于监听滚轮事件）
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            // 滚轮事件由 ScrollRect 原生处理
        }
    }
}

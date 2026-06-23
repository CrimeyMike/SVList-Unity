using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 垂直布局：从上到下排列 Item
    /// </summary>
    public class SVVerticalLayout : ISVLayout
    {
        private SVHeightCache _heightCache;
        private float _spacing;
        private float _paddingTop;
        private float _paddingBottom;
        private float _itemDefaultWidth;

        public SVVerticalLayout(SVHeightCache heightCache, float spacing = 0f, float paddingTop = 0f, float paddingBottom = 0f, float itemDefaultWidth = 0f)
        {
            _heightCache = heightCache;
            _spacing = spacing;
            _paddingTop = paddingTop;
            _paddingBottom = paddingBottom;
            _itemDefaultWidth = itemDefaultWidth;
        }

        public void UpdateParameters(SVConfig config)
        {
            _spacing = config.Spacing;
            _paddingTop = config.PaddingTop;
            _paddingBottom = config.PaddingBottom;
            _itemDefaultWidth = config.DefaultItemSize;
        }

        public void UpdateParameters(float spacing, float paddingTop, float paddingBottom, float itemDefaultWidth)
        {
            _spacing = spacing;
            _paddingTop = paddingTop;
            _paddingBottom = paddingBottom;
            _itemDefaultWidth = itemDefaultWidth;
        }

        public Vector2 GetPosition(int index)
        {
            float yOffset = _heightCache.GetOffset(index);
            float y = -(yOffset + _paddingTop + index * _spacing);

            // x 固定为 0（通过 anchor/stretch 控制宽度）
            return new Vector2(0f, y);
        }

        public float GetSize(int index)
        {
            return _heightCache.GetHeight(index);
        }

        public float GetContentSize()
        {
            int count = _heightCache.Count;
            if (count == 0) return 0f;

            float totalSpacing = (count - 1) * _spacing;
            return _heightCache.TotalHeight + totalSpacing + _paddingTop + _paddingBottom;
        }

        public int FindIndexByOffset(float offset)
        {
            // 减去 padding
            float adjustedOffset = offset - _paddingTop;
            if (adjustedOffset <= 0f) return 0;

            int count = _heightCache.Count;
            if (count == 0) return 0;

            // O(logN) 快速定位（忽略 spacing 的近似位置）
            int index = _heightCache.FindIndexByOffset(adjustedOffset);

            // 局部校正 spacing 累积误差（通常 < 5 步，常数时间）
            if (_spacing > 0f)
            {
                // 向前走：startOffset 可能因忽略 spacing 而偏大
                while (index > 0)
                {
                    float curStart = _heightCache.GetOffset(index) + index * _spacing;
                    if (curStart <= adjustedOffset) break;
                    index--;
                }
                // 向后走：当前 index 的 startOffset <= adjustedOffset，检查下一个
                while (index + 1 < count)
                {
                    float nextStart = _heightCache.GetOffset(index + 1) + (index + 1) * _spacing;
                    if (nextStart > adjustedOffset) break;
                    index++;
                }
            }

            return index;
        }

        public VisibleRange CalculateRange(float offset, float viewportSize, float bufferSize)
        {
            int count = _heightCache.Count;
            if (count == 0) return VisibleRange.Empty;

            float startOffset = offset - bufferSize;
            float endOffset = offset + viewportSize + bufferSize;

            int first = FindIndexByOffset(Mathf.Max(0f, startOffset));
            int last = FindIndexByOffset(Mathf.Max(0f, endOffset));

            first = Mathf.Clamp(first, 0, count - 1);
            last = Mathf.Clamp(last, 0, count - 1);

            return new VisibleRange(first, last);
        }
    }
}

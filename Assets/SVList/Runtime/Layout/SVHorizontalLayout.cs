using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 水平布局：从左到右排列 Item
    /// </summary>
    public class SVHorizontalLayout : ISVLayout
    {
        private SVHeightCache _widthCache; // 复用 HeightCache，只是概念上是宽度
        private float _spacing;
        private float _paddingLeft;
        private float _paddingRight;
        private float _itemDefaultHeight;

        public SVHorizontalLayout(SVHeightCache widthCache, float spacing = 0f, float paddingLeft = 0f, float paddingRight = 0f, float itemDefaultHeight = 0f)
        {
            _widthCache = widthCache;
            _spacing = spacing;
            _paddingLeft = paddingLeft;
            _paddingRight = paddingRight;
            _itemDefaultHeight = itemDefaultHeight;
        }

        public void UpdateParameters(SVConfig config)
        {
            _spacing = config.Spacing;
            _paddingLeft = config.PaddingLeft;
            _paddingRight = config.PaddingRight;
            _itemDefaultHeight = config.DefaultItemSize;
        }

        public void UpdateParameters(float spacing, float paddingLeft, float paddingRight, float itemDefaultHeight)
        {
            _spacing = spacing;
            _paddingLeft = paddingLeft;
            _paddingRight = paddingRight;
            _itemDefaultHeight = itemDefaultHeight;
        }

        public Vector2 GetPosition(int index)
        {
            float xOffset = _widthCache.GetOffset(index);
            float x = xOffset + _paddingLeft + index * _spacing;

            return new Vector2(x, 0f);
        }

        public float GetSize(int index)
        {
            return _widthCache.GetHeight(index);
        }

        public float GetContentSize()
        {
            int count = _widthCache.Count;
            if (count == 0) return 0f;

            float totalSpacing = (count - 1) * _spacing;
            return _widthCache.TotalHeight + totalSpacing + _paddingLeft + _paddingRight;
        }

        public int FindIndexByOffset(float offset)
        {
            float adjustedOffset = offset - _paddingLeft;
            if (adjustedOffset <= 0f) return 0;

            int count = _widthCache.Count;
            if (count == 0) return 0;

            // O(logN) 快速定位
            int index = _widthCache.FindIndexByOffset(adjustedOffset);

            // 局部校正 spacing 累积误差
            if (_spacing > 0f)
            {
                while (index > 0)
                {
                    float curStart = _widthCache.GetOffset(index) + index * _spacing;
                    if (curStart <= adjustedOffset) break;
                    index--;
                }
                while (index + 1 < count)
                {
                    float nextStart = _widthCache.GetOffset(index + 1) + (index + 1) * _spacing;
                    if (nextStart > adjustedOffset) break;
                    index++;
                }
            }
            return index;
        }

        public VisibleRange CalculateRange(float offset, float viewportSize, float bufferSize)
        {
            int count = _widthCache.Count;
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

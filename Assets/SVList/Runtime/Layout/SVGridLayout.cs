using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 网格布局：按行列排列 Item
    ///
    /// 排列方式：
    /// 0  1  2
    /// 3  4  5
    /// 6  7  8
    /// </summary>
    public class SVGridLayout : ISVLayout
    {
        private SVHeightCache _heightCache;
        private int _columnCount;
        private float _cellWidth;
        private float _cellHeight; // 固定行高（如为 0 则动态高度）
        private float _horizontalSpacing;
        private float _verticalSpacing;
        private float _paddingLeft;
        private float _paddingRight;
        private float _paddingTop;
        private float _paddingBottom;

        public int ColumnCount => _columnCount;

        public void UpdateParameters(SVConfig config)
        {
            _columnCount = Mathf.Max(1, config.GridColumnCount);
            _cellWidth = config.GridCellWidth;
            _cellHeight = config.GridCellHeight;
            _horizontalSpacing = config.GridHorizontalSpacing;
            _verticalSpacing = config.GridVerticalSpacing;
            _paddingLeft = config.PaddingLeft;
            _paddingRight = config.PaddingRight;
            _paddingTop = config.PaddingTop;
            _paddingBottom = config.PaddingBottom;
        }

        public SVGridLayout(SVHeightCache heightCache, int columnCount,
            float cellWidth, float cellHeight = 0f,
            float horizontalSpacing = 0f, float verticalSpacing = 0f,
            float paddingLeft = 0f, float paddingRight = 0f,
            float paddingTop = 0f, float paddingBottom = 0f)
        {
            _heightCache = heightCache;
            _columnCount = Mathf.Max(1, columnCount);
            _cellWidth = cellWidth;
            _cellHeight = cellHeight;
            _horizontalSpacing = horizontalSpacing;
            _verticalSpacing = verticalSpacing;
            _paddingLeft = paddingLeft;
            _paddingRight = paddingRight;
            _paddingTop = paddingTop;
            _paddingBottom = paddingBottom;
        }

        public Vector2 GetPosition(int index)
        {
            int row = index / _columnCount;
            int col = index % _columnCount;

            float x = _paddingLeft + col * (_cellWidth + _horizontalSpacing);
            float yOffset = 0f;

            if (_cellHeight > 0f)
            {
                // 固定行高
                yOffset = row * (_cellHeight + _verticalSpacing);
            }
            else
            {
                // 动态行高
                yOffset = _heightCache.GetOffset(index / _columnCount);
                yOffset += row * _verticalSpacing;
            }

            float y = -(_paddingTop + yOffset);
            return new Vector2(x, y);
        }

        public float GetSize(int index)
        {
            return _cellHeight > 0f ? _cellHeight : _heightCache.GetHeight(index / _columnCount);
        }

        public float GetContentSize()
        {
            int rowCount = GetRowCount();
            if (rowCount == 0) return 0f;

            float totalSpacing = (rowCount - 1) * _verticalSpacing;
            float totalHeight;

            if (_cellHeight > 0f)
            {
                totalHeight = rowCount * _cellHeight;
            }
            else
            {
                // 动态行高：HeightCache 按行数存储
                totalHeight = _heightCache.TotalHeight;
            }

            return totalHeight + totalSpacing + _paddingTop + _paddingBottom;
        }

        private int GetRowCount()
        {
            if (_cellHeight > 0f)
                return (_heightCache.Count + _columnCount - 1) / _columnCount;
            else
                return _heightCache.Count; // 动态模式：HeightCache 直接存行数
        }

        public int FindIndexByOffset(float offset)
        {
            float adjustedOffset = offset - _paddingTop;
            if (adjustedOffset <= 0f) return 0;

            int totalItems = _heightCache.Count; // 固定模式=item数, 动态模式=行数
            if (totalItems == 0) return 0;

            int row;
            if (_cellHeight > 0f)
            {
                // 固定行高：直接计算
                float rowHeight = _cellHeight + _verticalSpacing;
                int totalRows = (totalItems + _columnCount - 1) / _columnCount;
                row = Mathf.Clamp((int)(adjustedOffset / rowHeight), 0, totalRows - 1);
            }
            else
            {
                // 动态行高：二分查找，计入行间距
                row = FindRowByOffset(adjustedOffset, _heightCache.Count);
            }

            return Mathf.Min(row * _columnCount, totalItems - 1);
        }

        public VisibleRange CalculateRange(float offset, float viewportSize, float bufferSize)
        {
            int totalItems = _heightCache.Count; // 固定模式=item数, 动态模式=行数
            if (totalItems == 0) return VisibleRange.Empty;

            float startOffset = offset - bufferSize;
            float endOffset = offset + viewportSize + bufferSize;

            int firstRow, lastRow;

            if (_cellHeight > 0f)
            {
                float rowHeight = _cellHeight + _verticalSpacing;
                int totalRows = (totalItems + _columnCount - 1) / _columnCount;
                firstRow = Mathf.Clamp((int)(startOffset / rowHeight), 0, totalRows - 1);
                lastRow  = Mathf.Clamp((int)(endOffset   / rowHeight), 0, totalRows - 1);
            }
            else
            {
                // 动态行高：二分查找行号，计入行间距
                int rowCount = _heightCache.Count;
                firstRow = FindRowByOffset(Mathf.Max(0f, startOffset), rowCount);
                lastRow  = FindRowByOffset(Mathf.Max(0f, endOffset),   rowCount);
            }

            int first = Mathf.Clamp(firstRow * _columnCount, 0, totalItems - 1);
            int last  = Mathf.Clamp((lastRow + 1) * _columnCount - 1, 0, totalItems - 1);

            return new VisibleRange(first, last);
        }

        /// <summary>
        /// 查找指定 offset 对应的行号（计入行间距），O(logN) + 局部校正
        /// </summary>
        private int FindRowByOffset(float offset, int rowCount)
        {
            // O(logN) 快速定位
            int row = _heightCache.FindIndexByOffset(offset);

            // 局部校正行间距
            if (_verticalSpacing > 0f)
            {
                while (row > 0)
                {
                    float curStart = _heightCache.GetOffset(row) + row * _verticalSpacing;
                    if (curStart <= offset) break;
                    row--;
                }
                while (row + 1 < rowCount)
                {
                    float nextStart = _heightCache.GetOffset(row + 1) + (row + 1) * _verticalSpacing;
                    if (nextStart > offset) break;
                    row++;
                }
            }
            return row;
        }
    }
}

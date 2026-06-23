using UnityEngine;

namespace SVList
{
    /// <summary>
    /// SVList 通用工具方法
    /// </summary>
    public static class SVUtils
    {
        /// <summary>
        /// 计算两个可见范围的差异（用于调试/验证）
        /// </summary>
        public static void CalculateDiffRanges(
            VisibleRange oldRange, VisibleRange newRange,
            out int addStart, out int addEnd,
            out int removeStart, out int removeEnd)
        {
            addStart = addEnd = removeStart = removeEnd = -1;

            if (oldRange.First < 0)
            {
                addStart = newRange.First;
                addEnd = newRange.Last;
                return;
            }

            int oldFirst = oldRange.First;
            int oldLast = oldRange.Last;
            int newFirst = newRange.First;
            int newLast = newRange.Last;

            if (newLast > oldLast)
            {
                addStart = Mathf.Max(newFirst, oldLast + 1);
                addEnd = newLast;
            }
            if (newFirst < oldFirst)
            {
                addStart = newFirst;
                addEnd = Mathf.Min(newLast, oldFirst - 1);
            }

            if (oldFirst < newFirst)
            {
                removeStart = oldFirst;
                removeEnd = Mathf.Min(oldLast, newFirst - 1);
            }
            if (oldLast > newLast)
            {
                removeStart = Mathf.Max(oldFirst, newLast + 1);
                removeEnd = oldLast;
            }
        }

        /// <summary>
        /// 快速判断一个索引是否在可见范围内
        /// </summary>
        public static bool IsInRange(int index, VisibleRange range)
        {
            return index >= range.First && index <= range.Last;
        }

        /// <summary>
        /// 安全设置 GameObject 显隐（避免重复触发 OnEnable/OnDisable）
        /// </summary>
        public static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}

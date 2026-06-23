using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 布局接口，所有布局（Vertical/Horizontal/Grid/Masonry）统一实现
    /// 将 Core 与具体布局策略解耦
    /// </summary>
    public interface ISVLayout
    {
        /// <summary>获取指定索引 Item 的锚定位置</summary>
        Vector2 GetPosition(int index);

        /// <summary>获取指定索引 Item 的尺寸（垂直模式=高度，水平模式=宽度）</summary>
        float GetSize(int index);

        /// <summary>获取 Content 总尺寸</summary>
        float GetContentSize();

        /// <summary>根据偏移量查找对应索引</summary>
        int FindIndexByOffset(float offset);

        /// <summary>计算可见范围（含 buffer）</summary>
        VisibleRange CalculateRange(float offset, float viewportSize, float bufferSize);

        /// <summary>运行时更新布局参数（spacing/padding/cellSize 等），从 SVConfig 读取</summary>
        void UpdateParameters(SVConfig config);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// SVList 全局配置类
    ///
    /// 所有参数均可在 Inspector 中可视化配置。
    /// </summary>
    [Serializable]
    public class SVConfig
    {
        #region 布局参数

        [Header("Layout")]
        [Tooltip("元素之间的间距")]
        public float Spacing = 10f;

        [Tooltip("顶部/左侧内边距")]
        public float PaddingTop = 5f;

        [Tooltip("底部/右侧内边距")]
        public float PaddingBottom = 5f;

        [Tooltip("左内边距（用于 Horizontal 布局）")]
        public float PaddingLeft = 5f;

        [Tooltip("右内边距（用于 Horizontal 布局）")]
        public float PaddingRight = 5f;

        [Tooltip("默认 Item 宽度（垂直模式）或高度（水平模式）")]
        public float DefaultItemSize = 0f;

        #endregion

        #region 动态高度参数

        [Header("Dynamic Height")]
        [Tooltip("默认预估高度（未测量 item 使用此值）")]
        public float EstimateHeight = 100f;

        [Tooltip("是否启用动态高度")]
        public bool DynamicHeight = true;

        [Tooltip("高度变化阈值（变化小于此值忽略，防止死循环）")]
        public float HeightChangeThreshold = 0.5f;

        #endregion

        #region 性能参数

        [Header("Performance")]
        [Tooltip("视口外预加载倍数（0.5=半屏，1.0=一屏）")]
        public float PreloadFactor = 0.5f;

        [Tooltip("对象池最大容量（每种 Prefab）")]
        public int MaxPoolSize = 50;

        [Tooltip("每帧最大实例化数量")]
        public int MaxInstantiatePerFrame = 5;

        [Tooltip("预热数量（初始化时提前创建的实例数）")]
        public int PreWarmCount = 20;

        #endregion

        #region 方向参数

        [Header("Direction")]
        [Tooltip("滚动方向")]
        public SVDirection Direction = SVDirection.Vertical;

        [Tooltip("是否反转（聊天列表从底部开始）")]
        public bool Reverse = false;

        #endregion

        #region Grid参数

        [Header("Grid")]
        [Tooltip("网格列数（仅 Grid 模式生效）")]
        public int GridColumnCount = 3;

        [Tooltip("单元格宽度（仅 Grid 模式）")]
        public float GridCellWidth = 100f;

        [Tooltip("单元格高度（0=使用动态高度）")]
        public float GridCellHeight = 0f;

        [Tooltip("水平间距（仅 Grid 模式）")]
        public float GridHorizontalSpacing = 10f;

        [Tooltip("垂直间距（仅 Grid 模式）")]
        public float GridVerticalSpacing = 10f;

        #endregion

        #region 动画参数

        [Header("Animation")]
        [Tooltip("JumpTo 动画持续时间（秒）")]
        public float JumpDuration = 0.3f;

        [Tooltip("JumpTo 缓动曲线")]
        public AnimationCurve JumpEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Debug参数

        [Header("Debug")]
        [Tooltip("在 Scene 视图中绘制可见区和 Buffer 边界")]
        public bool ShowGizmos = false;

        [Tooltip("显示详细的 Gizmos（活跃元素边界、网格线、标签等）")]
        public bool ShowDetailedGizmos = false;

        [Tooltip("显示运行时调试面板")]
        public bool ShowDebugPanel = false;

        #endregion
    }

    /// <summary>
    /// 滚动方向
    /// </summary>
    public enum SVDirection
    {
        Vertical,
        Horizontal,
        Grid
    }
}

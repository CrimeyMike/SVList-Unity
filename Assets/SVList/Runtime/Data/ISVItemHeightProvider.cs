namespace SVList
{
    /// <summary>
    /// 高度预计算提供者（可选能力）
    ///
    /// 业务层实现此接口后，列表在 Item 实例化之前即可获得精确高度：
    /// - 高度缓存以"已测量"状态初始化，消除"估算 → 实例化修正"带来的滚动跳动
    /// - 头部插入历史数据时，插入高度已知，视口补偿可以做到零误差
    ///
    /// 典型用法（聊天）：消息进入数据源前，用 TMP_Text.GetPreferredValues(text, width)
    /// 算出文本排版高度 + 头像/内边距等固定部分，直接作为该条消息的精确高度。
    ///
    /// 返回 &lt;= 0 表示该条未提供（回落到 EstimateHeight 估算，仍可正常工作）。
    /// </summary>
    public interface ISVItemHeightProvider
    {
        /// <summary>获取指定索引数据项的预计算主轴尺寸（垂直列表=高度）</summary>
        float GetItemHeight(int index);
    }
}

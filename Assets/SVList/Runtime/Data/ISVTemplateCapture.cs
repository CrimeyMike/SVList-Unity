namespace SVList
{
    /// <summary>
    /// 模板实例化采集钩子（v1.2.0 自校准高度系统）。
    /// SVListView 在每个 Item 实例化并绑定后回调，供自动测量系统
    /// 从真实实例上采集布局参数（字体/字号/宽度/固定增量）。
    /// </summary>
    public interface ISVTemplateCapture
    {
        /// <summary>renderer：刚实例化的 Item；prefabId：其模板编号。</summary>
        void OnItemInstantiated(ISVItemRenderer renderer, int prefabId);
    }
}

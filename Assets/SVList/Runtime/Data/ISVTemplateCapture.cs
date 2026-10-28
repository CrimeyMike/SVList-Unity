namespace SVList
{
    /// <summary>
    /// 模板实例化采集钩子（v1.2.0 自校准高度系统）。
    /// SVListView 在每个 Item 实例化并绑定后回调，供自动测量系统
    /// 从真实实例上采集布局参数（字体/字号/宽度/固定增量）。
    /// </summary>
    public interface ISVTemplateCapture
    {
        /// <summary>renderer：刚实例化的 Item；prefabId：其模板编号。
        /// 返回后条目可能因布局未完成而暂不可采集，采集方应在 Tick 中重试。</summary>
        void OnItemInstantiated(ISVItemRenderer renderer, int prefabId);

        /// <summary>每帧驱动（View.LateUpdate 调用）：重试因布局未就绪而失败的采集，
        /// 并在探针就绪后执行预热补跑与精确高度灌入。</summary>
        void Tick();
    }
}

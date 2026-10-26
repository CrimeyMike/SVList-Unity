using UnityEngine;

namespace SVList
{
    /// <summary>
    /// Item 渲染器便捷基类（快速接入推荐直接继承这个，而不是从零实现 ISVItemRenderer）
    ///
    /// 最简用法——只重写 OnBind(T data, int index) 一个方法：
    /// <code>
    /// public class MyItem : SVItemBase&lt;ChatMessage&gt;
    /// {
    ///     [SerializeField] private TMP_Text _text;
    ///     protected override void OnBind(ChatMessage data, int index) => _text.text = data.Text;
    /// }
    /// </code>
    ///
    /// 其余生命周期默认空实现，按需重写：
    /// - OnCreated：等价 OnCreate，做一次性的 GetComponent 缓存
    /// - OnUnbound / OnRecycled：解绑/销毁清理
    /// - MeasureHeight：动态高度测量（配合 SVConfig.DynamicHeight；用高度预计算提供者时可不动）
    /// </summary>
    public abstract class SVItemBase<T> : MonoBehaviour, ISVItemRenderer
    {
        /// <summary>当前绑定的数据（OnBind 之后有效，离开视图置空）</summary>
        protected T Data { get; private set; }

        /// <summary>当前绑定索引（-1 表示未绑定）</summary>
        protected int Index { get; private set; } = -1;

        void ISVItemRenderer.OnCreate() => OnCreated();

        void ISVItemRenderer.OnBind(object data, int index)
        {
            Data = (T)data;
            Index = index;
            OnBind(Data, index);
        }

        void ISVItemRenderer.OnUnbind()
        {
            OnUnbound();
            Data = default;
            Index = -1;
        }

        void ISVItemRenderer.OnRecycle() => OnRecycled();

        float ISVItemRenderer.GetPreferredHeight() => MeasureHeight();

        /// <summary>创建时调用一次（缓存组件引用）</summary>
        protected virtual void OnCreated() { }

        /// <summary>绑定数据（唯一必须重写的方法）</summary>
        protected abstract void OnBind(T data, int index);

        /// <summary>离开视图（取消监听、停协程）</summary>
        protected virtual void OnUnbound() { }

        /// <summary>彻底销毁（释放资源）</summary>
        protected virtual void OnRecycled() { }

        /// <summary>
        /// 动态高度测量（DynamicHeight 开启时在 OnBind 后调用）。
        /// 默认返回 0 = 不修正（适用于已用 ISVItemHeightProvider 预计算、或等高列表）。
        /// </summary>
        protected virtual float MeasureHeight() => 0f;
    }
}

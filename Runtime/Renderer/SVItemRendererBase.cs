using UnityEngine;
using UnityEngine.UI;

namespace SVList
{
    /// <summary>
    /// 示例 Item 渲染器基类
    ///
    /// 提供了缓存 RectTransform 和常用组件的模式。
    /// 业务层可以继承此类或直接实现 ISVItemRenderer。
    /// </summary>
    public abstract class SVItemRendererBase : MonoBehaviour, ISVItemRenderer
    {
        [Header("SVList Renderer")]
        [SerializeField] protected RectTransform _rectTransform;
        [SerializeField] protected LayoutElement _layoutElement;

        protected int _currentIndex;

        public int CurrentIndex => _currentIndex;

        protected virtual void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;
        }

        /// <summary>
        /// 创建时调用（仅一次）
        /// 缓存组件引用
        /// </summary>
        public virtual void OnCreate()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (_layoutElement == null)
                _layoutElement = GetComponent<LayoutElement>();
        }

        /// <summary>
        /// 绑定数据
        /// </summary>
        public virtual void OnBind(object data, int index)
        {
            _currentIndex = index;
        }

        /// <summary>
        /// 解绑（取消监听、停止协程）
        /// </summary>
        public virtual void OnUnbind()
        {
            // 子类实现：取消事件监听、停止协程等
        }

        /// <summary>
        /// 销毁时回调
        /// </summary>
        public virtual void OnRecycle()
        {
            // 子类实现：释放资源
        }

        /// <summary>
        /// 获取推荐高度
        /// 默认返回 LayoutElement.preferredHeight 或 RectTransform.rect.height
        /// </summary>
        public virtual float GetPreferredHeight()
        {
            if (_layoutElement != null && _layoutElement.preferredHeight > 0f)
            {
                return _layoutElement.preferredHeight;
            }

            if (_rectTransform != null)
            {
                return LayoutUtility.GetPreferredHeight(_rectTransform);
            }

            return _rectTransform != null ? _rectTransform.rect.height : 100f;
        }
    }
}

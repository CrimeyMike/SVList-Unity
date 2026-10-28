using TMPro;
using UnityEngine;
using SVList;

namespace SVList.Examples
{
    /// <summary>
    /// 聊天 Item 示例：继承 SVItemBase&lt;ChatMessage&gt;，只重写 OnBind。
    ///
    /// 预制体结构（ChatItemExample.prefab，静态布局即所见 —— 内部布局不在代码里布置）：
    /// ChatItemExample (RectTransform + ChatItemExample)
    /// ├─ Name      (TMP_Text, 顶部整行，左右对齐由 alignment 切换)
    /// └─ Bubble    (Image, 左缘竖拉伸锚 = 静态"他人消息"形态；宽度 BubbleWidth)
    ///    └─ Msg    (TMP_Text, 内边距 16/10，宽度=ChatExampleMetrics.TextWidth)
    /// OnBind 只做状态刷新：文本、名字对齐、IsSelf 时的气泡左右翻转（方向切换，非布局拼装）。
    /// </summary>
    public class ChatItemExample : SVItemBase<ChatMessage>
    {
        [Header("UI 引用")]
        public TMP_Text NameText;
        public TMP_Text MsgText;
        public RectTransform BubbleRect;

        protected override void OnBind(ChatMessage data, int index)
        {
            NameText.text = data.Sender;
            NameText.alignment = data.IsSelf ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            MsgText.text = data.Text;

            // 左右翻转（状态，非布局）：预制体静态为"他人消息"（气泡靠左，预览即正确结构）。
            // 不设 sizeDelta —— 宽高/内边距全部由预制体静态布局决定，Inspector 调整即生效。
            bool self = data.IsSelf;
            BubbleRect.anchorMin = new Vector2(self ? 1f : 0f, 0f);
            BubbleRect.anchorMax = new Vector2(self ? 1f : 0f, 1f);
            BubbleRect.pivot = new Vector2(self ? 1f : 0f, 0.5f);
            BubbleRect.anchoredPosition = new Vector2(self ? -24f : 24f, -22f);
        }

        /// <summary>
        /// 响应式宽度钳制：气泡宽 = min(设计宽, 根宽 − 左右边距)。
        /// 面板与 1080 参考宽一致时保持设计宽（不满宽、留右侧空隙）；
        /// 面板更窄时气泡自动收缩，不再超出 Item 根的框选范围。
        /// 仅运行时回调（不写 [ExecuteAlways]，prefab 静态值不受影响），
        /// 属于响应式约束而非布局拼装 —— prefab 静态布局仍是权威。
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            if (BubbleRect == null) return;
            var root = transform as RectTransform;
            if (root == null) return;
            float rootW = root.rect.width;
            if (rootW <= 0f) return; // 布局未就绪

            float maxW = rootW - ChatExampleMetrics.BubbleSideMargin * 2f;
            if (maxW <= 0f) return;

            float w = Mathf.Min(ChatExampleMetrics.BubbleWidth, maxW);
            if (Mathf.Abs(BubbleRect.sizeDelta.x - w) > 0.5f)
                BubbleRect.sizeDelta = new Vector2(w, BubbleRect.sizeDelta.y);
        }

        /// <summary>
        /// 兜底测量（AutoMeasure / Provider 未生效时使用；宽度读实例真实值，Inspector 改宽度自动跟随）。
        /// v1.2.0 自校准高度开启时，Provider 已给出精确高度，本方法不会在滚动热路径被调用。
        /// 注意：条目创建帧 Canvas 布局尚未发生（rect.width=0），此时返回 0 放弃测量 ——
        /// 列表会保持估算并等待自校准回灌/复用帧实测，避免用错误的兜底宽度先入为主。
        /// </summary>
        protected override float MeasureHeight()
        {
            if (MsgText == null || Data == null) return 0f;
            float width = MsgText.rectTransform.rect.width;
            if (width <= 0f) return 0f; // 布局未就绪：宁可不测，不可测错
            float textH = MsgText.GetPreferredValues(Data.Text, width, 0f).y;
            return Mathf.Max(ChatExampleMetrics.MinHeight,
                textH + ChatExampleMetrics.NameRowHeight + ChatExampleMetrics.VerticalPadding);
        }
    }
}

using TMPro;
using UnityEngine;
using SVList;

namespace SVList.Examples
{
    /// <summary>聊天消息数据模型（示例）</summary>
    public class ChatMessage
    {
        public string Sender;
        public string Text;
        public bool IsSelf;

        /// <summary>图片消息（模拟表情/图片发送）：走第二种 Item 模板（ImageMessageItem）</summary>
        public bool IsImage;
    }

    /// <summary>
    /// 测量常量的唯一来源：
    /// Item 预制体的排版（名称行高、气泡宽度、内边距）必须和这里的数值一致，
    /// 数据层预计算的高度才会和实际渲染高度吻合（这是不等高列表不跳屏的前提）。
    /// </summary>
    public static class ChatExampleMetrics
    {
        public const float BubbleWidth = 792f;
        public const float TextWidth = BubbleWidth - 32f;   // 气泡左右各 16 内边距
        public const float NameRowHeight = 44f;
        public const float VerticalPadding = 20f;
        public const float MinHeight = 72f;
        public const float MsgFontSize = 34f;
        public const float NameFontSize = 28f;

        /// <summary>图片消息显示边长（正方形；真实项目按图片宽高比计算）</summary>
        public const float ImageSize = 240f;
    }

    /// <summary>
    /// 数据源示例：同时实现 ISVDataSource + ISVItemHeightProvider。
    /// 高度用隐藏的测量 TMP 预计算（TMP_Text.GetPreferredValues），
    /// 列表在实例化 Item 之前就知道精确高度 —— 无估算、无跳动。
    /// </summary>
    public class ChatDataSource : ISVDataSource, ISVItemHeightProvider
    {
        public readonly System.Collections.Generic.List<ChatMessage> Messages
            = new System.Collections.Generic.List<ChatMessage>();

        /// <summary>隐藏测量文本（字体、字号必须与消息文本一致）</summary>
        public TMP_Text MeasureText;

        public int GetItemCount() => Messages.Count;

        public object GetItemData(int index) => Messages[index];

        public float GetItemHeight(int index)
        {
            if (index < 0 || index >= Messages.Count || MeasureText == null) return -1f;

            var msg = Messages[index];
            if (msg.IsImage)
                return ChatExampleMetrics.NameRowHeight + ChatExampleMetrics.ImageSize
                     + ChatExampleMetrics.VerticalPadding;

            float textH = MeasureText.GetPreferredValues(
                msg.Text, ChatExampleMetrics.TextWidth, 0f).y;
            return Mathf.Max(ChatExampleMetrics.MinHeight,
                textH + ChatExampleMetrics.NameRowHeight + ChatExampleMetrics.VerticalPadding);
        }
    }
}

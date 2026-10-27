using System.Collections.Generic;
using UnityEngine;

namespace SVList.Examples
{
    /// <summary>
    /// 聊天模板驱动：不等高消息直接可跑。
    /// 高度走 ChatItemExample.MeasureHeight 兜底 + 视口补偿（滚动经过时修正，不跳屏）；
    /// 进阶零跳动方案：数据源实现 ISVItemHeightProvider 预计算高度，
    /// 完整写法见 Examples/Scripts/ChatExample.cs 与包内 README「不等高聊天」。
    /// </summary>
    public class ChatListDriver : MonoBehaviour
    {
        [SerializeField] private SVListView _view;
        [SerializeField] private int _itemCount = 100;

        private static readonly string[] SampleTexts =
        {
            "收到",
            "这是一条稍微长一点的消息，用来撑出不等高的效果。",
            "今晚八点联盟战，都准时上！",
            "好",
            "刚打完一局，战绩截图发群里了，大家看看这波操作怎么说，是不是有排面？",
            "ok",
        };

        private readonly List<ChatMessage> _data = new List<ChatMessage>();

        private void Start()
        {
            for (int i = 0; i < _itemCount; i++)
                _data.Add(new ChatMessage
                {
                    Sender = $"Member {(i % 17):00}",
                    Text = SampleTexts[i % SampleTexts.Length],
                    IsSelf = i % 3 == 0,
                });

            _view.Initialize(_data);
            _view.ScrollToBottom();
        }
    }
}

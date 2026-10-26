using UnityEngine;
using TMPro;
using SVList;

namespace SVList.Examples
{
    /// <summary>
    /// 演示场景驱动脚本（薄壳）：层级、按钮、Item 预制体都在场景/资产里接好线，
    /// 本脚本只负责数据生成与列表 API 的标准用法演示。
    ///
    /// 配套：Examples/Scenes/SVListChatDemo.unity + Examples/Prefabs/ChatItemExample.prefab
    /// 文档：包根 README.md
    /// </summary>
    public class SVListChatDemo : MonoBehaviour
    {
        [Header("场景引用（演示场景中已接好）")]
        [Tooltip("挂了 SVListView 的 ScrollRect 根物体")]
        public SVListView View;
        [Tooltip("左上角统计文本")]
        public TMP_Text StatsText;
        [Tooltip("隐藏的测量文本，字体/字号与消息文本一致（供数据源预计算高度）")]
        public TMP_Text MeasureText;

        [Header("演示参数")]
        public int InitialCount = 100;
        public int HistoryPageSize = 30;

        private readonly ChatDataSource _dataSource = new ChatDataSource();
        private float _nextAutoLoadTime;
        private int _historySeed = 100000;

        private static readonly string[] Senders =
            { "联盟统帅·铁头", "风色童话", "Yuki", "奶昔不加糖", "KPI终结者", "半夜上分选手", "会长的小号" };

        private static readonly string[] Fragments =
        {
            "今晚八点联盟战，都上线！", "收到", "谁把援防撤了？？？",
            "刚抽到新英雄，强度一般，观望", "资源够的兄弟捐一波兵，谢谢",
            "这波防守摆得有点东西，参考一下我的阵型", "新人报到！请问前期资源优先升什么？",
            "优先主堡和兵营，别乱升防御塔", "哈哈哈哈这个表情包存了",
            "有人一起打副本吗，缺一个输出", "来来来，马上开",
            "注意！明天维护到中午，别卡点上线", "大佬们这个活动值得氪吗",
            "白嫖党建议忍住，奖励一般", "刚翻完战绩，上周胜率61%，这周冲65",
            "打不过，根本打不过", "阵型分享给你们了，抄作业记得改陷阱位置",
            "盟主 又在半夜偷袭路人（划掉）训练", "哈哈哈哈哈哈",
            "补一句：联盟商店的加速优先换，别换皮肤碎片",
        };

        private void Start()
        {
            _dataSource.MeasureText = MeasureText;
            for (int i = 0; i < InitialCount; i++)
                _dataSource.Messages.Add(GenerateMessage(_historySeed--));

            // 初始化（ChatDataSource 实现了 ISVItemHeightProvider，会被自动识别）
            View.Initialize(_dataSource);
            View.Core.ScrollToBottom();

            _nextAutoLoadTime = Time.time;
        }

        private void Update()
        {
            // 顶部自动加载历史：滚到顶附近时插入一页（InsertItems 自动补偿视口，画面不跳）
            if (View.Core != null && View.Core.IsInitialized &&
                View.Core.State.ScrollOffset < 80f &&
                Time.time >= _nextAutoLoadTime)
            {
                _nextAutoLoadTime = Time.time + 2f;
                PrependHistoryPage();
            }
        }

        private float _statsNextUpdate;
        private string _statsCache;

        private void LateUpdate()
        {
            var core = View.Core;
            if (core == null || !core.IsInitialized || StatsText == null) return;
            if (Time.unscaledTime < _statsNextUpdate) return;
            _statsNextUpdate = Time.unscaledTime + 0.25f;

            var dbg = core.DebugInfo;
            var text = $"消息总数 {dbg.TotalCount}   活跃 {dbg.ActiveCount}   池 {dbg.PoolCount}\n" +
                       $"Offset {core.State.ScrollOffset:F0} / {core.MaxScrollOffset:F0}   " +
                       $"可见 [{core.State.FirstVisibleIndex}..{core.State.LastVisibleIndex}]   " +
                       $"在底部: {(core.IsAtBottom(48f) ? "是" : "否")}";
            if (text != _statsCache)
            {
                _statsCache = text;
                StatsText.text = text;
            }
        }

        #region 按钮回调（场景按钮已绑定）

        /// <summary>发送一条随机消息：只有原本在底部才跟滚（微信行为）</summary>
        public void SendRandomMessage()
        {
            bool wasAtBottom = View.IsAtBottom(48f);
            _dataSource.Messages.Add(GenerateMessage(Random.Range(0, 99999), isSelf: true));
            View.AppendItem();

            if (wasAtBottom)
                View.ScrollToBottomSmooth();
        }

        /// <summary>顶部加载一页历史消息 —— 核心特性：加载后画面不跳</summary>
        public void PrependHistoryPage()
        {
            for (int i = 0; i < HistoryPageSize; i++)
                _dataSource.Messages.Insert(0, GenerateMessage(_historySeed--));
            View.InsertItems(0, HistoryPageSize);
        }

        /// <summary>回到底部</summary>
        public void JumpToBottom()
        {
            View.ScrollToBottomSmooth();
        }

        #endregion

        private ChatMessage GenerateMessage(int seed, bool isSelf = false)
        {
            var rng = new System.Random(seed);
            int fragCount = 1 + rng.Next(4);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < fragCount; i++)
            {
                if (i > 0) sb.Append(rng.Next(3) == 0 ? "\n" : " ");
                sb.Append(Fragments[rng.Next(Fragments.Length)]);
            }
            // 偶尔生成超长消息，突出不等高
            if (rng.Next(10) == 0)
            {
                sb.Append("\n——附一段战报：");
                for (int i = 0; i < 6; i++) sb.Append(Fragments[rng.Next(Fragments.Length)]);
            }
            return new ChatMessage
            {
                Sender = isSelf ? "我" : Senders[rng.Next(Senders.Length)],
                Text = sb.ToString(),
                IsSelf = isSelf,
            };
        }
    }
}

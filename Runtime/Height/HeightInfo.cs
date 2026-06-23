namespace SVList
{
    /// <summary>
    /// 高度信息，区分未测量/已测量状态
    /// 不能简单用 float[] heights，因为未创建的 item 高度未知
    /// 未知高度 = EstimateHeight（不能为 0，否则二分查找出错）
    /// </summary>
    public class HeightInfo
    {
        /// <summary>是否已经真实测量</summary>
        public bool IsMeasured;

        /// <summary>当前真实高度（仅 IsMeasured=true 时有效）</summary>
        public float Height;

        /// <summary>默认估计高度（未测量时使用）</summary>
        public float EstimateHeight;

        public HeightInfo(float estimateHeight)
        {
            IsMeasured = false;
            Height = 0f;
            EstimateHeight = estimateHeight;
        }

        /// <summary>获取当前有效高度（已测量返回真实值，否则返回估计值）</summary>
        public float GetEffectiveHeight()
        {
            return IsMeasured ? Height : EstimateHeight;
        }

        /// <summary>更新真实高度</summary>
        public void SetMeasuredHeight(float measuredHeight)
        {
            IsMeasured = true;
            Height = measuredHeight;
        }

        /// <summary>重置为未测量状态</summary>
        public void Reset()
        {
            IsMeasured = false;
            Height = 0f;
        }
    }
}

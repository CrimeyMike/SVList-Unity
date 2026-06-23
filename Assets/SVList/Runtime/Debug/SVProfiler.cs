using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 运行时性能分析器
    ///
    /// 统计 FPS、GC 等，不产生运行时 GC 分配。
    /// </summary>
    public class SVProfiler
    {
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _fpsUpdateInterval = 0.5f;
        private float _fpsTimeLeft;

        /// <summary>当前帧率</summary>
        public float FPS { get; private set; }

        /// <summary>GC 分配量（需要外部配合 GC.GetTotalMemory）</summary>
        public long Memory { get; set; }

        /// <summary>活跃元素数量（外部写入）</summary>
        public int ActiveCount { get; set; }

        /// <summary>对象池元素数量（外部写入）</summary>
        public int PoolCount { get; set; }

        public SVProfiler()
        {
            FPS = 60f;
            _fpsTimeLeft = _fpsUpdateInterval;
        }

        /// <summary>
        /// 每帧更新（在 LateUpdate 中调用）
        /// </summary>
        public void Update()
        {
            _fpsTimeLeft -= Time.deltaTime;
            _fpsAccumulator += Time.timeScale / Time.deltaTime;
            _fpsFrameCount++;

            if (_fpsTimeLeft <= 0f)
            {
                FPS = _fpsAccumulator / _fpsFrameCount;
                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
                _fpsTimeLeft = _fpsUpdateInterval;
            }
        }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void Reset()
        {
            FPS = 60f;
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;
            _fpsTimeLeft = _fpsUpdateInterval;
            Memory = 0L;
            ActiveCount = 0;
            PoolCount = 0;
        }
    }
}

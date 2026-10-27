using System.Collections.Generic;
using System.IO;

namespace SVList
{
    /// <summary>
    /// 内容寻址的持久化高度缓存（v1.2.0 自校准高度系统的存储层）。
    ///
    /// 键 = 布局指纹 + 文本内容（见 <see cref="SVLayoutFingerprint.ContentKey"/>），
    /// 值 = 实测高度。布局变化 → 指纹变化 → 旧键自动不命中，无需失效逻辑。
    ///
    /// - 内存层：LRU（容量 <see cref="Capacity"/>，淘汰最旧）
    /// - 磁盘层：二进制文件（persistentDataPath/svlist/height_cache.bin），
    ///   脏标记 + <see cref="FlushIfDirty(long)"/> 节流落盘（至少间隔 3s），
    ///   退出/销毁时强制 Flush。重启后历史消息重载零测量。
    /// 线程模型：仅主线程使用（TMP 测量本就在主线程）。
    /// </summary>
    public static class SVPersistentHeightCache
    {
        private const int Magic = 0x5356_4843; // "SVHC"
        private const int Version = 1;
        private const long MsWriteInterval = 3000;

        /// <summary>内存容量上限（条）。10 万条约 1.6MB 内存与磁盘。</summary>
        public static int Capacity { get; set; } = 100_000;

        private static readonly Dictionary<ulong, LinkedListNode<ulong>> _nodes
            = new Dictionary<ulong, LinkedListNode<ulong>>();
        private static readonly Dictionary<ulong, float> _map = new Dictionary<ulong, float>();
        private static readonly LinkedList<ulong> _lru = new LinkedList<ulong>(); // 头=最旧，尾=最新
        private static bool _dirty;
        private static long _lastWriteMs = long.MinValue;
        private static string _path;
        private static bool _loaded;

        /// <summary>当前条数（含磁盘载入），诊断用。</summary>
        public static int Count => _map.Count;

        private static string Path
        {
            get
            {
                if (_path == null)
                {
                    var dir = UnityEngine.Application.persistentDataPath + "/svlist";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _path = dir + "/height_cache.bin";
                }
                return _path;
            }
        }

        public static bool TryGet(ulong contentKey, out float height)
        {
            LoadOnce();
            if (!_map.TryGetValue(contentKey, out height)) return false;
            var node = _nodes[contentKey];
            _lru.Remove(node);
            _lru.AddLast(node);           // 刷新为最新
            return true;
        }

        public static void Set(ulong contentKey, float height)
        {
            LoadOnce();
            if (height <= 0f || float.IsNaN(height) || float.IsInfinity(height)) return;

            if (_map.TryGetValue(contentKey, out var old))
            {
                if (old == height) { TryGet(contentKey, out _); return; } // 仅刷新 LRU
                _map[contentKey] = height;
                _dirty = true;
                return;
            }

            if (_map.Count >= Capacity) EvictOldest();

            _map[contentKey] = height;
            var node = new LinkedListNode<ulong>(contentKey);
            _nodes[contentKey] = node;
            _lru.AddLast(node);
            _dirty = true;
        }

        private static void EvictOldest()
        {
            if (_lru.First == null) return;
            var key = _lru.First.Value;
            _map.Remove(key);
            _nodes.Remove(key);
            _lru.RemoveFirst();
        }

        /// <summary>按需落盘（距上次至少 3 秒；force 时立即）。</summary>
        public static void FlushIfDirty(long nowMs, bool force = false)
        {
            if (!_dirty) return;
            if (!force && nowMs - _lastWriteMs < MsWriteInterval) return;
            WriteFile();
            _dirty = false;
            _lastWriteMs = nowMs;
        }

        /// <summary>清空内存与磁盘（诊断/测试用）。内存清空后下次访问会重新从磁盘载入（等价进程重启）。</summary>
        public static void Clear(bool deleteFile = false)
        {
            _map.Clear();
            _nodes.Clear();
            _lru.Clear();
            _dirty = false;
            _loaded = false;
            if (deleteFile && File.Exists(Path)) File.Delete(Path);
        }

        private static void LoadOnce()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(Path)) return;
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadInt32() != Magic || r.ReadInt32() != Version) return;
                    int count = r.ReadInt32();
                    if (count > Capacity * 4) return; // 异常体积视为损坏，放弃载入
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadUInt64();
                        var h = r.ReadSingle();
                        if (h > 0f && !_map.ContainsKey(key))
                        {
                            _map[key] = h;
                            var node = new LinkedListNode<ulong>(key);
                            _nodes[key] = node;
                            _lru.AddLast(node);
                        }
                    }
                }
            }
            catch { /* 损坏文件：忽略，视为空缓存 */ }
        }

        private static void WriteFile()
        {
            try
            {
                using (var fs = new FileStream(Path, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(Magic);
                    w.Write(Version);
                    w.Write(_map.Count);
                    foreach (var kv in _map)
                    {
                        w.Write(kv.Key);
                        w.Write(kv.Value);
                    }
                }
            }
            catch { /* 磁盘失败：静默，内存缓存仍工作 */ }
        }

        /// <summary>退出钩子（SVListView OnDestroy 调用；测试可强制落盘）。</summary>
        public static void Shutdown() => FlushIfDirty(long.MaxValue, force: true);
    }
}

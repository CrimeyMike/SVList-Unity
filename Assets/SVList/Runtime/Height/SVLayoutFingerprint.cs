using System;
using System.Text;

namespace SVList
{
    /// <summary>
    /// 布局指纹：把"决定一条文本高度的全部参数"浓缩为一个稳定哈希。
    /// 用作高度缓存的键组成部分 —— 布局（字体/字号/宽度/富文本）或固定增量任一变化，
    /// 指纹即变，旧缓存自动不命中，无需手动失效。
    ///
    /// 哈希用 FNV-1a（64 位，确定性、跨会话/跨平台稳定），
    /// 不用 string.GetHashCode（其在运行时可能被随机化，持久化不安全）。
    /// </summary>
    public readonly struct SVLayoutFingerprint : IEquatable<SVLayoutFingerprint>
    {
        /// <summary>稳定哈希（FNV-1a 64 位截断为 63 位正数，便于序列化）</summary>
        public readonly ulong Hash;

        public SVLayoutFingerprint(ulong hash) => Hash = hash;

        /// <summary>从原始参数构造指纹。</summary>
        /// <param name="fontGuid">字体资产 GUID（null 视为 0）</param>
        /// <param name="fontSize">字号（含 AutoSize 生效后的实际值）</param>
        /// <param name="width">文本可用宽度（px，四舍五入到整数 —— 亚像素差异不影响换行结果但会抖动哈希）</param>
        /// <param name="richText">富文本是否启用</param>
        /// <param name="wordWrap">自动换行是否启用</param>
        /// <param name="fixedDelta">固定附加高度（Item 总高 − 动态文本高，如名字行 + 内边距）</param>
        public static SVLayoutFingerprint Create(
            string fontGuid, float fontSize, float width, bool richText, bool wordWrap, float fixedDelta)
        {
            var sb = new StringBuilder(96);
            sb.Append(fontGuid ?? "0").Append('|')
              .Append(Mathf_Round(fontSize)).Append('|')
              .Append(Mathf_Round(width)).Append('|')
              .Append(richText ? 'R' : 'r').Append('|')
              .Append(wordWrap ? 'W' : 'w').Append('|')
              .Append(Mathf_Round(fixedDelta));
            return new SVLayoutFingerprint(Fnv1a64(sb.ToString()));
        }

        private static int Mathf_Round(float v) => v >= 0 ? (int)(v + 0.5f) : (int)(v - 0.5f);

        /// <summary>FNV-1a 64 位（确定性）</summary>
        public static ulong Fnv1a64(string s)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong h = offset;
            unchecked
            {
                foreach (var c in s)
                {
                    h ^= c;
                    h *= prime;
                }
            }
            return h & 0x7FFF_FFFF_FFFF_FFFFUL;
        }

        /// <summary>内容键：同一布局指纹下，不同文本得到不同键（文本原文参与哈希）。
        /// 盐 "v2"：高度缓存模式版本——测量逻辑行为变化时递增，使历史缓存文件中的
        /// 全部旧键自然失效（v1→v2：废弃"陈旧宽度时代"写入的脏高度）。</summary>
        public ulong ContentKey(string text) => Fnv1a64(Hash.ToString("X") + "|v2|" + (text ?? string.Empty));

        public bool Equals(SVLayoutFingerprint other) => Hash == other.Hash;
        public override bool Equals(object obj) => obj is SVLayoutFingerprint f && Equals(f);
        public override int GetHashCode() => (int)(Hash ^ (Hash >> 32));
        public override string ToString() => Hash.ToString("X16");
    }
}

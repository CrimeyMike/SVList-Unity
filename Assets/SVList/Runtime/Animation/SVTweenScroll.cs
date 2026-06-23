using System.Collections;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 平滑滚动动画工具
    ///
    /// 使用协程驱动 ScrollRect 缓动到目标位置。
    /// </summary>
    public static class SVTweenScroll
    {
        /// <summary>
        /// 执行平滑滚动动画
        /// </summary>
        public static IEnumerator Animate(
            SVScrollController controller,
            float fromOffset,
            float toOffset,
            float duration,
            AnimationCurve easeCurve = null)
        {
            if (controller == null) yield break;
            if (duration <= 0f)
            {
                controller.SetScrollOffset(toOffset);
                yield break;
            }

            controller.SetScrollEnabled(false);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (easeCurve != null)
                {
                    t = easeCurve.Evaluate(t);
                }

                float current = Mathf.Lerp(fromOffset, toOffset, t);
                controller.SetScrollOffset(current);
                yield return null;
            }

            controller.SetScrollOffset(toOffset);
            controller.SetScrollEnabled(true);
        }
    }
}

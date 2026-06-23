using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 当前活跃（正在显示或待回收）的 Item
    /// </summary>
    public class ActiveItem
    {
        /// <summary>数据索引</summary>
        public int Index;

        /// <summary>Prefab识别ID（多模板支持）</summary>
        public int PrefabID;

        /// <summary>当前状态</summary>
        public ItemState State;

        /// <summary>Item的RectTransform</summary>
        public RectTransform Rect;

        /// <summary>Item的渲染器接口</summary>
        public ISVItemRenderer Renderer;

        /// <summary>GameObject引用（用于SetActive等操作）</summary>
        public GameObject GameObject;

        public void SetActive(int index, int prefabID, RectTransform rect, ISVItemRenderer renderer, GameObject go)
        {
            Index = index;
            PrefabID = prefabID;
            State = ItemState.Active;
            Rect = rect;
            Renderer = renderer;
            GameObject = go;
        }

        public void MarkForRecycle()
        {
            State = ItemState.PendingRecycle;
        }

        public void MarkPooled()
        {
            State = ItemState.Pooled;
        }
    }
}

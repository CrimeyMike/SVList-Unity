using System.Collections.Generic;
using UnityEngine;

namespace SVList.Examples
{
    /// <summary>
    /// 最小列表驱动（垂直 / 水平模板用）：
    /// 把模板 prefab 拖进 Canvas 下，Play 即见数据列表。
    /// 增删 / 滚动控制的常用 API 见包内 README「API 速查」。
    /// </summary>
    public class SimpleListDriver : MonoBehaviour
    {
        [SerializeField] private SVListView _view;
        [SerializeField] private int _itemCount = 1000;

        private readonly List<SimpleListItemData> _data = new List<SimpleListItemData>();

        private void Start()
        {
            for (int i = 0; i < _itemCount; i++)
                _data.Add(new SimpleListItemData($"Item {i}"));

            _view.Initialize(_data);
        }
    }
}

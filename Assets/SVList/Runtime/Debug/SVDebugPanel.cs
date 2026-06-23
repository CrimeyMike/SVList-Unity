using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 运行时调试面板
    ///
    /// 在 Game 视图左上角显示虚拟列表运行状态。
    /// 读取 DebugInfo 而非直接访问各模块，避免耦合。
    /// </summary>
    public class SVDebugPanel : MonoBehaviour
    {
        [SerializeField] private SVListView _targetList;
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _textColor = Color.green;
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.5f);

        private bool _isVisible;
        private DebugInfo _debugInfo;
        private GUIStyle _style;
        private GUIStyle _bgStyle;

        private void Start()
        {
            _isVisible = _showOnStart;
        }

        private void OnGUI()
        {
            if (!_isVisible || _targetList == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = _fontSize;
                _style.normal.textColor = _textColor;
                _style.fontStyle = FontStyle.Bold;

                _bgStyle = new GUIStyle(GUI.skin.box);
                _bgStyle.normal.background = MakeTex(2, 2, _bgColor);
            }

            _debugInfo = _targetList.DebugInfo;
            if (_debugInfo == null) return;

            string info = $@"[SVList Debug]
FPS: {_debugInfo.FPS:F1}
Total: {_debugInfo.TotalCount}
Active: {_debugInfo.ActiveCount}
Pool: {_debugInfo.PoolCount}
CreateQueue: {_debugInfo.CreateQueueCount}
RecycleQueue: {_debugInfo.RecycleQueueCount}
Visible: [{_debugInfo.FirstVisibleIndex}..{_debugInfo.LastVisibleIndex}]
Scroll: {_debugInfo.ScrollOffset:F0}
ContentSize: {_debugInfo.ContentSize:F0}
Memory: {FormatBytes(_debugInfo.Memory)}";

            float width = 250f;
            float height = 200f;
            Rect rect = new Rect(10, 10, width, height);

            GUI.Box(rect, "", _bgStyle);
            GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, rect.height - 10), info, _style);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _isVisible = !_isVisible;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}

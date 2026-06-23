using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SVList
{
    /// <summary>
    /// SVListView —— MonoBehaviour 入口点
    ///
    /// 挂载在 ScrollRect 所在 GameObject 上。
    /// 负责：
    /// - 持有 SVListCore（纯逻辑核心）
    /// - 管理 ScrollRect、Content、Viewport 引用
    /// - 生命周期管理（Awake、Update、LateUpdate、Destroy）
    /// - 提供业务层 API
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    [DisallowMultipleComponent]
    public class SVListView : MonoBehaviour
    {
        #region 序列化字段

        [Header("References")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;

        [Header("Prefabs")]
        [SerializeField] private List<GameObject> _itemPrefabs = new List<GameObject>();

        [Header("Config")]
        [SerializeField] private SVConfig _config;

        #endregion

        #region 私有字段

        private SVListCore _core;
        private SVObjectPoolManager _poolManager;
        private SVScrollController _scrollController;
        private SVProfiler _profiler;
        private ISVDataSource _dataSource;
        private Transform _poolRoot;

        #endregion

        #region 属性

        /// <summary>核心逻辑实例</summary>
        public SVListCore Core => _core;

        /// <summary>当前数据源</summary>
        public ISVDataSource DataSource => _dataSource;

        /// <summary>配置</summary>
        public SVConfig Config => _config;

        /// <summary>调试信息</summary>
        public DebugInfo DebugInfo => _core?.DebugInfo;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            // 确保必要的引用
            if (_scrollRect == null)
                _scrollRect = GetComponent<ScrollRect>();

            if (_viewport == null && _scrollRect != null)
                _viewport = _scrollRect.viewport;

            if (_content == null && _scrollRect != null)
                _content = _scrollRect.content;

            if (_config == null)
                _config = new SVConfig();

            // 创建池根节点
            CreatePoolRoot();

            // 初始化核心
            InitializeCore();

            // 创建性能分析器
            _profiler = new SVProfiler();
        }

        private void CreatePoolRoot()
        {
            var poolGo = new GameObject("PoolRoot");
            poolGo.SetActive(true);
            poolGo.transform.SetParent(transform);
            _poolRoot = poolGo.transform;
        }

        private void InitializeCore()
        {
            _core = new SVListCore();

            // 初始化 ScrollController
            _scrollController = GetComponent<SVScrollController>();
            if (_scrollController == null)
                _scrollController = gameObject.AddComponent<SVScrollController>();

            bool isVertical = _config.Direction == SVDirection.Vertical ||
                             _config.Direction == SVDirection.Grid;
            _scrollController.Initialize(isVertical, _config.Reverse);

            // 初始化对象池
            _poolManager = new SVObjectPoolManager(_poolRoot, _config.MaxPoolSize);
            RegisterPrefabs();

            // 注册 Prefab 选择器
            _core.PrefabSelector = SelectPrefab;

            // 注册数据源提供者
            _core.DataSourceProvider = () => _dataSource;

            // 注册回调
            _core.OnContentSizeChanged = OnContentSizeChanged;
            _core.OnScrollPositionChanged = OnScrollPositionChanged;
            _core.OnDebugInfoUpdated = OnDebugInfoUpdated;

            // 设置 Viewport 尺寸
            float viewportSize = _scrollController.ViewportSize;
            _core.SetViewportSize(viewportSize);

            // 连接 ScrollController
            _scrollController.OnScrollOffsetChanged += OnScrollOffsetChanged;
        }

        private void RegisterPrefabs()
        {
            for (int i = 0; i < _itemPrefabs.Count; i++)
            {
                if (_itemPrefabs[i] != null)
                {
                    _poolManager.RegisterPrefab(i, _itemPrefabs[i]);
                }
            }

            // 预热
            for (int i = 0; i < _itemPrefabs.Count; i++)
            {
                if (_itemPrefabs[i] != null && _config.PreWarmCount > 0)
                {
                    _poolManager.PreWarm(i, _config.PreWarmCount / _itemPrefabs.Count);
                }
            }
        }

        private void Start()
        {
            if (_dataSource != null)
            {
                Initialize(_dataSource);
            }
        }

        private void LateUpdate()
        {
            if (_core != null && _core.IsInitialized)
            {
                _core.LateUpdate();
                _profiler?.Update();
            }
        }

        private void OnDestroy()
        {
            if (_scrollController != null)
            {
                _scrollController.OnScrollOffsetChanged -= OnScrollOffsetChanged;
            }

            _core?.Dispose();
            _poolManager?.Dispose();
        }

        #endregion

        #region 滚动回调

        private void OnScrollOffsetChanged(float scrollOffset)
        {
            _core?.OnScrollChanged(scrollOffset);
        }

        private void OnContentSizeChanged(float contentSize)
        {
            if (_scrollController != null)
            {
                _scrollController.SetContentSize(contentSize);
            }
        }

        private void OnScrollPositionChanged(float scrollOffset)
        {
            if (_scrollController != null)
            {
                _scrollController.SetScrollOffset(scrollOffset);
            }
        }

        private int _debugFrameCounter;

        private void OnDebugInfoUpdated(DebugInfo info)
        {
            // 更新 Profiler
            info.FPS = _profiler?.FPS ?? 0f;

            // GC.GetTotalMemory 开销较大，每 60 帧才调一次
            _debugFrameCounter++;
            if (_debugFrameCounter >= 60)
            {
                info.Memory = System.GC.GetTotalMemory(false);
                _debugFrameCounter = 0;
            }
        }

        #endregion

        #region 公开 API

        /// <summary>
        /// 初始化列表（使用泛型数据源）
        /// </summary>
        public void Initialize<T>(IList<T> dataList, Func<int, int> prefabSelector = null)
        {
            var dataSource = new ListDataSource<T>(dataList);
            _dataSource = dataSource;

            // 用用户提供的选择器覆盖默认（为 null 则保持默认 SelectPrefab）
            if (prefabSelector != null)
                _core.PrefabSelector = prefabSelector;
            else
                _core.PrefabSelector = SelectPrefab;

            // 更新 Viewport 尺寸（此时 Canvas 已布局完成）
            if (_scrollController != null)
                _core.SetViewportSize(_scrollController.ViewportSize);

            _core.Initialize(_config, _dataSource, _poolManager, _content);

            // 立即触发首次渲染
            _core.TriggerInitialRender();
        }

        /// <summary>
        /// 初始化列表（使用 ISVDataSource）
        /// </summary>
        public void Initialize(ISVDataSource dataSource)
        {
            _dataSource = dataSource;

            // 更新 Viewport 尺寸
            if (_scrollController != null)
                _core.SetViewportSize(_scrollController.ViewportSize);

            _core.Initialize(_config, dataSource, _poolManager, _content);

            // 立即触发首次渲染
            _core.TriggerInitialRender();
        }

        /// <summary>
        /// 刷新整个列表
        /// </summary>
        public void Refresh()
        {
            _core?.Refresh();
        }

        /// <summary>
        /// 刷新单个 Item
        /// </summary>
        public void RefreshItem(int index)
        {
            _core?.RefreshItem(index);
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        public void InsertItem(int index)
        {
            _core?.InsertItem(index);
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        public void RemoveItem(int index)
        {
            _core?.RemoveItem(index);
        }

        /// <summary>
        /// 跳转到指定索引
        /// </summary>
        public void JumpToIndex(int index, SVJumpMode mode = SVJumpMode.Top)
        {
            _core?.JumpToIndex(index, mode);
        }

        /// <summary>
        /// 平滑跳转到指定索引
        /// </summary>
        public void JumpToIndexSmooth(int index, SVJumpMode mode = SVJumpMode.Top)
        {
            if (_core == null || _scrollController == null) return;

            float targetOffset = _core.HeightCache.GetOffset(index);
            float viewportSize = _scrollController.ViewportSize;
            float currentOffset = _scrollController.ScrollOffset;

            switch (mode)
            {
                case SVJumpMode.Center:
                    targetOffset -= viewportSize * 0.5f;
                    break;
                case SVJumpMode.Bottom:
                    targetOffset -= viewportSize;
                    break;
            }

            targetOffset = Mathf.Max(0f, targetOffset);

            StartCoroutine(SVTweenScroll.Animate(
                _scrollController,
                currentOffset,
                targetOffset,
                _config.JumpDuration,
                _config.JumpEaseCurve));
        }

        /// <summary>
        /// 获取当前活跃的 Renderer
        /// </summary>
        public ISVItemRenderer GetRenderer(int index)
        {
            var container = _core?.State;
            // 通过 Core 的 ItemContainer 获取
            return null; // 需要暴露 ItemContainer
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void ClearPool()
        {
            _poolManager?.ClearAll();
        }

        /// <summary>
        /// 设置配置
        /// </summary>
        public void SetConfig(SVConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 设置元素间距（Vertical/Horizontal 模式）
        /// </summary>
        public void SetSpacing(float spacing)
        {
            if (_config == null) return;
            _config.Spacing = spacing;
            _core?.UpdateLayoutParameters();
        }

        /// <summary>
        /// 设置顶部和底部内边距
        /// </summary>
        public void SetPadding(float top, float bottom)
        {
            if (_config == null) return;
            _config.PaddingTop = top;
            _config.PaddingBottom = bottom;
            _core?.UpdateLayoutParameters();
        }

        /// <summary>
        /// 设置四向内边距
        /// </summary>
        public void SetPaddingAll(float top, float bottom, float left, float right)
        {
            if (_config == null) return;
            _config.PaddingTop = top;
            _config.PaddingBottom = bottom;
            _config.PaddingLeft = left;
            _config.PaddingRight = right;
            _core?.UpdateLayoutParameters();
        }

        /// <summary>
        /// 设置网格参数（仅 Grid 模式生效）
        /// </summary>
        public void SetGridParams(int columns, float cellWidth, float cellHeight,
            float horizontalSpacing, float verticalSpacing)
        {
            if (_config == null) return;
            _config.GridColumnCount = columns;
            _config.GridCellWidth = cellWidth;
            _config.GridCellHeight = cellHeight;
            _config.GridHorizontalSpacing = horizontalSpacing;
            _config.GridVerticalSpacing = verticalSpacing;
            _core?.UpdateLayoutParameters();
        }

        /// <summary>
        /// 更新布局参数（修改 config 后手动调用以应用变更）
        /// </summary>
        public void ApplyLayoutChanges()
        {
            _core?.UpdateLayoutParameters();
        }

        #endregion

        #region Prefab 选择

        private int SelectPrefab(int index)
        {
            // 默认选择器：统一返回第一个 Prefab（ID=0）
            // 多模板由用户通过 Initialize<T>() 的 prefabSelector 参数注入
            return 0;
        }

        #endregion

#if UNITY_EDITOR
        private static readonly Color GIZMO_VIEWPORT = new Color(0f, 1f, 0f, 0.8f);
        private static readonly Color GIZMO_CONTENT = new Color(1f, 0.92f, 0.016f, 0.6f);
        private static readonly Color GIZMO_BUFFER = new Color(0f, 1f, 1f, 0.4f);
        private static readonly Color GIZMO_ACTIVE_ITEM = new Color(0.3f, 0.6f, 1f, 0.5f);
        private static readonly Color GIZMO_ACTIVE_ITEM_BORDER = new Color(0.2f, 0.5f, 0.9f, 0.9f);
        private static readonly Color GIZMO_GRID_LINE = new Color(1f, 0f, 1f, 0.3f);
        private static readonly Color GIZMO_MEASURED = new Color(0f, 0.8f, 0f, 0.7f);
        private static readonly Color GIZMO_ESTIMATED = new Color(1f, 0.6f, 0f, 0.7f);
        private static readonly Color GIZMO_SCROLL_POS = new Color(1f, 0.3f, 0.3f, 0.8f);

        // GUIStyle 不能作为 static field initializer 在 MonoBehaviour 中创建（Unity 限制）
        // 使用 lazy initialization 解决
        private static GUIStyle _gizmoLabelStyle;
        private static GUIStyle GIZMO_LABEL_STYLE
        {
            get
            {
                if (_gizmoLabelStyle == null)
                {
                    _gizmoLabelStyle = new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.white },
                        fontSize = 10,
                        alignment = TextAnchor.MiddleLeft,
                        fontStyle = FontStyle.Bold
                    };
                }
                return _gizmoLabelStyle;
            }
        }

        private static GUIStyle _gizmoStatStyle;
        private static GUIStyle GIZMO_STAT_STYLE
        {
            get
            {
                if (_gizmoStatStyle == null)
                {
                    _gizmoStatStyle = new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = new Color(1f, 1f, 0.8f) },
                        fontSize = 11,
                        alignment = TextAnchor.UpperLeft,
                        fontStyle = FontStyle.Normal
                    };
                }
                return _gizmoStatStyle;
            }
        }

        private void OnDrawGizmos()
        {
            if (_config == null || !_config.ShowGizmos) return;

            if (_viewport != null)
            {
                // ── Viewport 边界 ──
                DrawRect(_viewport, GIZMO_VIEWPORT);
                LabelRectTopLeft(_viewport, "Viewport", GIZMO_VIEWPORT);
            }

            if (_content == null || _core == null) return;

            if (_core.IsInitialized)
            {
                // ── Content 边界（计算的虚拟总尺寸） ──
                DrawContentBounds();
            }
            else
            {
                // 未初始化：仅绘制 Content 的 RectTransform 边界
                DrawRect(_content, GIZMO_CONTENT);
            }
        }

        /// <summary>
        /// 绘制 Content 总范围（基于计算的 ContentSize + Viewport/Content 实际变换）
        /// </summary>
        private void DrawContentBounds()
        {
            var state = _core.State;
            var layout = _core.Layout;
            if (state == null || layout == null) return;

            // 用 Viewport 的 world corners 作为基准参考系
            Vector3[] vpCorners = new Vector3[4];
            _viewport.GetWorldCorners(vpCorners);
            Vector3 vpTopLeft = vpCorners[1]; // Canvas overlay 下 0=bottomLeft,1=topLeft,2=topRight,3=bottomRight

            float contentHeight = layout.GetContentSize();
            float viewportHeight = _viewport.rect.height;

            // Content 从 vpTopLeft.y 向下延伸 contentHeight
            bool isHorizontal = _config.Direction == SVDirection.Horizontal;
            float contentWidth = isHorizontal ? layout.GetContentSize() : _viewport.rect.width;

            Vector3 contentTopLeft = vpTopLeft;
            Vector3 contentTopRight = vpTopLeft + Vector3.right * contentWidth;
            Vector3 contentBottomRight = contentTopRight + Vector3.down * contentHeight;
            Vector3 contentBottomLeft = contentTopLeft + Vector3.down * contentHeight;

            Gizmos.color = GIZMO_CONTENT;
            Gizmos.DrawLine(contentTopLeft, contentTopRight);
            Gizmos.DrawLine(contentTopRight, contentBottomRight);
            Gizmos.DrawLine(contentBottomRight, contentBottomLeft);
            Gizmos.DrawLine(contentBottomLeft, contentTopLeft);

            // Content 标签
            Handles.Label(contentTopLeft + Vector3.right * 2f + Vector3.down * 2f,
                $"Content: {contentHeight:F0}px ({state.TotalCount} items)", GIZMO_LABEL_STYLE);

            // ── 滚动位置指示线 ──
            float scrollY = vpTopLeft.y - state.ScrollOffset;
            Vector3 scrollLineLeft = new Vector3(vpTopLeft.x - 15f, scrollY, 0f);
            Vector3 scrollLineRight = new Vector3(vpTopLeft.x - 5f, scrollY, 0f);
            Gizmos.color = GIZMO_SCROLL_POS;
            Gizmos.DrawLine(scrollLineLeft, scrollLineRight);

            // ── Buffer 区域 ──
            // Viewport 区域（半透明填充指示）
            DrawRectWire(vpTopLeft, viewportHeight, contentWidth, GIZMO_VIEWPORT, "Viewport");

            if (_config.ShowDetailedGizmos)
            {
                DrawDetailedGizmos(contentTopLeft, contentHeight, contentWidth, viewportHeight);
            }

            // ── Buffer 标签 ──
            float bufferSize = viewportHeight * (_config.PreloadFactor > 0f ? _config.PreloadFactor * 3f : 1f);
            Handles.Label(new Vector3(vpTopLeft.x - 5f, vpTopLeft.y - viewportHeight * 0.5f, 0f),
                $"Buf:{bufferSize:F0}px", GIZMO_LABEL_STYLE);

            // ── 可见范围索引 ──
            var visibleRange = _core.CurrentVisibleRange;
            if (visibleRange.Count > 0)
            {
                Vector3 rangeLabelPos = new Vector3(vpTopLeft.x + contentWidth + 5f,
                    vpTopLeft.y - viewportHeight * 0.5f, 0f);
                Handles.Label(rangeLabelPos,
                    $"Visible: [{visibleRange.First}..{visibleRange.Last}]\nActive: {_core.DebugInfo.ActiveCount}",
                    GIZMO_STAT_STYLE);
            }
        }

        // 帧跳过：详细 Gizmos 每 N 帧才刷新一次，大幅降低 Editor 开销
        private static int _gizmoFrameCounter;
        private const int GIZMO_DETAIL_FRAME_SKIP = 4;
        private const int LABEL_MAX_ITEMS = 60; // 超过此数量不绘制单个 Item 标签

        /// <summary>
        /// 详细 Gizmos：活跃元素矩形、网格线、高度状态等
        /// </summary>
        private void DrawDetailedGizmos(Vector3 contentTopLeft,
            float contentHeight, float contentWidth, float viewportHeight)
        {
            var state = _core.State;
            var heightCache = _core.HeightCache;
            var activeItems = _core.GetActiveItems();
            if (activeItems == null) return;

            int activeCount = _core.DebugInfo?.ActiveCount ?? 0;
            bool showLabels = activeCount <= LABEL_MAX_ITEMS;

            // 每 N 帧才绘制逐元素 Gizmos（网格线、统计面板每帧都绘制）
            bool drawPerItem = (_gizmoFrameCounter % GIZMO_DETAIL_FRAME_SKIP) == 0;
            _gizmoFrameCounter++;

            // ── 每个活跃元素的线框 ──
            // 预先收集首/尾元素的 Y 坐标，避免二次遍历
            var range = _core.CurrentVisibleRange;
            float firstY = float.MinValue, lastY = float.MaxValue;

            if (drawPerItem)
            {
                foreach (var item in activeItems)
                {
                    if (item.Rect == null) continue;

                    Vector3[] corners = new Vector3[4];
                    item.Rect.GetWorldCorners(corners);

                    // ★ 唯一矩形边框（无填充 DrawCube，无 DrawSphere）
                    Gizmos.color = GIZMO_ACTIVE_ITEM_BORDER;
                    for (int i = 0; i < 4; i++)
                        Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);

                    // 索引标签（仅在总数较少时绘制，避免大量 Handles.Label 调用）
                    if (showLabels)
                    {
                        Vector3 labelPos = corners[1] + Vector3.right * 3f + Vector3.down * 10f;
                        Handles.Label(labelPos, $"#{item.Index}", GIZMO_LABEL_STYLE);
                    }

                    // 高度状态指示（用短线段代替 DrawSphere，零填充几何体开销）
                    var heightInfo = heightCache?.GetInfo(item.Index);
                    if (heightInfo != null)
                    {
                        Gizmos.color = heightInfo.IsMeasured ? GIZMO_MEASURED : GIZMO_ESTIMATED;
                        Vector3 dotPos = corners[0] + Vector3.right * 8f + Vector3.up * 4f;
                        float s = 3f;
                        // 小十字代替球体：2 条线 = 2 次绘制调用 vs 球体的数百面
                        Gizmos.DrawLine(dotPos + Vector3.left * s, dotPos + Vector3.right * s);
                        Gizmos.DrawLine(dotPos + Vector3.down * s, dotPos + Vector3.up * s);
                    }

                    // ★ 同时收集首/尾 Y 坐标，避免二次遍历
                    if (item.Index == range.First) firstY = corners[1].y;
                    if (item.Index == range.Last) lastY = corners[0].y;
                }
            }

            // ── Grid 模式：绘制列分隔线 ──
            if (_config.Direction == SVDirection.Grid && _config.GridColumnCount > 1)
            {
                int cols = _config.GridColumnCount;
                float cellW = _config.GridCellWidth;
                float hSpacing = _config.GridHorizontalSpacing;
                float padLeft = _config.PaddingLeft;

                Gizmos.color = GIZMO_GRID_LINE;
                for (int c = 0; c <= cols; c++)
                {
                    float x = contentTopLeft.x + padLeft + c * (cellW + hSpacing) - hSpacing * 0.5f;
                    Vector3 top = new Vector3(x, contentTopLeft.y, 0f);
                    Vector3 bottom = new Vector3(x, contentTopLeft.y - contentHeight, 0f);
                    DrawDashedLine(top, bottom, 15f);
                }

                Handles.Label(new Vector3(contentTopLeft.x + padLeft + cellW * 0.5f,
                    contentTopLeft.y + 10f, 0f),
                    $"Grid: {cols} cols x {cellW:F0}px", GIZMO_LABEL_STYLE);
            }

            // ── 行高线（Grid 固定行高模式） ──
            if (_config.Direction == SVDirection.Grid && _config.GridCellHeight > 0f)
            {
                float rowH = _config.GridCellHeight;
                float vSpacing = _config.GridVerticalSpacing;
                float padTop = _config.PaddingTop;

                int totalRows = (state.TotalCount + _config.GridColumnCount - 1) / _config.GridColumnCount;
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);

                for (int r = 0; r <= totalRows; r++)
                {
                    float y = contentTopLeft.y - padTop - r * (rowH + vSpacing) + vSpacing * 0.5f;
                    Vector3 left = new Vector3(contentTopLeft.x, y, 0f);
                    Vector3 right = new Vector3(contentTopLeft.x + contentWidth, y, 0f);
                    DrawDashedLine(left, right, 10f);
                }
            }

            // ── 可见范围边线 ──
            if (range.Count > 0)
            {
                if (firstY > float.MinValue / 2f)
                {
                    Vector3 l = new Vector3(contentTopLeft.x - 30f, firstY, 0f);
                    Vector3 r = new Vector3(contentTopLeft.x + contentWidth + 5f, firstY, 0f);
                    Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
                    Gizmos.DrawLine(l, r);
                    Handles.Label(l + Vector3.up * 2f, $"First #{range.First}", GIZMO_LABEL_STYLE);
                }
                if (lastY < float.MaxValue / 2f)
                {
                    Vector3 l = new Vector3(contentTopLeft.x - 30f, lastY, 0f);
                    Vector3 r = new Vector3(contentTopLeft.x + contentWidth + 5f, lastY, 0f);
                    Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                    Gizmos.DrawLine(l, r);
                    Handles.Label(l + Vector3.down * 12f, $"Last #{range.Last}", GIZMO_LABEL_STYLE);
                }
            }

            // ── 调试统计面板（每帧更新） ──
            var dbg = _core.DebugInfo;
            if (dbg != null)
            {
                Vector3 statPos = new Vector3(
                    contentTopLeft.x + contentWidth + 10f,
                    contentTopLeft.y - viewportHeight - 10f,
                    0f);
                string stats = $"Active: {dbg.ActiveCount}  Pool: {dbg.PoolCount}  Total: {dbg.TotalCount}\n" +
                               $"CreateQ: {dbg.CreateQueueCount}  RecycleQ: {dbg.RecycleQueueCount}\n" +
                               $"FPS: {dbg.FPS:F1}  Scroll: {dbg.ScrollOffset:F0}px\n" +
                               $"State: {_core.VisibleState}";
                Handles.Label(statPos, stats, GIZMO_STAT_STYLE);
            }
        }

        /// <summary>
        /// 绘制带色边框的矩形（世界坐标）
        /// </summary>
        private void DrawRect(RectTransform rect, Color color)
        {
            if (rect == null) return;

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Gizmos.color = color;
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }

        /// <summary>
        /// 绘制世界坐标矩形线框
        /// </summary>
        private void DrawRectWire(Vector3 topLeft, float height, float width, Color color, string label = null)
        {
            Vector3 tl = topLeft;
            Vector3 tr = topLeft + Vector3.right * width;
            Vector3 br = topLeft + Vector3.right * width + Vector3.down * height;
            Vector3 bl = topLeft + Vector3.down * height;

            Gizmos.color = color;
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);

            if (!string.IsNullOrEmpty(label))
            {
                Handles.Label(tl + Vector3.right * 3f + Vector3.down * 3f, label, GIZMO_LABEL_STYLE);
            }
        }

        /// <summary>
        /// 在 RectTransform 左上角标签
        /// </summary>
        private void LabelRectTopLeft(RectTransform rect, string label, Color color)
        {
            if (rect == null) return;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 topLeft = corners[1];
            Handles.Label(topLeft + Vector3.right * 3f + Vector3.down * 3f, label,
                new GUIStyle(GIZMO_LABEL_STYLE) { normal = { textColor = color } });
        }

        /// <summary>
        /// 绘制虚线（使用当前 Gizmos.color）
        /// </summary>
        private static void DrawDashedLine(Vector3 from, Vector3 to, float dashLength)
        {
            Vector3 dir = (to - from).normalized;
            float totalLength = Vector3.Distance(from, to);
            float drawn = 0f;

            while (drawn < totalLength)
            {
                float segmentLength = Mathf.Min(dashLength, totalLength - drawn);
                Vector3 segStart = from + dir * drawn;
                Vector3 segEnd = segStart + dir * segmentLength;
                Gizmos.DrawLine(segStart, segEnd);
                drawn += dashLength * 2f;
            }
        }
#endif
    }

    /// <summary>
    /// 基于 List 的内置数据源实现
    /// </summary>
    internal class ListDataSource<T> : ISVDataSource<T>
    {
        private IList<T> _data;

        public ListDataSource(IList<T> data)
        {
            _data = data;
        }

        public int GetItemCount()
        {
            return _data?.Count ?? 0;
        }

        public T GetItemData(int index)
        {
            if (_data == null || index < 0 || index >= _data.Count)
                return default;
            return _data[index];
        }

        object ISVDataSource.GetItemData(int index)
        {
            return GetItemData(index);
        }
    }
}

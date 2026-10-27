using System;
using System.Collections.Generic;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// SVListCore —— 纯逻辑核心，不继承 MonoBehaviour
    ///
    /// 负责：
    /// - Index 计算 / Offset 换算
    /// - 协调所有子系统（Layout, Visible, HeightCache, ObjectPool, Scheduler, Container）
    /// - Diff 算法结果转换为实际的创建/回收操作
    ///
    /// 不依赖 GameObject，方便单元测试。
    /// </summary>
    public class SVListCore
    {
        #region 子系统引用

        private SVListState _state;
        private SVHeightCache _heightCache;
        private SVVisibleManager _visibleManager;
        private SVItemContainer _itemContainer;
        private SVObjectPoolManager _poolManager;
        private SVInstantiateScheduler _instantiateScheduler;
        private SVRecycleScheduler _recycleScheduler;
        private ISVLayout _layout;
        private SVConfig _config;
        private Transform _contentTransform;

        // 复用的临时列表，避免 GC（Refresh / Dispose 中使用）
        private List<ActiveItem> _tempItemList = new List<ActiveItem>(64);

        // 缓存的委托，避免每帧方法组转换产生 GC
        private Func<CreateRequest, bool> _cachedCreateAction;
        private Action<RecycleRequest> _cachedRecycleAction;

        #endregion

        #region 外部回调

        /// <summary>Item 创建回调（由 View 层提供）</summary>
        public Func<int, int, ISVItemRenderer> OnCreateItem;

        /// <summary>Item 实例化并绑定后的通知（v1.2.0：自校准高度系统从此采集模板布局指纹）</summary>
        public Action<ISVItemRenderer, int> OnItemInstantiated;

        /// <summary>Item 回收回调</summary>
        public Action<ISVItemRenderer, int> OnRecycleItem;

        /// <summary>Content 尺寸更新回调</summary>
        public Action<float> OnContentSizeChanged;

        /// <summary>滚动位置更新回调</summary>
        public Action<float> OnScrollPositionChanged;

        /// <summary>
        /// 滚动位置增量平移回调（插入/删除补偿专用）。
        /// 与 OnScrollPositionChanged 的区别：走 content 增量平移而非 normalizedPosition，
        /// 不会清掉 ScrollRect 的惯性速度（normalized setter 内部会 m_Velocity=0）。
        /// </summary>
        public Action<float> OnScrollOffsetShift;

        /// <summary>调试信息更新回调</summary>
        public Action<DebugInfo> OnDebugInfoUpdated;

        #endregion

        #region 属性

        public SVListState State => _state;
        public SVHeightCache HeightCache => _heightCache;
        public ISVLayout Layout => _layout;
        public DebugInfo DebugInfo { get; private set; }
        public bool IsInitialized => _state != null && _state.Status == SVListStatus.Running;

        /// <summary>当前可见范围（供 Gizmos/Debug 使用）</summary>
        public VisibleRange CurrentVisibleRange => _visibleManager?.CurrentRange ?? VisibleRange.Empty;

        /// <summary>上一帧可见范围（供 Gizmos/Debug 使用）</summary>
        public VisibleRange PreviousVisibleRange => _visibleManager?.PreviousRange ?? VisibleRange.Empty;

        /// <summary>当前可见状态（供 Gizmos/Debug 使用）</summary>
        public VisibleState VisibleState => _visibleManager?.State ?? VisibleState.Stable;

        /// <summary>获取所有活跃元素（供 Gizmos/Debug 遍历，注意：返回的是实时集合，不可缓存）</summary>
        public System.Collections.Generic.Dictionary<int, ActiveItem>.ValueCollection GetActiveItems()
        {
            return _itemContainer?.GetActiveItems();
        }

        #endregion

        public SVListCore()
        {
            _state = new SVListState();
            DebugInfo = new DebugInfo();
        }

        #region 初始化

        /// <summary>
        /// 初始化核心
        /// </summary>
        public void Initialize(SVConfig config, ISVDataSource dataSource,
            SVObjectPoolManager poolManager, Transform contentTransform)
        {
            // 幂等保护：重复 Initialize 时先回收上一轮的活跃元素与队列，
            // 否则旧 item 变孤儿挂在 Content 下，与新 item 重叠显示
            if (_state.Status == SVListStatus.Running)
            {
                _tempItemList.Clear();
                if (_itemContainer != null)
                {
                    foreach (var item in _itemContainer.GetActiveItems())
                        _tempItemList.Add(item);
                    foreach (var item in _tempItemList)
                        RecycleItem(item);
                    _itemContainer.Clear();
                }
                _instantiateScheduler?.Clear();
                _recycleScheduler?.Clear();
                _state.Reset();
            }

            _config = config;
            _poolManager = poolManager;
            _contentTransform = contentTransform;

            _state.Status = SVListStatus.Initializing;

            // 创建高度缓存
            float estimateHeight = config.EstimateHeight > 0f ? config.EstimateHeight : 100f;
            _heightCache = new SVHeightCache(estimateHeight);

            // 自动识别数据源实现的高度提供者
            if (HeightProvider == null && dataSource is ISVItemHeightProvider provider)
            {
                HeightProvider = provider;
            }

            // 初始化数据计数
            int totalCount = dataSource.GetItemCount();
            _state.TotalCount = totalCount;

            // Grid 动态行高：HeightCache 按行数初始化（每行一个高度记录）；
            // Provider 按 item 粒度提供高度，不适用于 Grid 行高，Grid 保持估算
            if (config.Direction == SVDirection.Grid && config.GridCellHeight <= 0f)
            {
                int rowCount = (totalCount + config.GridColumnCount - 1) / Mathf.Max(1, config.GridColumnCount);
                _heightCache.Initialize(rowCount);
            }
            else
            {
                _heightCache.Initialize(totalCount,
                    _cachedHeightGetter);
            }

            // 创建布局
            CreateLayout();

            // 计算 buffer 大小
            float bufferSize = estimateHeight * 2f; // 默认 buffer = 2个cell高度
            if (config.PreloadFactor > 0f)
            {
                bufferSize = estimateHeight * config.PreloadFactor * 6f; // 根据 PreloadFactor 调整
            }

            // 创建可见区管理器
            _visibleManager = new SVVisibleManager(_layout, _heightCache, bufferSize);

            // 创建元素容器
            _itemContainer = new SVItemContainer(64);

            // 创建调度器
            _instantiateScheduler = new SVInstantiateScheduler(config.MaxInstantiatePerFrame);
            _recycleScheduler = new SVRecycleScheduler();

            // 缓存委托，避免每帧分配
            _cachedCreateAction = ProcessCreateRequest;
            _cachedRecycleAction = ProcessRecycleRequest;

            _state.Status = SVListStatus.Running;
        }

        /// <summary>
        /// 触发首次渲染（在 Initialize 后由 SVListView 调用）
        /// </summary>
        public void TriggerInitialRender()
        {
            if (_state.Status != SVListStatus.Running) return;

            // 确保 ViewportSize 有效
            if (_state.ViewportSize <= 0f)
                _state.ViewportSize = 1080f; // 默认值

            // 先设置 Content 大小，ScrollRect 才能正常工作
            _state.Dirty = true;
            UpdateContentSize();

            // 触发首次可见区计算和渲染
            OnScrollChanged(0f);
        }

        /// <summary>
        /// 创建对应布局
        /// </summary>
        private void CreateLayout()
        {
            switch (_config.Direction)
            {
                case SVDirection.Vertical:
                    var vLayout = new SVVerticalLayout(
                        _heightCache,
                        _config.Spacing,
                        _config.PaddingTop,
                        _config.PaddingBottom,
                        _config.DefaultItemSize);
                    _layout = vLayout;
                    break;

                case SVDirection.Horizontal:
                    var hLayout = new SVHorizontalLayout(
                        _heightCache,
                        _config.Spacing,
                        _config.PaddingLeft,
                        _config.PaddingRight,
                        _config.DefaultItemSize);
                    _layout = hLayout;
                    break;

                case SVDirection.Grid:
                    var gLayout = new SVGridLayout(
                        _heightCache,
                        _config.GridColumnCount,
                        _config.GridCellWidth,
                        _config.GridCellHeight,
                        _config.GridHorizontalSpacing,
                        _config.GridVerticalSpacing,
                        _config.PaddingLeft,
                        _config.PaddingRight,
                        _config.PaddingTop,
                        _config.PaddingBottom);
                    _layout = gLayout;
                    break;
            }
        }

        #endregion

        #region 主更新循环

        private bool _isProcessingScroll;

        /// <summary>
        /// 处理滚动变化（从 ScrollController 回调）
        /// </summary>
        public void OnScrollChanged(float scrollOffset)
        {
            if (_state.Status != SVListStatus.Running) return;

            // ★ 重入锁：防止 item 创建触发的 layout → ScrollRect 回火 → 死循环
            if (_isProcessingScroll) return;
            _isProcessingScroll = true;

            try
            {
                _state.ScrollOffset = scrollOffset;

            // 计算可见范围
            VisibleRange newRange = _visibleManager.Calculate(scrollOffset, _state.ViewportSize);

            // 保存到 state
            _state.PrevFirstVisibleIndex = _state.FirstVisibleIndex;
            _state.PrevLastVisibleIndex = _state.LastVisibleIndex;
            _state.FirstVisibleIndex = newRange.First;
            _state.LastVisibleIndex = newRange.Last;

            // Diff
            DiffResult diff = _visibleManager.Diff();

            // 处理结果
            ProcessDiff(diff);
            }
            finally
            {
                _isProcessingScroll = false;
            }
        }

        /// <summary>
        /// 每帧 LateUpdate 调用（在 SVListView 中驱动）
        /// </summary>
        public void LateUpdate()
        {
            if (_state.Status != SVListStatus.Running) return;

            // 处理创建队列
            _instantiateScheduler.ProcessPending(_cachedCreateAction);

            // 处理回收队列（有条件触发）
            if (_recycleScheduler.ShouldRecycle(_itemContainer.Count))
            {
                _recycleScheduler.ProcessAll(_cachedRecycleAction);
            }

            // 更新 Content 尺寸
            UpdateContentSize();

            // 更新调试信息
            UpdateDebugInfo();
        }

        #endregion

        #region Diff 处理

        /// <summary>
        /// 处理 Diff 结果
        /// </summary>
        private void ProcessDiff(DiffResult diff)
        {
            if (_state.Status != SVListStatus.Running) return;

            // 紧急刷新模式：全部重建
            if (_visibleManager.State == VisibleState.Jumping)
            {
                ProcessEmergencyRefresh(diff);
                return;
            }

            // 处理删除
            if (diff.HasRemove)
            {
                _recycleScheduler.EnqueueRange(diff.RemoveStart, diff.RemoveEnd, _itemContainer);
            }

            // 处理新增
            if (diff.HasAdd)
            {
                for (int i = diff.AddStart; i <= diff.AddEnd; i++)
                {
                    int prefabID = GetPrefabID(i);
                    _instantiateScheduler.Enqueue(i, prefabID);
                }
            }

            // 立即处理回收（因为删除的元素已经不可见，可以立刻回收）
            if (diff.HasRemove)
            {
                _recycleScheduler.ProcessAll(_cachedRecycleAction);
            }
        }

        /// <summary>
        /// 紧急刷新：清空所有，重新创建
        /// </summary>
        private void ProcessEmergencyRefresh(DiffResult diff)
        {
            // 回收所有旧元素
            if (diff.HasRemove)
            {
                for (int i = diff.RemoveStart; i <= diff.RemoveEnd; i++)
                {
                    if (_itemContainer.TryGet(i, out ActiveItem item))
                    {
                        RecycleItem(item);
                    }
                }
            }

            // 创建所有新元素
            if (diff.HasAdd)
            {
                for (int i = diff.AddStart; i <= diff.AddEnd; i++)
                {
                    int prefabID = GetPrefabID(i);
                    CreateAndBindItem(i, prefabID);
                }
            }
        }

        #endregion

        #region 创建/回收处理

        /// <summary>
        /// 处理单个创建请求（由 Scheduler 驱动）
        /// </summary>
        private bool ProcessCreateRequest(CreateRequest request)
        {
            if (_itemContainer.Contains(request.Index))
            {
                // 已经存在，跳过
                return false;
            }

            CreateAndBindItem(request.Index, request.PrefabID);
            return true;
        }

        /// <summary>
        /// 创建并绑定一个 Item
        /// </summary>
        private void CreateAndBindItem(int index, int prefabID)
        {
            // 无对象池（纯逻辑/单测环境）：跳过实例化，仅维持数据层一致性
            if (_poolManager == null) return;

            // 从对象池获取，设置父节点为 Content
            ISVItemRenderer renderer = _poolManager.Get(prefabID, _contentTransform);
            if (renderer == null) return;

            MonoBehaviour mb = null;
            RectTransform rect = null;

            try
            {
                mb = renderer as MonoBehaviour;
                if (mb == null) return;

                rect = mb.transform as RectTransform;
                if (rect == null) return;

                // 确保父节点为 Content
                if (_contentTransform != null && rect.parent != _contentTransform)
                    rect.SetParent(_contentTransform, false);

                // 设置锚点、pivot 和尺寸
                // 使用 layout.GetSize() 而非直接 _heightCache.GetHeight()
                // Grid 模式下 layout 会正确映射 item index → 行高（动态行高）或固定 cellHeight
                float itemHeight = _layout.GetSize(index);
                Vector2 pos = _layout.GetPosition(index);

                bool isGrid = _config.Direction == SVDirection.Grid;

                if (isGrid)
                {
                    // Grid 模式：精确定位到 (x, y)，使用固定宽度
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.sizeDelta = new Vector2(_config.GridCellWidth, itemHeight);
                    rect.anchoredPosition = pos;
                }
                else
                {
                    // Vertical/Horizontal 模式：拉伸宽度/高度
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(0f, itemHeight);
                    rect.anchoredPosition = new Vector2(0f, pos.y);
                }

                // 激活
                mb.gameObject.SetActive(true);

                // 获取数据
                object data = null;
                var dataSource = GetDataSource();
                if (dataSource != null && index < dataSource.GetItemCount())
                {
                    data = dataSource.GetItemData(index);
                }

                // 绑定
                renderer.OnBind(data, index);

                // 加入容器（从池中分配 ActiveItem，避免 GC）
                ActiveItem activeItem = _itemContainer.Allocate();
                activeItem.SetActive(index, prefabID, rect, renderer, mb.gameObject);
                _itemContainer.Add(index, activeItem);

                // v1.2.0：通知自动测量系统采集模板布局指纹（无订阅者时零开销）
                OnItemInstantiated?.Invoke(renderer, prefabID);

                // 更新高度（动态高度模式）。
                // Provider 已给出精确高度的条目跳过 renderer 复核：
                // TMP GetPreferredValues 每次调用都有 GC 分配与 CPU 成本，滚动热路径应省掉
                if (_config.DynamicHeight && !IsHeightExactFromProvider(index))
                {
                    UpdateItemHeight(index, renderer);
                }
            }
            catch (System.Exception)
            {
                // 失败时归还渲染器到对象池，防止泄漏
                if (renderer != null)
                {
                    if (mb != null) mb.gameObject.SetActive(false);
                    _poolManager.Release(prefabID, renderer);
                }
                throw;
            }
        }

        /// <summary>
        /// 处理单个回收请求（由 Scheduler 驱动）
        /// </summary>
        private void ProcessRecycleRequest(RecycleRequest request)
        {
            if (request.Item == null) return;
            RecycleItem(request.Item);
        }

        /// <summary>
        /// 回收一个 Item
        /// </summary>
        private void RecycleItem(ActiveItem item)
        {
            if (item == null) return;
            if (item.State == ItemState.Pooled) return;

            // Unbind
            if (item.Renderer != null)
            {
                item.Renderer.OnUnbind();
            }

            // 从容器移除（保留 ActiveItem 引用用于归还池）
            _itemContainer.Remove(item.Index);

            // 归还渲染器到对象池
            if (item.Renderer != null)
            {
                _poolManager.Release(item.PrefabID, item.Renderer);
            }

            // 归还 ActiveItem 包装器到池（避免 GC）
            _itemContainer.ReturnToPool(item);
        }

        #endregion

        #region 高度管理

        /// <summary>
        /// 更新指定 Item 的高度
        /// </summary>
        public void UpdateItemHeight(int index, ISVItemRenderer renderer)
        {
            if (!_config.DynamicHeight) return;

            float preferredHeight = renderer.GetPreferredHeight();
            if (preferredHeight <= 0f) return;

            // 更新高度缓存
            float delta = _heightCache.UpdateHeight(index, preferredHeight);
            if (Mathf.Abs(delta) > _config.HeightChangeThreshold)
            {
                // ★ 更新该 Item 自身的 sizeDelta.y，否则视觉上高度不变
                if (_itemContainer.TryGet(index, out ActiveItem selfItem) && selfItem.Rect != null)
                {
                    selfItem.Rect.sizeDelta = new Vector2(selfItem.Rect.sizeDelta.x, preferredHeight);
                }

                // 调整锚点补偿（防止画面跳动）
                CompensateAnchor(index, delta);

                // 更新受影响 Item 的位置和高度
                UpdatePositionsFrom(index + 1);

                // 先更新 ContentSize 再做滚动补偿，保证 MaxScrollOffset 是最新值
                _state.Dirty = true;
                UpdateContentSize();

                // ★ 被修正的 Item 完全位于视口上方时，其高度变化会整体平移下方所有内容，
                //   同步平移滚动偏移消除"上方测量导致的画面跳动"（无 Provider 的估算模式兜底）
                float itemStart = _layout.GetItemStartOffset(index);
                if (_state.ScrollOffset > 0f && itemStart + preferredHeight <= _state.ScrollOffset + 0.5f)
                {
                    ApplyScrollOffset(_state.ScrollOffset + delta, true);
                }
            }
        }

        /// <summary>
        /// 锚点补偿：高度变化后保持视觉稳定
        /// </summary>
        private void CompensateAnchor(int changedIndex, float delta)
        {
            if (_state.AnchorIndex >= 0 && changedIndex < _state.AnchorIndex)
            {
                // 在锚点之前发生变化，需要补偿
                _state.AnchorOffset += delta;
            }
        }

        /// <summary>
        /// 该条高度是否已由 Provider 精确提供（是则无需 renderer 测量复核）
        /// </summary>
        private bool IsHeightExactFromProvider(int index)
        {
            if (_heightProvider == null) return false;
            var info = _heightCache.GetInfo(index);
            return info != null && info.IsMeasured;
        }

        /// <summary>
        /// 从指定索引开始更新所有活跃元素的位置
        /// </summary>
        private void UpdatePositionsFrom(int fromIndex)
        {
            foreach (var item in _itemContainer.GetActiveItems())
            {
                if (item.Index >= fromIndex)
                {
                    Vector2 pos = _layout.GetPosition(item.Index);
                    if (item.Rect != null)
                    {
                        item.Rect.anchoredPosition = pos;
                    }
                }
            }
        }

        /// <summary>
        /// 运行时更新布局参数（spacing/padding/cellSize 等）
        /// 更新后自动刷新所有活跃元素位置和 ContentSize，并触发可见区重新计算
        /// </summary>
        public void UpdateLayoutParameters()
        {
            if (_state.Status != SVListStatus.Running) return;

            _layout.UpdateParameters(_config);

            // 更新所有活跃元素的位置和尺寸
            bool isGrid = _config.Direction == SVDirection.Grid;
            foreach (var item in _itemContainer.GetActiveItems())
            {
                if (item.Rect == null) continue;

                Vector2 pos = _layout.GetPosition(item.Index);
                float itemHeight = _layout.GetSize(item.Index);
                item.Rect.anchoredPosition = pos;

                if (isGrid)
                {
                    item.Rect.sizeDelta = new Vector2(_config.GridCellWidth, itemHeight);
                }
                else
                {
                    // 非 Grid 模式：高度可能已变化，更新 sizeDelta
                    item.Rect.sizeDelta = new Vector2(item.Rect.sizeDelta.x, itemHeight);
                }
            }

            // 先更新 ContentSize，再触发可见区重算（顺序很重要）
            _state.Dirty = true;
            UpdateContentSize();

            // 触发可见区重新计算：间距变化后可能有 item 移出/移入视野
            OnScrollChanged(_state.ScrollOffset);
        }

        /// <summary>
        /// 更新 Content 尺寸
        /// </summary>
        private void UpdateContentSize()
        {
            if (!_state.Dirty) return;

            float contentSize = _layout.GetContentSize();
            _state.ContentSize = contentSize;
            OnContentSizeChanged?.Invoke(contentSize);
            _state.Dirty = false;
        }

        #endregion

        #region 公共 API

        /// <summary>
        /// 设置 Viewport 尺寸
        /// </summary>
        public void SetViewportSize(float size)
        {
            _state.ViewportSize = size;
        }

        /// <summary>
        /// 刷新整个列表
        /// </summary>
        public void Refresh()
        {
            if (_state.Status != SVListStatus.Running) return;

            _state.Status = SVListStatus.Refreshing;

            // 回收所有活跃元素（复用缓存列表，避免 GC）
            _tempItemList.Clear();
            foreach (var item in _itemContainer.GetActiveItems())
                _tempItemList.Add(item);
            foreach (var item in _tempItemList)
                RecycleItem(item);

            _itemContainer.Clear();
            _instantiateScheduler.Clear();
            _recycleScheduler.Clear();

            // 重新初始化高度缓存
            var dataSource = GetDataSource();
            int totalCount = dataSource?.GetItemCount() ?? 0;
            _state.TotalCount = totalCount;

            // Grid 动态行高需要按行数初始化
            if (_config.Direction == SVDirection.Grid && _config.GridCellHeight <= 0f)
            {
                int rowCount = (totalCount + _config.GridColumnCount - 1) / Mathf.Max(1, _config.GridColumnCount);
                _heightCache.Initialize(rowCount);
            }
            else
            {
                _heightCache.Initialize(totalCount,
                    _cachedHeightGetter);
            }

            // 重新创建布局
            CreateLayout();

            // 重置可见区
            _visibleManager.Reset();
            _state.Reset();
            _state.TotalCount = totalCount;

            _state.Status = SVListStatus.Running;
            _state.Dirty = true;

            // 触发一次滚动计算
            OnScrollChanged(0f);
        }

        /// <summary>
        /// 刷新单个 Item
        /// </summary>
        public void RefreshItem(int index)
        {
            if (_itemContainer.TryGet(index, out ActiveItem item) && item.Renderer != null)
            {
                var dataSource = GetDataSource();
                if (dataSource != null && index < dataSource.GetItemCount())
                {
                    object data = dataSource.GetItemData(index);
                    item.Renderer.OnBind(data, index);

                    if (_config.DynamicHeight && !IsHeightExactFromProvider(index))
                    {
                        UpdateItemHeight(index, item.Renderer);
                    }
                }
            }
        }

        /// <summary>
        /// 插入数据（单条，等价于 InsertItems(index, 1)）
        /// </summary>
        public void InsertItem(int index)
        {
            InsertItems(index, 1);
        }

        /// <summary>
        /// 批量插入数据（调用前数据源需已完成插入）
        ///
        /// 增量更新高度缓存与活跃元素索引，不做全量重建、不丢已测量高度。
        /// 插入点位于视口上方（含顶部加载历史）时自动平移滚动位置，画面保持不动。
        /// </summary>
        public void InsertItems(int index, int count)
        {
            if (_state.Status != SVListStatus.Running || count <= 0) return;
            if (index < 0 || index > _state.TotalCount) return;

            // Grid 不支持增量（改动点可能落在行中间）：退化为全量刷新
            if (_config.Direction == SVDirection.Grid)
            {
                _state.TotalCount += count;
                Refresh();
                return;
            }

            // 1. 高度缓存批量插入（有 Provider 时直接写入精确高度，用缓存委托避免 GC）
            float itemsSize = _heightCache.InsertRange(index, count, _cachedHeightGetter);
            float shift = _layout.GetInsertShift(count, itemsSize);

            _state.TotalCount += count;

            // 2. 已有活跃元素索引平移
            _itemContainer.ShiftIndices(index, count);

            // 3. 可见范围记录同步平移（_prevOffset 一并平移，避免补偿被误判为滚动速度尖峰）
            _visibleManager.ShiftIndices(index, count, shift);
            ShiftStateVisibleIndices(index, count);

            // 4. 平移改动点之后活跃元素的位置
            UpdatePositionsFrom(index);

            // 5. 更新 ContentSize 并下发给 ScrollRect（补偿依赖最新的 MaxScrollOffset）
            _state.Dirty = true;
            UpdateContentSize();

            // 6. 视口补偿：插入点在视口顶部或上方 → 滚动偏移平移同等距离，画面不动。
            //    优先走增量平移通道（保住用户惯性），无 ScrollRect 环境回落绝对定位
            float insertStart = _layout.GetItemStartOffset(index);
            if (insertStart <= _state.ScrollOffset + 0.5f)
            {
                CompensateByShift(shift);
            }
            else
            {
                OnScrollChanged(_state.ScrollOffset);
            }

            // 7. 补洞：Diff 只比较区间边界，插入点落在视口内部时中间缺的索引由这里补齐
            FillRangeHoles(CurrentVisibleRange);
        }

        /// <summary>
        /// 删除数据（单条，等价于 RemoveItems(index, 1)）
        /// </summary>
        public void RemoveItem(int index)
        {
            RemoveItems(index, 1);
        }

        /// <summary>
        /// 批量删除数据（调用前数据源需已完成删除）
        ///
        /// 增量更新；删除点位于视口上方时自动回退滚动偏移，画面保持不动。
        /// </summary>
        public void RemoveItems(int index, int count)
        {
            if (_state.Status != SVListStatus.Running || count <= 0) return;
            if (index < 0 || index >= _state.TotalCount) return;
            if (count > _state.TotalCount - index) count = _state.TotalCount - index;

            // Grid 不支持增量：退化为全量刷新
            if (_config.Direction == SVDirection.Grid)
            {
                _state.TotalCount -= count;
                Refresh();
                return;
            }

            // 1. 回收被删区间的活跃元素（此时索引还有效）
            for (int i = index + count - 1; i >= index; i--)
            {
                if (_itemContainer.TryGet(i, out ActiveItem removed))
                {
                    RecycleItem(removed);
                }
            }

            // 2. 高度缓存批量删除，返回被删高度供补偿
            float removedSize = _heightCache.RemoveRange(index, count);
            float shift = _layout.GetInsertShift(count, removedSize);

            _state.TotalCount -= count;

            // 3. 后续元素索引左移
            _itemContainer.ShiftIndices(index + count, -count);

            // 4. 可见范围记录同步平移
            _visibleManager.ShiftIndices(index, -count, -shift);
            ShiftStateVisibleIndices(index, -count);

            // 5. 平移剩余活跃元素位置
            UpdatePositionsFrom(index);

            // 6. 更新 ContentSize
            _state.Dirty = true;
            UpdateContentSize();

            // 7. 视口补偿：删除点在视口上方 → 滚动偏移回退同等距离（同样走增量通道保惯性）
            float removeStart = _layout.GetItemStartOffset(index);
            if (removeStart < _state.ScrollOffset - 0.5f)
            {
                CompensateByShift(-shift);
            }
            else
            {
                OnScrollChanged(_state.ScrollOffset);
            }

            FillRangeHoles(CurrentVisibleRange);
        }

        /// <summary>最大可滚动偏移（ContentSize - ViewportSize）</summary>
        public float MaxScrollOffset => Mathf.Max(0f, _state.ContentSize - _state.ViewportSize);

        /// <summary>是否停在底部（threshold 为距底部像素阈值，聊天用它决定新消息是否自动跟滚）</summary>
        public bool IsAtBottom(float threshold = 16f)
        {
            return _state.ScrollOffset >= MaxScrollOffset - threshold;
        }

        /// <summary>滚动到指定偏移（立即）</summary>
        public void ScrollToOffset(float offset)
        {
            if (_state.Status != SVListStatus.Running) return;
            ApplyScrollOffset(offset, true);
        }

        /// <summary>滚动到底部（立即，不播动画）</summary>
        public void ScrollToBottom()
        {
            ScrollToOffset(MaxScrollOffset);
        }

        /// <summary>
        /// 应用滚动偏移：先推送给 ScrollRect（由 onValueChanged 回流），
        /// 再同步刷新一次内部可见区——ScrollRect 的回调可能推迟到 LateUpdate，双保险。
        /// OnScrollChanged 幂等，重复计算无副作用。
        /// </summary>
        private void ApplyScrollOffset(float newOffset, bool notifyScrollRect)
        {
            newOffset = Mathf.Clamp(newOffset, 0f, MaxScrollOffset);

            if (notifyScrollRect && OnScrollPositionChanged != null)
            {
                OnScrollPositionChanged.Invoke(newOffset);
            }

            OnScrollChanged(newOffset);
        }

        /// <summary>
        /// 插入/删除的视口补偿（增量通道）：
        /// 有 ScrollRect 时走 content 增量平移（不清惯性速度），否则回落绝对定位。
        /// </summary>
        private void CompensateByShift(float shift)
        {
            float target = Mathf.Clamp(_state.ScrollOffset + shift, 0f, MaxScrollOffset);
            float actual = target - _state.ScrollOffset;
            if (Mathf.Abs(actual) < 0.01f) return;

            _state.ScrollOffset = target;

            if (OnScrollOffsetShift != null)
            {
                // 回流链：ShiftScrollOffset → OnScrollOffsetChanged → OnScrollChanged（幂等）
                OnScrollOffsetShift.Invoke(actual);
            }

            OnScrollChanged(_state.ScrollOffset);
        }

        /// <summary>
        /// 数据增删后平移 State 中记录的可见索引，保持与实际一致
        /// </summary>
        private void ShiftStateVisibleIndices(int fromIndex, int delta)
        {
            _state.PrevFirstVisibleIndex = ShiftIndex(_state.PrevFirstVisibleIndex, fromIndex, delta);
            _state.PrevLastVisibleIndex = ShiftIndex(_state.PrevLastVisibleIndex, fromIndex, delta);
            _state.FirstVisibleIndex = ShiftIndex(_state.FirstVisibleIndex, fromIndex, delta);
            _state.LastVisibleIndex = ShiftIndex(_state.LastVisibleIndex, fromIndex, delta);
        }

        private static int ShiftIndex(int index, int fromIndex, int delta)
        {
            return index >= fromIndex ? index + delta : index;
        }

        /// <summary>
        /// 确保可见区间内每个索引都有活跃元素。
        /// Diff 只比较区间边界，插入点落在视口内部时中间会缺索引，由这里补齐创建。
        /// </summary>
        private void FillRangeHoles(VisibleRange range)
        {
            if (range.First < 0) return;
            for (int i = range.First; i <= range.Last; i++)
            {
                if (!_itemContainer.Contains(i))
                {
                    _instantiateScheduler.Enqueue(i, GetPrefabID(i));
                }
            }
        }

        /// <summary>
        /// 获取指定索引当前活跃的 Renderer（该条不可见时返回 null）
        /// </summary>
        public ISVItemRenderer GetRenderer(int index)
        {
            return _itemContainer != null && _itemContainer.TryGet(index, out ActiveItem item)
                ? item.Renderer
                : null;
        }

        /// <summary>
        /// 跳转到指定索引
        /// </summary>
        public void JumpToIndex(int index, SVJumpMode mode = SVJumpMode.Top)
        {
            if (_state.Status != SVListStatus.Running) return;
            if (index < 0 || index >= _state.TotalCount) return;

            float targetOffset = _heightCache.GetOffset(index);

            switch (mode)
            {
                case SVJumpMode.Top:
                    // 已是目标 offset
                    break;

                case SVJumpMode.Center:
                    targetOffset -= _state.ViewportSize * 0.5f;
                    targetOffset += _heightCache.GetHeight(index) * 0.5f;
                    break;

                case SVJumpMode.Bottom:
                    targetOffset -= _state.ViewportSize - _heightCache.GetHeight(index);
                    break;
            }

            targetOffset = Mathf.Max(0f, targetOffset);

            _visibleManager.MarkJumping();
            OnScrollPositionChanged?.Invoke(targetOffset);
        }

        /// <summary>
        /// 设置锚点（用于高度变化时保持视觉稳定）
        /// </summary>
        public void SetAnchor(int index, float offset)
        {
            _state.AnchorIndex = index;
            _state.AnchorOffset = offset;
        }

        /// <summary>
        /// 获取 PrefabID（通过外部注册的选择器）
        /// </summary>
        public System.Func<int, int> PrefabSelector { get; set; }

        private int GetPrefabID(int index)
        {
            return PrefabSelector?.Invoke(index) ?? 0;
        }

        /// <summary>
        /// 获取数据源（通过外部注册）
        /// </summary>
        public Func<ISVDataSource> DataSourceProvider { get; set; }

        /// <summary>
        /// 高度预计算提供者（可选）。
        /// 提供后 Item 未实例化即知精确高度：初始化/插入无估算误差，
        /// 头部插入历史数据的视口补偿可做到零跳动。
        /// 若数据源自身实现 ISVItemHeightProvider，Initialize 时会自动识别。
        /// </summary>
        public ISVItemHeightProvider HeightProvider
        {
            get => _heightProvider;
            set
            {
                _heightProvider = value;
                // 缓存委托：方法组转换每次求值都会分配新委托，插入高频时是稳定 GC 源
                _cachedHeightGetter = value != null ? value.GetItemHeight : null;
            }
        }

        private ISVItemHeightProvider _heightProvider;
        private Func<int, float> _cachedHeightGetter;

        private ISVDataSource GetDataSource()
        {
            return DataSourceProvider?.Invoke();
        }

        #endregion

        #region 调试

        private void UpdateDebugInfo()
        {
            if (DebugInfo == null) return;

            DebugInfo.ActiveCount = _itemContainer.Count;
            DebugInfo.PoolCount = _poolManager?.GetTotalPoolCount() ?? 0;
            DebugInfo.CreateQueueCount = _instantiateScheduler?.QueueCount ?? 0;
            DebugInfo.RecycleQueueCount = _recycleScheduler?.QueueCount ?? 0;
            DebugInfo.TotalCount = _state.TotalCount;
            DebugInfo.FirstVisibleIndex = _state.FirstVisibleIndex;
            DebugInfo.LastVisibleIndex = _state.LastVisibleIndex;
            DebugInfo.ContentSize = _state.ContentSize;
            DebugInfo.ScrollOffset = _state.ScrollOffset;

            OnDebugInfoUpdated?.Invoke(DebugInfo);
        }

        #endregion

        #region 销毁

        /// <summary>
        /// 销毁核心
        /// </summary>
        public void Dispose()
        {
            _state.Status = SVListStatus.Disposed;

            // 回收所有活跃元素（复用缓存列表，避免 GC）
            _tempItemList.Clear();
            foreach (var item in _itemContainer.GetActiveItems())
                _tempItemList.Add(item);
            foreach (var item in _tempItemList)
                RecycleItem(item);

            _itemContainer?.Clear();
            _instantiateScheduler?.Clear();
            _recycleScheduler?.Clear();
            _heightCache?.Dispose();
            _poolManager?.ClearAll();

            _state.Reset();
            _state.Status = SVListStatus.Disposed;
        }

        #endregion
    }

    /// <summary>
    /// JumpTo 模式
    /// </summary>
    public enum SVJumpMode
    {
        /// <summary>顶部对齐</summary>
        Top,

        /// <summary>居中</summary>
        Center,

        /// <summary>底部对齐</summary>
        Bottom
    }
}

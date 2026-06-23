using UnityEngine;
using UnityEditor;

namespace SVList.Editor
{
    /// <summary>
    /// SVListView 自定义 Inspector
    ///
    /// 提供：
    /// - 配置参数可视化编辑
    /// - 运行时状态实时显示（VisibleCount, PoolCount, FPS, Memory）
    /// - 快捷操作按钮（Refresh, ClearPool, Jump）
    /// </summary>
    [CustomEditor(typeof(SVListView))]
    public class SVListViewEditor : UnityEditor.Editor
    {
        private SVListView _target;
        private SerializedProperty _scrollRectProp;
        private SerializedProperty _viewportProp;
        private SerializedProperty _contentProp;
        private SerializedProperty _itemPrefabsProp;
        private SerializedProperty _configProp;

        private bool _showRuntimeInfo = true;
        private bool _showConfig = true;
        private bool _showReferences = true;
        private bool _showPrefabs = true;

        private int _jumpToIndex = 0;

        private void OnEnable()
        {
            _target = (SVListView)target;

            _scrollRectProp = serializedObject.FindProperty("_scrollRect");
            _viewportProp = serializedObject.FindProperty("_viewport");
            _contentProp = serializedObject.FindProperty("_content");
            _itemPrefabsProp = serializedObject.FindProperty("_itemPrefabs");
            _configProp = serializedObject.FindProperty("_config");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 标题
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SVList - Virtual Scroll View", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("高性能 UGUI 虚拟列表框架", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // 运行时信息
            DrawRuntimeInfo();

            // References
            _showReferences = EditorGUILayout.Foldout(_showReferences, "References", true);
            if (_showReferences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_scrollRectProp);
                EditorGUILayout.PropertyField(_viewportProp);
                EditorGUILayout.PropertyField(_contentProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Prefabs
            _showPrefabs = EditorGUILayout.Foldout(_showPrefabs, "Item Prefabs", true);
            if (_showPrefabs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_itemPrefabsProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 配置
            _showConfig = EditorGUILayout.Foldout(_showConfig, "Config", true);
            if (_showConfig)
            {
                EditorGUI.indentLevel++;
                DrawConfig();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 快捷按钮
            DrawQuickActions();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRuntimeInfo()
        {
            if (!Application.isPlaying) return;

            _showRuntimeInfo = EditorGUILayout.Foldout(_showRuntimeInfo, "Runtime Info", true);
            if (!_showRuntimeInfo) return;

            var debugInfo = _target.DebugInfo;
            if (debugInfo == null) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"FPS: {debugInfo.FPS:F1}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Items: {debugInfo.TotalCount}");
            EditorGUILayout.LabelField($"Active Items: {debugInfo.ActiveCount}");
            EditorGUILayout.LabelField($"Pooled Items: {debugInfo.PoolCount}");
            EditorGUILayout.LabelField($"Create Queue: {debugInfo.CreateQueueCount}");
            EditorGUILayout.LabelField($"Recycle Queue: {debugInfo.RecycleQueueCount}");
            EditorGUILayout.LabelField($"Visible Range: [{debugInfo.FirstVisibleIndex}..{debugInfo.LastVisibleIndex}]");
            EditorGUILayout.LabelField($"Scroll Offset: {debugInfo.ScrollOffset:F1}");
            EditorGUILayout.LabelField($"Content Size: {debugInfo.ContentSize:F1}");
            EditorGUILayout.LabelField($"Memory: {FormatBytes(debugInfo.Memory)}");

            // 进度条：滚动位置
            if (debugInfo.ContentSize > 0f)
            {
                float progress = debugInfo.ScrollOffset / debugInfo.ContentSize;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scroll Progress:");
                Rect progressRect = GUILayoutUtility.GetRect(18, 18, "TextField");
                EditorGUI.ProgressBar(progressRect, progress, $"{progress * 100:F1}%");
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;

            // 强制刷新 Inspector
            Repaint();
        }

        private void DrawConfig()
        {
            if (_configProp == null) return;

            SerializedProperty spacingProp = _configProp.FindPropertyRelative("Spacing");
            SerializedProperty paddingTopProp = _configProp.FindPropertyRelative("PaddingTop");
            SerializedProperty paddingBottomProp = _configProp.FindPropertyRelative("PaddingBottom");
            SerializedProperty paddingLeftProp = _configProp.FindPropertyRelative("PaddingLeft");
            SerializedProperty paddingRightProp = _configProp.FindPropertyRelative("PaddingRight");
            SerializedProperty defaultItemSizeProp = _configProp.FindPropertyRelative("DefaultItemSize");
            SerializedProperty estimateHeightProp = _configProp.FindPropertyRelative("EstimateHeight");
            SerializedProperty dynamicHeightProp = _configProp.FindPropertyRelative("DynamicHeight");
            SerializedProperty heightChangeThresholdProp = _configProp.FindPropertyRelative("HeightChangeThreshold");
            SerializedProperty preloadFactorProp = _configProp.FindPropertyRelative("PreloadFactor");
            SerializedProperty maxPoolSizeProp = _configProp.FindPropertyRelative("MaxPoolSize");
            SerializedProperty maxCreatePerFrameProp = _configProp.FindPropertyRelative("MaxInstantiatePerFrame");
            SerializedProperty preWarmCountProp = _configProp.FindPropertyRelative("PreWarmCount");
            SerializedProperty directionProp = _configProp.FindPropertyRelative("Direction");
            SerializedProperty reverseProp = _configProp.FindPropertyRelative("Reverse");
            SerializedProperty gridColumnCountProp = _configProp.FindPropertyRelative("GridColumnCount");
            SerializedProperty gridCellWidthProp = _configProp.FindPropertyRelative("GridCellWidth");
            SerializedProperty gridCellHeightProp = _configProp.FindPropertyRelative("GridCellHeight");
            SerializedProperty gridHSpacingProp = _configProp.FindPropertyRelative("GridHorizontalSpacing");
            SerializedProperty gridVSpacingProp = _configProp.FindPropertyRelative("GridVerticalSpacing");
            SerializedProperty showDebugPanelProp = _configProp.FindPropertyRelative("ShowDebugPanel");
            SerializedProperty showGizmosProp = _configProp.FindPropertyRelative("ShowGizmos");
            SerializedProperty showDetailedGizmosProp = _configProp.FindPropertyRelative("ShowDetailedGizmos");

            // Layout
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(directionProp);

            bool isGrid = directionProp.enumValueIndex == (int)SVDirection.Grid;
            bool isVertical = directionProp.enumValueIndex == (int)SVDirection.Vertical;
            bool isHorizontal = directionProp.enumValueIndex == (int)SVDirection.Horizontal;

            // 通用间距参数
            if (isVertical || isHorizontal)
            {
                EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing"));
            }

            // 内边距
            if (isGrid)
            {
                EditorGUILayout.PropertyField(paddingTopProp, new GUIContent("Padding Top"));
                EditorGUILayout.PropertyField(paddingBottomProp, new GUIContent("Padding Bottom"));
                EditorGUILayout.PropertyField(paddingLeftProp, new GUIContent("Padding Left"));
                EditorGUILayout.PropertyField(paddingRightProp, new GUIContent("Padding Right"));
            }
            else
            {
                EditorGUILayout.PropertyField(paddingTopProp, new GUIContent(isVertical ? "Padding Top" : "Padding Left"));
                EditorGUILayout.PropertyField(paddingBottomProp, new GUIContent(isVertical ? "Padding Bottom" : "Padding Right"));
            }

            // DefaultItemSize（非 Grid 模式）
            if (!isGrid)
            {
                EditorGUILayout.PropertyField(defaultItemSizeProp, new GUIContent(isVertical ? "Default Width" : "Default Height"));
            }

            // Dynamic Height
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dynamic Height", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dynamicHeightProp);
            EditorGUILayout.PropertyField(estimateHeightProp);
            if (isGrid)
            {
                EditorGUILayout.PropertyField(heightChangeThresholdProp);
            }

            // Performance
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(preloadFactorProp);
            EditorGUILayout.PropertyField(maxPoolSizeProp);
            EditorGUILayout.PropertyField(maxCreatePerFrameProp);
            EditorGUILayout.PropertyField(preWarmCountProp);

            // Direction
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(reverseProp);

            // Grid
            if (isGrid)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(gridColumnCountProp, new GUIContent("Column Count"));
                EditorGUILayout.PropertyField(gridCellWidthProp, new GUIContent("Cell Width"));
                EditorGUILayout.PropertyField(gridCellHeightProp, new GUIContent("Cell Height", "0 = 使用动态行高（每行列等高）"));
                EditorGUILayout.PropertyField(gridHSpacingProp, new GUIContent("Horizontal Spacing"));
                EditorGUILayout.PropertyField(gridVSpacingProp, new GUIContent("Vertical Spacing"));
            }

            // Debug
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showGizmosProp);
            if (showGizmosProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(showDetailedGizmosProp,
                    new GUIContent("Detailed Gizmos", "显示活跃元素矩形、网格线、高度状态等详细信息"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(showDebugPanelProp);
        }

        private void DrawQuickActions()
        {
            bool canOperate = Application.isPlaying && _target != null && _target.Core?.IsInitialized == true;

            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = canOperate;
            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                _target.Refresh();
                Debug.Log($"[SVList] Refresh() called — {_target.DebugInfo?.ActiveCount ?? 0} active items");
                Repaint();
            }

            if (GUILayout.Button("Clear Pool", GUILayout.Height(30)))
            {
                _target.ClearPool();
                Debug.Log($"[SVList] ClearPool() called");
                Repaint();
            }

            // Log Info 在运行时可点击，无 Core 时也能查看基础信息
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Log Info", GUILayout.Height(30)))
            {
                var info = _target.DebugInfo;
                if (info != null)
                {
                    Debug.Log($"[SVList] Active:{info.ActiveCount} Pool:{info.PoolCount} " +
                              $"Visible:[{info.FirstVisibleIndex}..{info.LastVisibleIndex}] " +
                              $"Total:{info.TotalCount} FPS:{info.FPS:F1} " +
                              $"Scroll:{info.ScrollOffset:F0}/{info.ContentSize:F0}");
                }
                else
                {
                    Debug.Log($"[SVList] Core not initialized yet. " +
                              $"isPlaying={Application.isPlaying} viewport={_target.name}");
                }
                Repaint();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // JumpTo
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JumpTo Index:", GUILayout.Width(80));
            _jumpToIndex = EditorGUILayout.IntField(_jumpToIndex, GUILayout.Width(80));

            GUI.enabled = canOperate;
            if (GUILayout.Button("Jump", GUILayout.Width(60)))
            {
                _target.JumpToIndex(_jumpToIndex);
                Debug.Log($"[SVList] JumpToIndex({_jumpToIndex}) called");
                Repaint();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }
    }
}

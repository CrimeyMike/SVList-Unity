using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace SVList.EditorTools
{
    /// <summary>
    /// SVList 设计时预览窗口(v1.3.0 一期):Window → SVList → Item Preview。
    ///
    /// 形态(交互原型 v3 已确认):窗口=纯画布,参数在 Inspector;
    /// 一期实现把 Inspector 以"内嵌原生 Inspector"的方式放在窗口右栏(与用户自定义 Editor 兼容),
    /// 测试数据为窗口本地配置(EditorPrefs 持久,不进 prefab/不产生资产文件)。
    ///
    /// 联动:选中带 Item 组件的物体/Project 选中 prefab/Prefab Stage 打开时自动跟随;
    /// Stage 内未保存的拖动经节流轮询半实时刷新;prefab 保存与脚本编译后自动重建。
    /// </summary>
    public sealed class SVItemPreviewWindow : EditorWindow
    {
        private SVItemPreviewScene _scene;

        // 目标
        private GameObject _targetPrefab;     // 磁盘资产或 Stage 内容根
        private bool _locked;                 // 锁定当前目标不跟随选择
        private string _targetName = "(未选择)";

        // 数据
        private readonly List<object> _datas = new List<object>();
        private Type _modelType;
        private FieldInfo[] _fields;
        private string _persistKey;

        // 选项
        private float _panelWidth = 720f;
        private bool _showBounds = true;
        private bool _showHeights = true;

        // 画布视图(像 Scene 视图):滚轮缩放 + 拖拽平移,1 = 适配画布
        private float _canvasZoom = 1f;
        private Vector2 _canvasPan = Vector2.zero;

        // 内嵌 Inspector(注意:本命名空间下裸 Editor 会解析到 SVList.Editor 命名空间,必须全称)
        private UnityEditor.Editor _cachedInspector;
        private UObject _cachedInspectorTarget;

        // 刷新节流
        private double _nextPoll;
        private string _lastStageHash;

        [MenuItem("Window/SVList/Item Preview")]
        public static void Open() => GetWindow<SVItemPreviewWindow>("SVList Preview");

        private void OnEnable()
        {
            PrefabStage.prefabSaved += OnPrefabSaved;
            PrefabStage.prefabStageClosing += OnStageClosing;
            AssemblyReloadEvents.afterAssemblyReload += OnHotReload;
            Selection.selectionChanged += Repaint;
            // 轮询挂编辑器主循环而非 OnGUI:docked 窗口只有收到鼠标/GUI 事件才跑 OnGUI,
            // 用户在 Scene 里编辑时预览窗口收不到事件,挂在 OnGUI 末尾的轮询会饿死(预览冻结在旧帧)。
            EditorApplication.update += PollStage;
            _scene = new SVItemPreviewScene();
            AutoTargetFromSelection();
        }

        private void OnDisable()
        {
            PrefabStage.prefabSaved -= OnPrefabSaved;
            PrefabStage.prefabStageClosing -= OnStageClosing;
            AssemblyReloadEvents.afterAssemblyReload -= OnHotReload;
            Selection.selectionChanged -= Repaint;
            EditorApplication.update -= PollStage;
            _scene?.Dispose();
            _scene = null;
        }

        private void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCanvas(GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
                DrawSidePanel(320f);
            }
        }

        // ---------------- 目标与刷新 ----------------

        private void AutoTargetFromSelection()
        {
            if (_locked) return;

            // 1) Prefab Stage 优先(编辑中的内容根,含未保存修改)
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null && HasRenderer(stage.prefabContentsRoot))
            {
                SetTarget(stage.prefabContentsRoot, stage.prefabContentsRoot.name + " (Stage)");
                return;
            }

            // 2) 选中的 GameObject(取其 prefab 实例根)
            var go = Selection.activeGameObject;
            if (go != null)
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                GameObject asset = null;
                if (root != null)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(root) as GameObject;
                    if (src != null && HasRenderer(src)) asset = src;
                }
                if (asset == null && HasRenderer(go)) asset = go;
                if (asset != null) { SetTarget(asset, asset.name); return; }
            }

            // 3) Project 选中 prefab
            var sel = Selection.activeObject as GameObject;
            if (sel != null && PrefabUtility.IsPartOfPrefabAsset(sel) && HasRenderer(sel))
                SetTarget(sel, sel.name);
        }

        private static bool HasRenderer(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is ISVItemRenderer) return true;
            return false;
        }

        private void SetTarget(GameObject prefab, string label)
        {
            if (prefab == _targetPrefab) return;
            _targetPrefab = prefab;
            _targetName = label;
            ResetCanvasView();   // 换目标内容完全不同,视图回适配;同目标的 Rebuild 保持视角
            RebuildDataModel();
            Rebuild();
        }

        private void RebuildDataModel()
        {
            _modelType = null; _fields = null;
            if (_targetPrefab == null) { _datas.Clear(); return; }

            Type t = null;
            foreach (var mb in _targetPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(mb is ISVItemRenderer)) continue;
                var bt = mb.GetType().BaseType;
                while (bt != null)
                {
                    if (bt.IsGenericType && bt.GetGenericTypeDefinition() == typeof(SVItemBase<>))
                    { t = bt.GetGenericArguments()[0]; break; }
                    bt = bt.BaseType;
                }
                if (t != null) break;
            }
            if (t == null) return;

            if (_modelType == t) return;
            _modelType = t;
            var fl = new List<FieldInfo>();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var ft = f.FieldType;
                if (ft == typeof(string) || ft == typeof(bool) || ft == typeof(int) || ft == typeof(float) ||
                    ft == typeof(double) || ft.IsEnum || ft == typeof(Sprite) || ft == typeof(Color))
                    fl.Add(f);
            }
            _fields = fl.ToArray();

            // 恢复本地保存的测试数据;没有则给两组默认
            _persistKey = "SVList.Preview.Data." + t.FullName;
            _datas.Clear();
            var json = EditorPrefs.GetString(_persistKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var box = JsonUtility.FromJson<DatumBox>(json);
                    foreach (var j in box.items)
                    {
                        var inst = Activator.CreateInstance(t);
                        EditorJsonUtility.FromJsonOverwrite(j, inst);
                        _datas.Add(inst);
                    }
                }
                catch { _datas.Clear(); }
            }
            if (_datas.Count == 0)
            {
                _datas.Add(Activator.CreateInstance(t));
                _datas.Add(Activator.CreateInstance(t));
            }
        }

        private void SaveDatas()
        {
            if (_modelType == null) return;
            var box = new DatumBox();
            foreach (var d in _datas) box.items.Add(EditorJsonUtility.ToJson(d));
            EditorPrefs.SetString(_persistKey, JsonUtility.ToJson(box));
        }

        [Serializable] private class DatumBox { public List<string> items = new List<string>(); }

        private void Rebuild()
        {
            if (_scene == null || _targetPrefab == null) { Repaint(); return; }

            // 重建会销毁全部预览实例;先释放引用它们的所有缓存 Editor,
            // 否则 Odin 等接管自定义 Inspector 的插件会在 OnGUI 时对已销毁目标抛异常
            _cachedInspector = null;
            _cachedInspectorTarget = null;

            _scene.Build(_targetPrefab, _panelWidth, _datas);
            CacheInspector();
            Repaint();
        }

        private void OnPrefabSaved(GameObject _) { if (!_locked) { AutoTargetFromSelection(); } Rebuild(); }
        private void OnHotReload() { RebuildDataModel(); Rebuild(); }

        private void OnStageClosing(PrefabStage stage)
        {
            // Stage 关闭会销毁内容根;若当前目标正是它,退回该 prefab 资产继续预览(否则目标变已销毁引用,预览冻结)
            if (_targetPrefab == stage.prefabContentsRoot)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
                if (asset != null) SetTarget(asset, asset.name);
            }
        }

        /// <summary>Stage 未保存拖动的半实时跟随:节流轮询内容根的结构哈希。
        /// 由 EditorApplication.update 驱动(OnEnable 订阅),不依赖窗口收到 GUI 事件。</summary>
        private void PollStage()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.35;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage?.prefabContentsRoot == null) { _lastStageHash = null; return; }
            if (_locked) return;

            var hash = HashTransform(stage.prefabContentsRoot.transform);
            if (hash != _lastStageHash)
            {
                _lastStageHash = hash;
                _targetPrefab = stage.prefabContentsRoot;
                _targetName = stage.prefabContentsRoot.name + " (Stage)";
                RebuildDataModel();
                Rebuild();
            }
        }

        private static string HashTransform(Transform t)
        {
            var sb = new System.Text.StringBuilder(64);
            Append(t);
            void Append(Transform tr)
            {
                var rt = tr as RectTransform;
                sb.Append(tr.name).Append(':');
                if (rt != null)
                    sb.Append(rt.anchorMin).Append(rt.anchorMax).Append(rt.anchoredPosition)
                      .Append(rt.sizeDelta).Append(rt.pivot).Append('|');
                foreach (Transform c in tr) Append(c);
            }
            return sb.ToString();
        }

        // ---------------- UI ----------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("目标: " + _targetName, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                EditorGUI.BeginChangeCheck();
                _locked = GUILayout.Toggle(_locked, "锁定", EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck() && !_locked) AutoTargetFromSelection();

                if (GUILayout.Button("跟随选择", EditorStyles.toolbarButton)) { _locked = false; AutoTargetFromSelection(); }
                if (GUILayout.Button("重建", EditorStyles.toolbarButton)) Rebuild();
                if (GUILayout.Button("复位视图", EditorStyles.toolbarButton)) ResetCanvasView();
            }
        }

        private void ResetCanvasView()
        {
            _canvasZoom = 1f;
            _canvasPan = Vector2.zero;
            Repaint();
        }

        /// <summary>画布交互:滚轮以鼠标为锚缩放、右键/中键拖拽平移、F 复位(与 Scene 视图习惯一致)。</summary>
        private void HandleCanvasEvents(Rect rect)
        {
            var e = Event.current;

            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                var mouse = e.mousePosition - rect.center;   // 相对画布中心
                float old = _canvasZoom;
                _canvasZoom = Mathf.Clamp(_canvasZoom * Mathf.Pow(0.9f, e.delta.y), 0.15f, 10f);
                // 锚点修正:保持鼠标下的内容点不动
                _canvasPan = mouse - (mouse - _canvasPan) * (_canvasZoom / old);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2) && rect.Contains(e.mousePosition))
            {
                _canvasPan += e.delta;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && rect.Contains(e.mousePosition))
            {
                ResetCanvasView();
                e.Use();
            }
        }

        private void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));
            if (_scene?.Texture == null || _targetPrefab == null)
            {
                GUI.Label(rect, "选中一个挂有 SVItemBase<T> 的 prefab(或在 Prefab Mode 中编辑)",
                    new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            HandleCanvasEvents(rect);

            var tex = _scene.Texture;
            // 画布语义:宽=虚拟面板(面板宽参数),高=当前全部条目总高;RT 整幅先适配画布,再叠加用户缩放/平移
            float fit = Mathf.Min(rect.width / tex.width, rect.height / tex.height);
            float scale = fit * _canvasZoom;
            var texSize = new Vector2(tex.width, tex.height) * scale;
            var origin = rect.center - texSize * 0.5f + _canvasPan;   // RT 左上角(画布坐标)

            using (new GUI.GroupScope(rect))   // 裁剪:放大/平移超出画布的部分不画出界
            {
                var local = new Rect(origin - rect.position, texSize);
                GUI.DrawTexture(local, tex, ScaleMode.StretchToFill, false);

                // 叠加:边界/越界/重叠 + 高度标注(RT 像素 → 画布坐标)
                Vector2 Map(Vector2 px) => local.position + px * scale;

                if (_showBounds)
                {
                    foreach (var item in _scene.Items)
                    {
                        foreach (var el in item.Elements)
                        {
                            if (el.Rt == null) continue;
                            var c = new Vector3[4];
                            el.Rt.GetWorldCorners(c);
                            var p0 = Map(_scene.WorldToPixel(c[0]));
                            var p2 = Map(_scene.WorldToPixel(c[2]));
                            var r = Rect.MinMaxRect(p0.x, p0.y, p2.x, p2.y);

                            Color col;
                            if (el.IsRoot) col = new Color(0.5f, 0.75f, 0.5f, 0.9f);
                            else if (el.OverRoot) col = new Color(0.85f, 0.35f, 0.35f, 0.95f);
                            else if (el.Overlap) col = new Color(0.85f, 0.7f, 0.3f, 0.9f);
                            else col = new Color(0.55f, 0.65f, 0.8f, 0.5f);
                            DrawRectOutline(r, col, el.IsRoot ? 2f : 1f);
                        }
                    }
                }

                if (_showHeights)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel) { richText = true, alignment = TextAnchor.MiddleRight };
                    foreach (var item in _scene.Items)
                    {
                        if (item.Root == null) continue;
                        var c = new Vector3[4];
                        item.Root.GetWorldCorners(c);
                        var p0 = Map(_scene.WorldToPixel(c[0]));
                        var p2 = Map(_scene.WorldToPixel(c[2]));
                        var r = Rect.MinMaxRect(p0.x, p0.y, p2.x, p2.y);
                        var label = item.PreferredHeight > 0f
                            ? $"<color=#ffd27a>{item.PreferredHeight:F0}px</color>"
                            : "<color=#ff8888>测量失败</color>";
                        if (!string.IsNullOrEmpty(item.BindError)) label += "\n<color=#ff8888>OnBind:" + item.BindError + "</color>";
                        var lr = new Rect(r.xMax - 70f, r.yMin, 70f, 30f);
                        GUI.Label(lr, label, style);
                    }
                }
            }

            // 画布边框 + 范围参照(画在裁剪组外,始终可见):明确"框"的边界与所参照的面板宽度
            DrawRectOutline(rect, new Color(0.3f, 0.3f, 0.36f), 1f);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width * 0.55f, 16f),
                $"面板宽 {_scene.CanvasWidth:F0}px · 内容高 {_scene.ContentHeight():F0}px · 缩放 {_canvasZoom * 100f:F0}%",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + rect.width * 0.45f, rect.y + 3f, rect.width * 0.55f - 6f, 16f),
                "滚轮缩放 · 右键拖动 · F 复位", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
        }

        private static void DrawRectOutline(Rect r, Color col, float w)
        {
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, w), col);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - w, r.width, w), col);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, w, r.height), col);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.yMin, w, r.height), col);
        }

        private void DrawSidePanel(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                // 选项
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _panelWidth = EditorGUILayout.Popup(IndexOfPanel(_panelWidth), new[] { "1080", "720", "540", "自定义" }) switch
                    {
                        0 => 1080f, 1 => 720f, 2 => 540f, _ => _panelWidth
                    };
                    if (EditorGUI.EndChangeCheck()) Rebuild();
                    _showBounds = GUILayout.Toggle(_showBounds, "边界");
                    _showHeights = GUILayout.Toggle(_showHeights, "高度");
                }
                _panelWidth = Mathf.Max(320f, EditorGUILayout.DelayedFloatField("面板宽", _panelWidth));

                // 测试数据编辑器(按数据模型字段反射生成)
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("测试数据 · " + (_modelType?.Name ?? "—"),
                    EditorStyles.miniBoldLabel);
                if (_modelType == null)
                {
                    EditorGUILayout.HelpBox("目标未使用 SVItemBase<T>(无法反射数据模型)", MessageType.None);
                }
                else
                {
                    for (int i = 0; i < _datas.Count; i++)
                    {
                        var d = _datas[i];
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField("数据组 " + (i + 1), EditorStyles.miniLabel);
                                if (GUILayout.Button("删", EditorStyles.miniButton, GUILayout.Width(28)))
                                { _datas.RemoveAt(i); SaveDatas(); Rebuild(); break; }
                            }
                            EditorGUI.BeginChangeCheck();
                            foreach (var f in _fields)
                                DrawField(f, d);
                            if (EditorGUI.EndChangeCheck()) { SaveDatas(); Rebuild(); }
                        }
                    }
                    if (GUILayout.Button("+ 添加一组"))
                    { _datas.Add(Activator.CreateInstance(_modelType)); SaveDatas(); Rebuild(); }
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("组件参数(内嵌 Inspector · 改动即时预览)", EditorStyles.miniBoldLabel);
                // 内嵌原生 Inspector:对象=预览实例上的 Item 组件(不直接改 prefab)
                if (_cachedInspector != null)
                {
                    EditorGUI.BeginChangeCheck();
                    try { _cachedInspector.OnInspectorGUI(); }
                    catch (ExitGUIException) { throw; }
                    catch (System.Exception e)
                    {
                        // 异常可能发生在内嵌 Inspector 已压入 GUIClip 之后(如 Odin 接管时),
                        // 若静默吞掉继续跑会留下不平衡的 clip 栈(GUI Error: pushing more GUIClips
                        // than popping)。记录原始异常并干净放弃本事件,下一事件自动恢复平衡。
                        _cachedInspector = null;
                        _cachedInspectorTarget = null;
                        Debug.LogException(e);
                        GUIUtility.ExitGUI();
                    }
                    if (EditorGUI.EndChangeCheck()) Rebuild();

                    if (GUILayout.Button("将组件值写回 prefab"))
                        ApplyInspectorToPrefab();
                }
                else
                {
                    EditorGUILayout.HelpBox("预览实例上无 Item 组件", MessageType.None);
                }
            }
        }

        private static int IndexOfPanel(float w) =>
            Mathf.Approximately(w, 1080f) ? 0 : Mathf.Approximately(w, 720f) ? 1 : Mathf.Approximately(w, 540f) ? 2 : 3;

        private void DrawField(FieldInfo f, object d)
        {
            var ft = f.FieldType;
            var v = f.GetValue(d);
            if (ft == typeof(string))
            {
                var s = (string)v ?? "";
                f.SetValue(d, EditorGUILayout.DelayedTextField(f.Name, s));
            }
            else if (ft == typeof(bool))
                f.SetValue(d, EditorGUILayout.Toggle(f.Name, (bool)v));
            else if (ft == typeof(float))
                f.SetValue(d, EditorGUILayout.DelayedFloatField(f.Name, (float)v));
            else if (ft == typeof(double))
                f.SetValue(d, EditorGUILayout.DelayedDoubleField(f.Name, (double)v));
            else if (ft == typeof(int))
                f.SetValue(d, EditorGUILayout.DelayedIntField(f.Name, (int)v));
            else if (ft.IsEnum)
                f.SetValue(d, EditorGUILayout.EnumPopup(f.Name, (Enum)v));
            else if (ft == typeof(Sprite))
                f.SetValue(d, EditorGUILayout.ObjectField(f.Name, (Sprite)v, typeof(Sprite), false));
            else if (ft == typeof(Color))
                f.SetValue(d, EditorGUILayout.ColorField(f.Name, (Color)v));
        }

        // ---------------- 内嵌 Inspector ----------------

        private void CacheInspector()
        {
            UObject target = null;
            if (_scene != null)
            {
                foreach (var it in _scene.Items)
                {
                    if (it.Root == null) continue;
                    foreach (var mb in it.Root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (mb is ISVItemRenderer) { target = mb; break; }
                    if (target != null) break;
                }
            }
            if (target == _cachedInspectorTarget && _cachedInspector != null) return;
            _cachedInspectorTarget = target;
            if (target != null)
                UnityEditor.Editor.CreateCachedEditor(target, null, ref _cachedInspector);
            else _cachedInspector = null;
        }

        /// <summary>把内嵌 Inspector 改过的组件值(存在于预览实例上)拷回 prefab 资产。</summary>
        private void ApplyInspectorToPrefab()
        {
            var src = _cachedInspectorTarget as MonoBehaviour;
            if (src == null) return;
            GameObject prefabAsset = src.gameObject;
            var stage = PrefabStageUtility.GetPrefabStage(src.gameObject);
            if (stage != null && stage.IsPartOfPrefabContents(src.gameObject))
            {
                // Stage 编辑中:直接改内容根(随 Stage 保存)
                EditorUtility.SetDirty(src);
                Debug.Log("[SVList] 组件值已写入 Prefab Stage 内容(随 Stage 保存生效)。");
                return;
            }
            var path = AssetDatabase.GetAssetPath(_targetPrefab);
            if (string.IsNullOrEmpty(path))
            {
                // 目标是场景实例:直接拷回其源 prefab
                var srcPrefab = PrefabUtility.GetCorrespondingObjectFromSource(_targetPrefab);
                if (srcPrefab == null) { Debug.LogWarning("[SVList] 目标不是 prefab,无法写回。"); return; }
                EditorUtility.CopySerialized(src, srcPrefab);
                EditorUtility.SetDirty(srcPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log("[SVList] 组件值已写回 " + srcPrefab.name + "。");
                _targetPrefab = srcPrefab.gameObject;
                Rebuild();
                return;
            }
            var assetRoot = _targetPrefab;
            var dst = assetRoot.GetComponent(src.GetType());
            if (dst == null)
            {
                foreach (var mb in assetRoot.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb.GetType() == src.GetType()) { dst = mb; break; }
            }
            if (dst == null) { Debug.LogWarning("[SVList] prefab 上找不到对应组件。"); return; }
            EditorUtility.CopySerialized(src, dst);
            EditorUtility.SetDirty(dst);
            AssetDatabase.SaveAssets();
            Debug.Log("[SVList] 组件值已写回 " + path + "。");
            Rebuild();
        }
    }
}

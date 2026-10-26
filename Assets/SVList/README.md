# SVList 快速上手

高性能 UGUI 虚拟化滚动列表：视口复用 + 对象池 + 树状数组定位，支持**不等高 Item**、**头部插入历史不跳屏**、**高度预计算**，适合聊天、邮件、排行榜、战报等长列表。

> 本目录是基于 [CrimeyMike/SVList-Unity](https://github.com/CrimeyMike/SVList-Unity)（MIT）的二次开发版本，优化内容见文末[变更记录](#变更记录)。

## 演示场景（建议先跑一遍）

打开 `Examples/Scenes/SVListChatDemo.unity`（相对包根，本项目中即 `Assets/ThirdParty/SVList/Examples/Scenes/`）直接 Play：联盟聊天，不等高中文消息、滚到顶自动加载一页历史（画面不跳）、在底部时发送自动跟滚、左上角实时统计（总数/活跃/池/Offset/可见区间）。

演示用的是**标准接入形态**，可直接照抄：

| 内容 | 位置 |
|---|---|
| Item 预制体（`SVItemBase<ChatMessage>` 用法） | `Examples/Prefabs/ChatItemExample.prefab` |
| Item 脚本（只重写 OnBind + 兜底 MeasureHeight） | `Examples/Scripts/ChatItemExample.cs` |
| 数据源（ISVDataSource + ISVItemHeightProvider 双实现） | `Examples/Scripts/ChatExample.cs` |
| 测量常量（排版与测量公式的唯一来源） | `Examples/Scripts/ChatExample.cs` 的 `ChatExampleMetrics` |
| 场景驱动（增删/跟滚/统计的标准 API 套路） | `Examples/SVListChatDemo.cs` |
| 中文动态字体资产 | `Examples/Fonts/ChatFont SDF.asset` |

场景被误改后可用菜单 `Tools → SVList → 重建聊天演示场景` 一键重建。

---

## 30 秒接入（推荐路径）

**第 1 步：一键创建层级。** Hierarchy 右键 → `UI → SVList →` 按需选择：

| 菜单项 | 用途 |
|---|---|
| 垂直列表（通用） | 普通垂直列表 |
| **聊天列表（不等高·动态高度）** | 聊天等不等高场景，已开 DynamicHeight |
| 水平列表 | 横向列表 |

生成的 `ScrollRect / Viewport / Content` 锚点已按库要求搭好（Content 顶部锚定是最常见的接入错误，菜单已代劳）。

**第 2 步：写 Item 脚本**（继承 `SVItemBase<T>`，只重写 `OnBind` 一个方法）：

```csharp
using TMPro;
using UnityEngine;
using SVList;

public class ChatItem : SVItemBase<ChatMessage>
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _msgText;

    protected override void OnBind(ChatMessage data, int index)
    {
        _nameText.text = data.Sender;
        _msgText.text = data.Text;
    }
}
```

做成预制体，挂回 `SVListView` 的 **Item Prefabs** 列表（第 0 个 = PrefabID 0）。

**第 3 步：初始化：**

```csharp
view.Initialize(messages);   // messages : IList<ChatMessage>
```

完成。增删数据、滚动控制的 API 见下文。

### 纯代码路径（无预制体）

```csharp
// 先建好 ScrollRect/Viewport/Content 层级（参考 SVListChatDemo.BuildUi）
view.RegisterItemPrefab(itemTemplate);   // 运行时注册模板，PrefabID = 0
view.Initialize(dataSource);
```

---

## 不等高聊天（重点）

不等高的关键是**高度在实例化之前就要知道**。两条路，可叠加：

### 方式 A：高度预计算（推荐，零跳动）

数据源同时实现 `ISVItemHeightProvider`，在数据层用 TMP 把高度量准：

```csharp
public class ChatDataSource : ISVDataSource, ISVItemHeightProvider
{
    public List<ChatMessage> Messages;
    public TMP_Text MeasureText;      // 隐藏的测量用 TMP（字体/字号与消息文本一致！）

    public int GetItemCount() => Messages.Count;
    public object GetItemData(int index) => Messages[index];

    public float GetItemHeight(int index)
    {
        float textH = MeasureText.GetPreferredValues(Messages[index].Text, TextWidth, 0f).y;
        return textH + NameRowHeight + VerticalPadding;   // 与 Item 内排版一致的公式
    }
}
```

> ⚠️ **测量一致性**：`GetItemHeight` 的公式（字体、字号、可用宽度、固定附加高度）必须和 Item 预制体里的实际排版完全一致，否则会出现滚动修正跳动。参考 `SVListChatDemo` 的写法。
> 返回 `<= 0` 表示该条不提供，自动回落 `EstimateHeight` 估算。

### 方式 B：实例化后测量（兜底，无需 Provider）

Item 脚本重写 `MeasureHeight()` 返回真实高度（例如 `LayoutUtility.GetPreferredHeight` 或 TMP 测量）：

```csharp
protected override float MeasureHeight()
    => _msgText.GetPreferredValues(Data.Text, _msgText.rectTransform.rect.width, 0f).y + 44f;
```

滚动经过时首次修正；视口上方修正会自动补偿滚动位置（本版本新增），不会跳屏。

---

## 数据增删（增量，不重建列表）

调用前**先改数据源，再通知列表**：

```csharp
// 头部加载一页历史 —— 视口自动补偿，画面不跳（核心特性）
messages.InsertRange(0, historyPage);
view.InsertItems(0, historyPage.Count);

// 追加新消息
messages.Add(msg);
view.AppendItem();                 // 或 AppendItems(n)

// 删除
messages.RemoveAt(i);
view.RemoveItem(i);                // 或 RemoveItems(start, count)

// 单条内容变化（如"发送中→失败"）
messages[i] = updated;
view.RefreshItem(i);
```

---

## 滚动控制（聊天跟滚套路）

```csharp
// 新消息到达：只有在底部才跟滚（微信行为）
bool wasAtBottom = view.IsAtBottom(48f);
messages.Add(msg);
view.AppendItem();
if (wasAtBottom) view.ScrollToBottomSmooth();

// 顶部自动加载历史（配合 InsertItems(0, n)）
if (view.Core.State.ScrollOffset < 80f) { /* 拉取后 InsertItems(0, n) */ }

// 跳转
view.JumpToIndex(123, SVJumpMode.Center);
view.ScrollToBottom();
```

---

## API 速查

### SVListView

| API | 说明 |
|---|---|
| `Initialize(IList<T>)` / `Initialize(ISVDataSource)` | 初始化（会自检：未注册模板会报错并给出修法） |
| `RegisterItemPrefab(GameObject)` | 运行时注册模板（代码建 UI 时用，须在 Initialize 前） |
| `SetHeightProvider(ISVItemHeightProvider)` | 指定高度提供者（数据源实现了该接口则自动识别） |
| `InsertItem(s) / RemoveItem(s) / AppendItem(s)` | 增删（先改数据源再调用） |
| `RefreshItem(index)` / `Refresh()` | 单条刷新 / 全量重建 |
| `IsAtBottom(threshold=16)` | 是否在底部 |
| `ScrollToBottom()` / `ScrollToBottomSmooth()` | 滚到底（立即/平滑） |
| `JumpToIndex(index, mode)` / `JumpToIndexSmooth` | 跳到指定条 |
| `GetRenderer(index)` | 取可见条目的渲染器（不可见返回 null） |
| `Core` | SVListCore：`State.ScrollOffset` / `MaxScrollOffset` / `DebugInfo` 等 |

### SVItemBase\<T\>（继承它，别裸写 ISVItemRenderer）

| 方法 | 必须重写 | 说明 |
|---|---|---|
| `OnBind(T data, int index)` | ✅ | 绑定数据刷新 UI |
| `OnCreated()` | | 一次性缓存组件 |
| `OnUnbound()` | | 取消监听/停协程 |
| `MeasureHeight()` | | 动态高度测量（走 Provider 时可不动） |
| `Data` / `Index` | | 当前绑定数据（只读属性） |

### SVConfig 常用参数

| 参数 | 默认 | 说明 |
|---|---|---|
| `DynamicHeight` | true | 动态高度 |
| `EstimateHeight` | 100 | 未测量条目的估算高度（尽量接近均值，滚动条更准） |
| `Spacing` / `PaddingTop/Bottom` | 10 / 5 | 间距与内边距 |
| `MaxInstantiatePerFrame` | 5 | 每帧实例化上限（首次填充可调大） |
| `PreWarmCount` | 20 | 池预热数量 |
| `Reverse` | false | 反转滚动方向 |
| `Direction` | Vertical | Vertical / Horizontal / Grid |

### 多模板（文本/系统公告/图片混排）

把多个预制体按顺序拖进 Item Prefabs（下标即 PrefabID），初始化时传选择器：

```csharp
view.Initialize(dataSource, index => dataSource[index] is SystemMsg ? 1 : 0);
```

---

## 常见坑（FAQ）

1. **列表空白/不显示**：看 Console——接入自检会明确指出缺 Viewport/Content/模板；Viewport 必须有实际尺寸。
2. **列表拖不动**：Viewport 内必须有 `raycastTarget=true` 的 Graphic（UGUI 拖动依赖 EventSystem 射线，`RectMask2D` 本身不参与射线检测）。一键菜单生成的 Viewport 已带一张透明 Image 承接射线；手搭层级时在 Viewport 或 Content 背景放一张透明/纯色 Image 即可。注意 Item 内文本若全部关闭 raycastTarget，事件会落到 Viewport 这层 Image 上，不影响拖动。
3. **Content 锚点**：垂直列表必须顶部锚定（`anchorMin/Max.y=1`、`pivot.y=1`）。用一键菜单创建不会错；手搭错了会有警告提示。
4. **不等高跳动**：说明 Provider 公式和实际排版不一致（字体/字号/宽度对不上），或没用 Provider 且估算偏差大（视口上方的修正已自动补偿，但首次快速滚动仍可能有轻微调整）。
5. **Provider 高度整体偏小/离谱**：检查测量用的 TMP 是否被 `SetActive(false)`——场景序列化的 inactive TMP 在 Play 时不会执行 Awake/OnEnable，内部状态未初始化，`GetPreferredValues` 会返回错误值。测量文本应保持激活（移出画布/全透明即可）。
6. **中文显示方框**：默认 TMP 字体不含中文。运行时用 `TMP_FontAsset.CreateFontAsset(中文字体)` 生成动态字体（见 Demo），或直接使用示例资产 `Examples/Fonts/ChatFont SDF.asset` 的生成方式。
7. **Grid 模式**：不支持增量插入/删除（自动退化为全量刷新）；高度 Provider 也不适用 Grid（按行高缓存）。
8. **嵌套滚动**（聊天窗嵌在大滚动页）：未在本项目验证，接入时注意 UGUI ScrollRect 嵌套事件透传。

---

## 性能说明（GC）

实测（演示场景，1080 参考分辨率，持续来回滚动）：

| 阶段 | 滚动期托管内存分配 |
|---|---|
| 优化前 | ~7.3 KB/帧 |
| 库内 GC 源修复后 | ~4.6 KB/帧 |
| 同场景静音 TMP 文本赋值（对照） | **~0.7 KB/帧** |

即：**库自身滚动热路径（核心逻辑/高度缓存/容器/调度/滚动控制）已零分配**，剩余分配的 ~85% 来自 TMP 给消息文本赋值的内部处理（文本变化是聊天业务本质）。若需进一步压榨，方向在 TMP 侧（减少富文本标签、长文分段、升级 TMP 版本），而非列表库。

已做的库内优化：
- 高度 Provider 已提供的条目**跳过 renderer 测量复核**（TMP `GetPreferredValues` 每次 ~1-2KB 分配 + CPU，滚动热路径上已省掉）
- Provider 委托缓存（方法组转换每次求值都会分配新委托）
- `HeightInfo` 对象池化（插入/删除零 new）
- `ShiftIndices` 缓冲列表复用（不再每次 new List）
- 演示驱动的统计文本节流 0.25s + 变化检测（不再每帧拼字符串/重建 TMP）

## 变更记录（相对原版）

- `InsertItems / RemoveItems` **增量路径**：原版插入/删除走全量 Refresh（丢失全部已测量高度 + 滚动跳顶）；现改为增量更新 + **视口补偿**（头部插入历史画面不动）
- **`ISVItemHeightProvider` 高度预计算**：数据层预量高度，消除"估算→修正"跳动；数据源实现该接口自动识别
- 估算修正模式补充**视口上方测量的滚动补偿**（无 Provider 也不跳）
- 新增 API：`IsAtBottom / ScrollToBottom / ScrollToBottomSmooth / AppendItem(s) / RegisterItemPrefab / GetRenderer / MaxScrollOffset`
- 新增 `SVItemBase<T>` 便捷基类（5 方法接口 → 只重写 OnBind）
- 新增**接入自检**：层级/锚点/模板缺失给出带修法的报错
- 新增 **Hierarchy 一键创建菜单**（垂直/聊天/水平）
- 新增**预制体化演示场景**（真实 Item 预制体 + 场景接线 + 中文动态字体资产）与 `Tools → SVList → 重建聊天演示场景` 重建工具
- 修复**首次布局重叠**：重复 Initialize 不再泄漏旧 Item（core.Initialize 幂等回收 + view.Start 防重入）；演示场景测量文本不能 inactive（inactive 的 TMP 未初始化，`GetPreferredValues` 返回错误值，见 FAQ）
- 修复**头部加载时惯性被打断**：插入/删除的视口补偿改走增量平移（`SVScrollController.ShiftScrollOffset`），不再经过会清空 `ScrollRect.velocity` 的 `verticalNormalizedPosition`
- 修复 PoolRoot 重复创建、`GetRenderer` 返回 null 的半成品
- 补充 EditMode 单测 17 项（树状数组 / 高度缓存 / 插入补偿行为），位于 `Tests/EditMode/`

## 已知限制

- Grid 模式增量增删退化为全量刷新
- 与 `NestedScrollRect` 等嵌套滚动的兼容未验证
- 滚动条 Thumb 在估算高度未测量区间会有轻微比例漂移（用 Provider 后基本消失）

## 许可证

MIT（见 [LICENSE.md](LICENSE.md)）。基于 [CrimeyMike/SVList-Unity](https://github.com/CrimeyMike/SVList-Unity) 二次开发，保留原作者版权声明。

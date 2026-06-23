# SVList

高性能、可扩展、零 GC 的 Unity UGUI 虚拟列表框架。

SVList 并非对 UGUI ScrollView 的简单封装，而是从架构层重新设计的一套支持百万级数据、动态高度、多模板和可插拔布局的虚拟列表系统。

---

# 特性

* ✅ 百万级数据虚拟化
* ✅ 动态高度（Fenwick Tree）
* ✅ 多模板列表项
* ✅ 零 GC 滚动
* ✅ 可插拔布局系统
* ✅ 帧预算实例化
* ✅ 自适应缓冲区
* ✅ 锚点补偿
* ✅ 重入保护
* ✅ 多级对象池
* ✅ 运行时参数调整
* ✅ Gizmos 调试支持
* 🚧 瀑布流布局（规划中）
* 🚧 分页加载（规划中）
* 🚧 Sticky Header（规划中）

---

# 一、架构设计

## 整体架构

```
SVListView (MonoBehaviour 外壳)
        ↓
SVListCore（纯 C# 核心）
        │
        ├── ISVLayout
        ├── SVVisibleManager
        ├── SVHeightCache
        ├── SVObjectPoolManager
        ├── SVInstantiateScheduler
        ├── SVRecycleScheduler
        └── SVItemContainer
```

SVListCore 不依赖 UnityEngine。

因此：

* 可以进行单元测试
* 不需要场景和 GameObject
* 所有模块可独立替换
* 更容易维护和扩展

---

# 二、相比 UGUI ScrollView 的优势

| 特性           | UGUI ScrollRect | SVList |
| ------------ | --------------- | ------ |
| 100 万项       | ❌ 崩溃            | ✅ 支持   |
| 内存占用         | O(N)            | O(1)   |
| 首次加载         | 全部 Instantiate  | 仅创建可见项 |
| 滚动性能         | 全量参与布局          | 仅活跃项参与 |
| 动态高度         | ❌               | ✅      |
| 多模板          | ❌               | ✅      |
| 零 GC         | ❌               | ✅      |
| 运行时配置        | ❌               | ✅      |
| 可测试性         | ❌               | ✅      |
| Frame Budget | ❌               | ✅      |

---

# 三、核心模块

## 1. SVHeightCache + Fenwick Tree

### 问题

动态高度列表需要频繁回答：

> offset 对应哪个 index？

普通线性扫描：

```
O(N)
```

数据量大时成本极高。

### 解决方案

采用 Fenwick Tree（二叉索引树）：

```
GetOffset(index)
O(logN)

FindIndexByOffset(offset)
O(logN)

UpdateHeight(index,height)
O(logN)
```

支持百万级动态高度列表。

---

## 2. SVVisibleManager（Diff 更新）

### 问题

滚动事件触发后重新构建所有项：

```
Destroy All
Create All
```

造成：

* CPU 浪费
* GC
* 卡顿

### 解决方案

比较：

```
PreviousRange

CurrentRange
```

仅处理变化部分：

```
滚动前

[0][1][2][3][4]|5|6|7|8|9|10|11|[12][13][14]

滚动后

[0][1][2][3][4][5]|6|7|8|9|10|11|12|[13][14][15]
```

Diff：

```
回收 5

创建 15
```

而不是全部重建。

---

## 3. 自适应缓冲区

### 问题

固定 Buffer：

快速滚动时容易出现空白。

### 解决方案

动态 Buffer：

```cpp
bufferSize =
baseBuffer
+
speed × BUFFER_SPEED_FACTOR
```

限制：

```cpp
0.5 × viewportSize
~
4 × viewportSize
```

特点：

慢速：

* Buffer 小

快速：

* Buffer 自动扩大

跳跃：

* 紧急重建

---

## 4. 帧预算实例化

模块：

```
SVInstantiateScheduler
```

### 问题

跳转时一次创建大量对象：

```cpp
Instantiate 30 个 Item
```

容易掉帧。

### 解决方案

限制：

```cpp
MaxInstantiatePerFrame = 5
```

跨多帧创建：

```
Frame1
5

Frame2
5

Frame3
5
```

保证帧率稳定。

---

## 5. 延迟回收

模块：

```
SVRecycleScheduler
```

### 问题

快速来回滚动：

```
Recycle
↓

马上 Get
↓

Recycle
↓

马上 Get
```

产生抖动。

### 解决方案

达到阈值后才回收：

```cpp
ShouldRecycle(count)
```

避免：

```
回收 → 立即重用
```

的反模式。

---

## 6. 零 GC 滚动

已完成优化：

### ActiveItem 对象池

避免：

```cpp
new ActiveItem()
```

---

### 缓存委托

```cpp
_cachedCreateAction

_cachedRecycleAction
```

避免方法组转换分配。

---

### GC 调试节流

原来：

```
每帧调用 GC.GetTotalMemory()
```

现在：

```
60 帧调用一次
```

降低调试开销。

---

### 临时 List 复用

避免：

```cpp
new List()
```

实现滚动零 GC。

---

## 7. 锚点补偿

### 问题

ContentSizeFitter 导致高度变化：

```
视觉跳动
```

### 解决方案

记录：

```
AnchorIndex
```

高度变化：

```
deltaHeight
```

补偿：

```
contentPosition += deltaHeight
```

保证顶部项稳定。

---

## 8. 重入保护

### 问题

可能形成：

```
OnScrollChanged

↓

CreateItem

↓

Canvas Rebuild

↓

OnScrollChanged

↓

无限递归
```

### 解决方案

加入：

```cpp
_isProcessingScroll
```

检测重入后直接返回。

---

## 9. 多模板对象池

通过：

```cpp
prefabID
```

支持：

* 标题
* 普通项
* 分隔符
* 特殊 Item

共存于一个列表。

---

# 四、剩余优化

---

## 高优先级

### 1. MeasureScheduler

问题：

ContentSizeFitter 的最终高度在布局完成前无法得到。

现状：

首次出现高度错误。

下一帧修正。

产生跳动。

建议：

延迟到：

```
Canvas.willRenderCanvases
```

之后统一测量。

---

### 2. InsertItem / RemoveItem 增量更新

现状：

调用：

```cpp
Refresh()
```

导致：

```
回收全部

重新创建全部
```

百万级列表插入一个元素成本过高。

目标：

仅更新受影响部分。

避免全量刷新。

---

### 3. 网格动态高度 Bug

当前：

HeightCache 保存行高度。

但：

```cpp
UpdateItemHeight(index)
```

使用的是 Item Index。

导致映射错误。

需要：

```
index
↓

rowIndex
```

转换层。

---

# 中优先级

## 异步实例化

利用：

* Addressables
* AssetBundle

提前预热。

减少主线程卡顿。

---

## LRU 对象池

当前：

FIFO。

问题：

滚动到 1000 项后池会一直保留。

建议：

最近最少使用淘汰策略。

加入：

```cpp
MaxPoolSize
```

软限制。

---

## Viewport 裁剪优化

当前：

仅检测 Y。

未来：

增加：

* X 轴裁剪
* Grid 列裁剪

减少无效实例化。

---

## 批量 Canvas 更新

当前：

逐个修改：

```cpp
anchoredPosition
```

未来：

统一批量更新。

降低 BuildBatch 开销。

---

# 五、未来规划

## SVMasonryLayout

瀑布流布局。

将成为重要竞争优势。

---

## 编辑器预览

支持：

```cpp
MockItemCount = 20
```

无需运行即可预览列表效果。

---

## 淡入淡出

使用：

```cpp
CanvasGroup
```

实现边缘渐隐效果。

---

## 分页加载

新增：

```cpp
HasMoreData()

LoadMoreAsync()
```

自动加载更多。

适用于：

* API
* 聊天
* 社交 Feed

---

## Sticky Header

支持：

```
A
B
C
```

联系人列表。

背包分类。

排行榜。

---

## 键盘 / 手柄导航

支持：

```cpp
ISelectHandler
```

适配：

* PC
* Console
* SteamDeck

---

# 六、竞争力分析

| 特性           | SVList    | UGUI ScrollRect | 常见 Asset Store 插件 |
| ------------ | --------- | --------------- | ----------------- |
| 动态高度         | ✅ O(logN) | ❌               | ⚠️ O(N)           |
| 网格布局         | ✅         | ❌               | ⚠️                |
| 多模板          | ✅         | ❌               | ⚠️                |
| 零 GC         | ✅         | ❌               | ⚠️                |
| 纯逻辑核心        | ✅         | ❌               | ❌                 |
| 运行时配置        | ✅         | ❌               | ❌                 |
| Frame Budget | ✅         | ❌               | ⚠️                |
| Gizmos 调试    | ✅         | ❌               | ⚠️                |
| 瀑布流          | 🚧        | ❌               | 少量支持              |
| 单元测试         | ✅         | ❌               | ❌                 |

---

# 七、总结

SVList 在以下方面已经显著优于传统 UGUI ScrollView：

### 可扩展性

Fenwick Tree + 虚拟化使列表容量理论上无限。

### 性能

对象池、Diff 算法、零 GC 滚动保证高帧率。

### 稳定性

锚点补偿、重入保护、帧预算机制避免抖动和卡顿。

### 灵活性

支持：

* 多模板
* 可插拔布局
* 运行时配置
* 自定义扩展

当前最大的改进方向并非正确性，而是：

### 降低变更成本

未来目标：

> 插入一个元素，不应付出刷新一百万项的代价。

让 SVList 从一个高性能虚拟列表框架，进一步演化为工业级 UI 数据展示解决方案。

# SVList

高性能 UGUI 虚拟列表框架 / High-Performance UGUI Virtual List Framework

![Unity](https://img.shields.io/badge/Unity-2020.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/license-MIT-blue)
![Status](https://img.shields.io/badge/status-production--ready-green)

## 目录 / Table of Contents

- [概述 / Overview](#概述--overview)
- [安装 / Installation](#安装--installation)
- [架构 / Architecture](#架构--architecture)
- [快速开始 / Quick Start](#快速开始--quick-start)
- [配置参数 / Configuration](#配置参数--configuration)
- [运行时 API / Runtime API](#运行时-api--runtime-api)
- [接口 / Interfaces](#接口--interfaces)
- [自定义布局 / Custom Layouts](#自定义布局--custom-layouts)
- [编辑器功能 / Editor Features](#编辑器功能--editor-features)
- [性能数据 / Performance](#性能数据--performance)
- [项目结构 / Project Structure](#项目结构--project-structure)
- [环境要求 / Requirements](#环境要求--requirements)
- [许可证 / License](#许可证--license)
- [贡献指南 / Contributing](#贡献指南--contributing)

---

## 概述 / Overview

SVList 是一个面向 Unity UGUI 的生产级虚拟滚动列表框架。通过仅实例化屏幕上可见（含预加载缓冲区）的 Item，支持 **100 万以上数据项** 在 60 FPS 下流畅滚动。

SVList is a production-grade virtual scroll list for Unity UGUI. It handles **1,000,000+ items** at 60 FPS by only instantiating items currently visible on screen, plus a configurable buffer zone.

| 特性 / Feature | 状态 / Status |
|----------------|---------------|
| 百万级数据 60FPS / 1M+ items at 60 FPS | 已实现 / Done |
| 动态高度 (ContentSizeFitter) / Dynamic height | 已实现 / Done |
| 垂直 / 水平 / 网格布局 / Vertical, Horizontal, Grid | 已实现 / Done |
| 固定列"背包"网格 / Fixed-column backpack grid | 已实现 / Done |
| 多模板 (多个 Prefab) / Multi-template | 已实现 / Done |
| 零 GC 滚动 / Zero GC scrolling | 已实现 / Done |
| 运行时调整间距和内边距 / Runtime spacing & padding | 已实现 / Done |
| Fenwick 树 O(log N) 偏移查询 / Fenwick tree queries | 已实现 / Done |
| 分帧实例化 / Frame-budgeted instantiation | 已实现 / Done |
| Editor Gizmos 详细调试覆盖层 / Detailed Gizmos | 已实现 / Done |
| 纯 C# 核心逻辑 (可单元测试) / Pure-C# core | 已实现 / Done |
| 瀑布流布局 / Masonry layout | 计划中 / Planned |
| 单元测试 / Unit tests | 计划中 / Planned |

---

## 安装 / Installation

### UPM (Unity Package Manager)

通过 Git URL 安装 / Install via Git URL:

1. 打开 **Window > Package Manager**
2. 点击 **+** > **Add package from git URL...**
3. 输入 / Enter:

```
https://github.com/CrimeyMike/SVList-Unity.git?path=/Assets/SVList
```

安装指定版本 / Install a specific version:

```
https://github.com/CrimeyMike/SVList-Unity.git?path=/Assets/SVList#v1.0.0
```

### 手动安装 / Manual

将仓库克隆到项目的 `Packages/` 目录下：

Clone this repo into your project's `Packages/` folder:

```bash
cd YourProject/Packages
git clone https://github.com/CrimeyMike/SVList-Unity.git com.svlist.unity
```

---

## 架构 / Architecture

```
SVListView (MonoBehaviour 薄壳入口)
  └── SVListCore (纯 C# 逻辑, 零 UnityEngine 依赖)
        ├── ISVLayout          可插拔布局策略 / Pluggable layout
        │     ├── SVVerticalLayout     垂直列表
        │     ├── SVHorizontalLayout   水平列表
        │     └── SVGridLayout         网格 (背包)
        ├── SVVisibleManager    可见区计算 + Diff 算法
        ├── SVHeightCache       高度缓存 + Fenwick 树 O(log N)
        ├── SVObjectPoolManager 对象池 (多模板支持)
        ├── SVInstantiateScheduler  分帧创建调度
        ├── SVRecycleScheduler      延迟回收调度
        └── SVItemContainer         活跃元素容器
```

`SVListCore` 不继承 `MonoBehaviour`，不依赖 `GameObject`、`Transform` 或任何 Unity 类型。整个滚动逻辑可以在没有 Unity 场景的情况下进行单元测试。

`SVListCore` has zero dependency on `GameObject`, `Transform`, or any Unity type. The entire scrolling logic can be unit-tested without a Unity scene.

---

## 快速开始 / Quick Start

### 1. 场景搭建 / Scene Setup

1. 在场景中创建一个 Canvas。
2. 在 Canvas 下添加带有 Viewport 和 Content 子节点的 ScrollRect。
3. 将 `SVListView` 组件挂载到 ScrollRect 所在的 GameObject 上。
4. 在 Inspector 中拖入 ScrollRect、Viewport、Content 的引用。
5. 将一个或多个 Item Prefab 添加到 Item Prefabs 列表中。

### 2. Item Prefab

你的 Item Prefab 必须实现 `ISVItemRenderer` 接口，或继承提供的基类：

Your item prefab must implement `ISVItemRenderer`, or extend the provided base class:

```csharp
using SVList;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MyItemRenderer : SVItemRendererBase
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private Image _icon;

    public override void OnBind(object data, int index)
    {
        base.OnBind(data, index);
        var item = data as MyData;
        if (item == null) return;

        _nameText.text = item.Name;
        _valueText.text = item.Value.ToString();
        _icon.sprite = item.Icon;
    }
}
```

### 3. 初始化 / Initialize

```csharp
using SVList;
using System.Collections.Generic;
using UnityEngine;

public class Demo : MonoBehaviour
{
    [SerializeField] private SVListView _listView;

    void Start()
    {
        // 生成 10 万条数据
        var data = new List<MyData>();
        for (int i = 0; i < 100_000; i++)
            data.Add(new MyData { Name = $"Item {i}", Value = i * 100 });

        _listView.Initialize(data);

        // 多模板: 通过 prefabSelector 返回不同 Prefab ID
        // _listView.Initialize(data, index => index == 0 ? 0 : 1);
    }
}
```

---

## 配置参数 / Configuration

所有参数通过 `SVConfig` 在 Inspector 中配置，也可在代码中修改。

All parameters are configured through `SVConfig` in the Inspector, or via code.

### 布局 / Layout

| 参数 / Parameter | 说明 / Description |
|------------------|---------------------|
| Direction | Vertical, Horizontal, Grid |
| Spacing | 元素间距 (垂直/水平模式) |
| Padding Top | 上方内边距 |
| Padding Bottom | 下方内边距 |
| Padding Left | 左侧内边距 (Grid 模式) |
| Padding Right | 右侧内边距 (Grid 模式) |
| Default Item Size | 默认 Item 宽度或高度 |
| Reverse | 反转滚动方向 |

### 网格 (背包) / Grid

| 参数 / Parameter | 说明 / Description |
|------------------|---------------------|
| Grid Column Count | 列数 |
| Grid Cell Width | 单元格宽度 |
| Grid Cell Height | 单元格高度 (0=动态行高) |
| Grid Horizontal Spacing | 列间距 |
| Grid Vertical Spacing | 行间距 |

### 动态高度 / Dynamic Height

| 参数 / Parameter | 说明 / Description |
|------------------|---------------------|
| Dynamic Height | 是否启用 ContentSizeFitter 驱动高度 |
| Estimate Height | 未测量 Item 的默认预估高度 |
| Height Change Threshold | 高度变化阈值 (小于此值忽略, 防止死循环) |

### 性能 / Performance

| 参数 / Parameter | 说明 / Description |
|------------------|---------------------|
| Preload Factor | 缓冲区倍数 (0.5=半屏, 1.0=全屏) |
| Max Pool Size | 每种 Prefab 的对象池上限 |
| Max Instantiate Per Frame | 每帧最大实例化数 (防止卡顿) |
| PreWarm Count | 初始化时预创建的实例数 |

### 调试 / Debug

| 参数 / Parameter | 说明 / Description |
|------------------|---------------------|
| Show Gizmos | 在 Scene 视图中绘制 Viewport/Content/滚动位置 |
| Show Detailed Gizmos | 绘制每项线框、网格线、高度状态、统计面板 |
| Show Debug Panel | 运行时显示调试面板 |

---

## 运行时 API / Runtime API

### 核心操作 / Core Operations

```csharp
// 初始化
listView.Initialize(myDataList);
listView.Initialize(myDataSource);                      // 自定义 ISVDataSource
listView.Initialize(myDataList, i => i % 2 == 0 ? 0 : 1); // 多模板

// 刷新
listView.Refresh();                                     // 重建整个列表
listView.RefreshItem(42);                               // 重新绑定单个 Item

// 插入 / 删除
listView.InsertItem(10);
listView.RemoveItem(10);

// 跳转
listView.JumpToIndex(100);                              // 瞬间跳转
listView.JumpToIndex(100, SVJumpMode.Center);           // 居中
listView.JumpToIndexSmooth(100, SVJumpMode.Bottom);     // 平滑动画

// 对象池
listView.ClearPool();                                   // 清空所有池化实例
```

### 运行时调整布局 / Runtime Layout

```csharp
// 间距和内边距
listView.SetSpacing(10f);
listView.SetPadding(top: 20f, bottom: 30f);
listView.SetPaddingAll(top: 15f, bottom: 15f, left: 10f, right: 10f);

// 网格参数
listView.SetGridParams(
    columns: 4,
    cellWidth: 120f,
    cellHeight: 120f,
    horizontalSpacing: 8f,
    verticalSpacing: 8f
);

// 批量更新
listView.Config.GridColumnCount = 5;
listView.Config.Spacing = 12f;
listView.ApplyLayoutChanges();
```

---

## 接口 / Interfaces

### ISVDataSource

```csharp
public interface ISVDataSource
{
    int GetItemCount();
    object GetItemData(int index);
}

// 泛型版本 / Generic version
public interface ISVDataSource<T> : ISVDataSource
{
    new T GetItemData(int index);
}
```

### ISVItemRenderer

```csharp
public interface ISVItemRenderer
{
    void OnCreate();                          // 实例化后仅调用一次
    void OnBind(object data, int index);      // 每次进入视图时调用
    void OnUnbind();                          // 每次离开视图时调用
    void OnRecycle();                         // 彻底销毁时调用
    float GetPreferredHeight();               // 动态高度: 返回测量后的高度
}
```

推荐继承 `SVItemRendererBase` 基类，它已自动缓存 `RectTransform` 和 `LayoutElement` 引用。

Use the provided `SVItemRendererBase` abstract class for convenience -- it caches `RectTransform` and `LayoutElement` references automatically.

---

## 自定义布局 / Custom Layouts

实现 `ISVLayout` 接口即可创建自定义布局 (瀑布流、环形等):

Implement `ISVLayout` to create custom layouts (waterfall, circular, etc.):

```csharp
public interface ISVLayout
{
    Vector2 GetPosition(int index);
    float GetSize(int index);
    float GetContentSize();
    int FindIndexByOffset(float offset);
    VisibleRange CalculateRange(float offset, float viewportSize, float bufferSize);
    void UpdateParameters(SVConfig config);
}
```

在 `SVListCore.CreateLayout()` 中注册你的布局即可生效。

Register your layout in `SVListCore.CreateLayout()`.

---

## 编辑器功能 / Editor Features

### 自定义 Inspector

- 运行时信息实时显示: FPS, 活跃/池化数量, 可见范围, 内存, 滚动进度条
- 快捷操作按钮: Refresh, Clear Pool, Log Info, JumpTo
- 配置面板: 按分类组织的所有参数

### Gizmos 调试

在 Config 中启用 Show Gizmos 后，Scene 视图会显示:

| 模式 / Mode | 绘制内容 / Draws |
|-------------|-------------------|
| 基础 / Basic | Viewport 绿色边框, Content 黄色区域, 滚动位置红色指示线, 可见范围标签 |
| 详细 / Detailed | 以上全部 + 每项蓝色线框+索引标签, 绿色/橙色高度状态点 (已测量/估计), 网格列行虚线, 统计面板 (Active/Pool/Total/队列/FPS/状态) |

---

## 性能数据 / Performance

| 场景 / Scenario | 耗时 / Time |
|----------------|-------------|
| 10 万项动态高度滚动 | 小于 3 ms/帧 |
| 100 万项动态高度滚动 | 小于 5 ms/帧 |
| 稳态滚动 GC 分配 | 0 (零) |

### 关键优化

- **Fenwick 树**: 动态高度下 O(log N) 偏移到索引的查找
- **Diff 算法**: 仅创建进入可见区的项，仅回收离开可见区的项
- **对象池**: ActiveItem 包装器、GameObject 实例、委托全部池化, 避免滚动时分配
- **重入锁**: 防止 Canvas 布局重建触发 ScrollRect 回调形成死循环
- **锚点补偿**: 动态高度项大小变化时自动调整 Content 位置, 防止画面跳动
- **自适应缓冲区**: 快速滑动时增大, 空闲时缩小
- **分帧创建**: 每帧最多实例化 5 个, 避免尖峰卡顿

---

## 项目结构 / Project Structure

```
Assets/
  SVList/                   UPM 包 / Package (安装路径: ?path=/Assets/SVList)
    package.json
    Runtime/
      Core/                 SVListView.cs, SVListCore.cs, SVListState.cs, SVListStatus.cs
      Layout/               ISVLayout.cs, SVVerticalLayout.cs, SVHorizontalLayout.cs, SVGridLayout.cs
      Visible/              SVVisibleManager.cs, VisibleRange.cs, VisibleState.cs, DiffResult.cs
      Height/               SVHeightCache.cs, SVFenwickTree.cs, HeightInfo.cs
      Container/            SVItemContainer.cs, ActiveItem.cs, ItemState.cs
      Pool/                 SVObjectPoolManager.cs, SVPoolNode.cs
      Scheduler/            SVInstantiateScheduler.cs, SVRecycleScheduler.cs, CreateRequest.cs, RecycleRequest.cs
      Renderer/             ISVItemRenderer.cs, SVItemRendererBase.cs
      Data/                 ISVDataSource.cs
      Controller/           SVScrollController.cs
      Config/               SVConfig.cs
      Debug/                DebugInfo.cs, SVProfiler.cs, SVDebugPanel.cs
      Animation/            SVTweenScroll.cs
      Utils/                SVUtils.cs
    Editor/
      SVListViewEditor.cs
  Prefabs/                  Demo 预制体
  Scripts/
    Core/                   DemoInitializer.cs
```

---

## 环境要求 / Requirements

- Unity 2020.3 或更新版本
- UGUI (UnityEngine.UI)
- TextMesh Pro (可选, Demo Item 使用; 你可以使用任意 UI 组件)

## AI-assisted Development

SVList was developed with extensive AI assistance and iterative vibe coding workflows.

Architecture design, performance optimization, and code implementation were continuously reviewed and refined by the author.

AI served as a productivity tool, while overall architecture and engineering decisions remained under human supervision.

## 许可证 / License

MIT

## 贡献指南 / Contributing

1. 先阅读 `Assets/SVList/Runtime/Core/SVListCore.cs` -- 它是所有子系统的调度中心。
2. 核心逻辑必须保持纯 C# (不依赖 MonoBehaviour) -- 新功能应添加到 `SVListCore` 而非 `SVListView`。
3. 所有新布局实现 `ISVLayout` 接口。
4. 保持命名约定: 框架类型统一使用 `SV` 前缀。
5. 提交前请查看项目记忆文件中的 `bugs-fixed.md`，避免引入已修复的 Bug。

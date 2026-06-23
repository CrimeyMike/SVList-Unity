# Changelog

All notable changes to SVList will be documented in this file.

## [1.0.0] - 2025-06-23

### Added
- Vertical, horizontal, and grid layouts with pluggable `ISVLayout` interface
- Fixed-column "backpack" grid with adjustable spacing, padding, and cell size
- Dynamic item height via ContentSizeFitter with O(log N) Fenwick tree queries
- Multi-template support via `prefabID` and custom prefab selector
- Zero-GC scrolling (pooled `ActiveItem` wrappers, cached delegates, reused temp lists)
- Frame-budgeted instantiation scheduler (`MaxInstantiatePerFrame`)
- Deferred recycle scheduler with conditional trigger
- Adaptive buffer zone (expands during fast scroll, shrinks when idle)
- Anchor compensation to prevent visual jumping on dynamic height changes
- Scroll re-entrancy guard to prevent feedback loop crashes
- Pure-C# `SVListCore` architecture (zero UnityEngine dependency, unit-testable)
- `SVScrollController` bridging UGUI `ScrollRect` to core logic
- `SVConfig` ScriptableObject-style Inspector configuration
- Runtime layout API (`SetSpacing`, `SetPadding`, `SetGridParams`, `ApplyLayoutChanges`)
- `SVListViewEditor` custom Inspector with runtime stats and quick-action buttons
- Scene-view Gizmos (viewport, content, scroll indicator, buffer zone)
- Detailed Gizmos mode (per-item wireframes, height-status dots, grid lines, stats panel)
- `SVItemRendererBase` convenience class with cached `RectTransform` / `LayoutElement`
- `ListDataSource<T>` built-in data source for `IList<T>`
- Smooth scroll-to-index animation (`SVTweenScroll`)
- Demo scene with 100,000-item ranking list
- Bilingual README (Chinese / English)

### Fixed
- Grid items positioned at X=0 (now use full `GetPosition` Vector2)
- Grid item width was 0px (now uses explicit `GridCellWidth`)
- `SVGridLayout` missing `UpdateParameters` (now implemented)
- `ISVLayout` missing `UpdateParameters(SVConfig)` (now in interface)
- Editor Inspector missing grid config fields (now shows all 7 grid params)
- Gizmos caused 500+ batches in detailed mode (now frame-skipped, wireframe-only, label-throttled)
- 3 demo scripts missing namespace (now `SVList.Demo`)
- Prefab/scene `m_EditorClassIdentifier` stale after namespace migration
- Unused `using System` in `SVVisibleManager`

[1.0.0]: https://github.com/CrimeyMike/SVList-Unity/releases/tag/v1.0.0

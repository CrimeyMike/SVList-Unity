# Changelog

All notable changes to SVList will be documented in this file.

## [1.2.1] - 2026-10-28

### Fixed
- **Chat bubble exceeded the item bounds on panels narrower than the 1080 design width**: the bubble used a hard-coded width anchored to the root's edge. It is now clamped to `min(design width, root width - side margins)` via a responsive `OnRectTransformDimensionsChange` callback — runtime-only, the prefab's static layout stays authoritative (wide panels keep the designed width; narrow panels shrink the bubble to fit)

## [1.2.0] - 2026-10-28

### Added
- **Self-calibrating heights** — automatic, exact height pre-computation with zero user formulas:
  - `SVHeightProbe`: captures a layout fingerprint (font, size, wrap width, rich-text/word-wrap flags, fixed delta) from the first instantiated item of each template; Inspector layout edits are picked up automatically, `OnBind` never touches RectTransform sizes — prefab static layouts are authoritative
  - `SVPersistentHeightCache`: content-addressed height cache (FNV-1a of fingerprint + text, schema-salted) with in-memory LRU (100k) and throttled binary persistence — restarts hit the cache; layout changes invalidate keys naturally
  - `SVAutoHeightProvider`: auto-enabled by `SVConfig.AutoMeasure` (default on) when the data source implements no `ISVItemHeightProvider`; `view.PremeasureHeights(from, to)` warms up a page after loading history
  - Text-member heuristic (case-insensitive preferred names, longest current value wins); fixed-height templates (image messages) captured as constants; TMP integration conditionally compiled (`UNITY_TEXTMESHPRO_PRESENT`), keeping the Runtime TMP-free where TMP is absent
- Chat example reworked Inspector-first: prefab static layouts are correct in preview, `OnBind` only flips the bubble side for self-messages; `MeasureHeight` reads the live `rect.width`
- Image message template (`ImageMessageItem`): second chat template dispatched via `prefabSelector` by data type

### Fixed
- Scrolling jitter when scrolling through not-yet-measured items: exact provider heights are back-filled into the layout cache as soon as each template's fingerprint is ready (with per-template timing and bottom-anchoring)
- Text overflowing its bubble: measurements refuse to run with an unavailable wrap width, fingerprint capture defers to the frame after the canvas layout pass (creation-frame widths are stale), and every measurement reconciles with the provider (provider wins beyond a threshold)
- Text-member heuristic measured the sender name instead of the body (case-sensitive matching missed `Text`; member order is not guaranteed at runtime)
- `ChatListTemplate` broken item prefab reference; hierarchy-menu stale path



## [1.1.0] - 2026-10-26

### Added
- Incremental `InsertItems` / `RemoveItems` with viewport compensation: inserting a page of history at the top no longer resets the view or discards measured heights (previously fell back to a full refresh and jumped to top)
- `ISVItemHeightProvider` height pre-computation channel: the data layer can measure item heights up front (e.g. TMP `GetPreferredValues`), eliminating the estimate→correct jitter; auto-detected when the data source implements the interface, falls back to `EstimateHeight` per item
- Chat-oriented APIs: `IsAtBottom`, `ScrollToBottom`, `ScrollToBottomSmooth`, `AppendItem(s)`, `RegisterItemPrefab`, `GetRenderer`, `MaxScrollOffset`
- `SVItemBase<T>` convenience base class — implement only `OnBind` instead of the 5-method renderer interface
- Setup self-check: actionable errors for missing Viewport / wrong Content anchors / unregistered item templates
- Hierarchy one-click creation menu (`GameObject/UI/SVList`: vertical / chat / horizontal presets); generated Viewport carries a transparent raycast-target Image so the list is draggable out of the box
- Out-of-the-box list templates in `Examples/Templates/` (`VerticalListTemplate` / `HorizontalListTemplate` / `ChatListTemplate` + `SimpleListItem`): fully wired hierarchies with item prefab and minimal driver scripts attached — drag under a Canvas and press Play; regenerable via `Tools → SVList → Rebuild list templates`
- Prefab-based chat demo scene (in-package, `Examples/Scenes/`), in-package Chinese dynamic TMP font asset, and `Tools → SVList → Rebuild Chat Demo Scene` rebuild tool; all asset paths are derived from the script location, so the package works at any install path
- 17 EditMode unit tests (Fenwick tree / height cache / insert-compensation behavior)

### Changed
- Zero-GC hot path: cached provider delegates, pooled `HeightInfo` objects, reused shift-index buffers, and renderer re-measure skipped when the height provider already supplied a height. Measured ~7.3 KB → ~4.6 KB per frame while scrolling; the remaining ~85% of allocation is TMP text assignment itself
- Insert/delete viewport compensation now shifts the scroll position incrementally via `SVScrollController.ShiftScrollOffset` instead of writing `verticalNormalizedPosition`, so it no longer clears `ScrollRect.velocity` (scroll inertia survives a history page load)

### Fixed
- First-layout overlap caused by double `Initialize` (script `Start` + manual call) leaking orphaned items — `Initialize` is now idempotent and `view.Start` is re-entry guarded
- Inactive TMP measure text returning garbage values from `GetPreferredValues` (inactive scene-serialized TMP never runs Awake/OnEnable); the demo keeps its measure text active but off-canvas and fully transparent
- Duplicate `PoolRoot` creation
- `GetRenderer` returning half-initialized renderers

## [1.0.1] - 2025-06-23

### Changed
- Restructured repo to standard UPM package layout: `Runtime/` and `Editor/` moved to repo root
- `package.json` now at repo root alongside `Runtime/`/`Editor/` — valid UPM git package
- Demo project now references SVList as a local package via `Packages/manifest.json`
- **Important**: Demo scene script references need re-linking after upgrade (one-time)

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

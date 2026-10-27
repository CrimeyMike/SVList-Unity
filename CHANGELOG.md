# Changelog

All notable changes to SVList will be documented in this file.

## [1.2.0] - 2026-10-27

### Added
- **Self-calibrating heights** — automatic, exact height pre-computation with zero user formulas:
  - `SVHeightProbe`: captures a layout fingerprint (font, size, wrap width, rich-text/word-wrap flags, fixed delta) from the first instantiated item of each template. Inspector layout edits are picked up automatically — no constants to keep in sync, and no code-side layout (OnBind never touches sizes anymore)
  - `SVPersistentHeightCache`: content-addressed height cache keyed by FNV-1a(layout fingerprint + text), with in-memory LRU (100k entries) and throttled binary persistence under `persistentDataPath/svlist/`. Restarts hit the cache (history reload needs no measuring); layout changes change the fingerprint, so stale entries invalidate themselves
  - `SVAutoHeightProvider`: wires probe + cache into the existing provider channel. Enabled by `SVConfig.AutoMeasure` (default on) whenever the data source implements no `ISVItemHeightProvider` and no manual provider was set — falls back to estimation + post-instantiation correction until the first item of a template has been seen
  - `SVListView.PremeasureHeights(from, to)`: warm up a page of heights after loading history (milliseconds); used by the chat template driver
  - Text-member heuristic for arbitrary data types: string field/property named `text`/`content`/`msg`/`message`/`body`, else the first string member; exotic layouts can still fall back to a hand-written provider
  - Templates without TMP text (e.g. image messages) are captured as fixed-height items automatically
  - TMP integration is conditionally compiled (`UNITY_TEXTMESHPRO_PRESENT` via asmdef versionDefines); projects without TextMeshPro keep a TMP-free Runtime, AutoMeasure degrades to a no-op
- Chat example reworked Inspector-first: prefab static layouts are now authoritative (name row top, bubble left-anchored, correct preview), `OnBind` only flips the bubble side for self-messages and fills text; `MeasureHeight` reads the live `rect.width` so Inspector width edits propagate automatically
- Image message template (`ImageMessageItem`): second chat template (PrefabID 1) selected via `prefabSelector` by data type — demonstrates multi-template dispatch (text vs. image) with the generator wiring both prefabs

### Fixed
- `ChatListTemplate` item prefab reference (wrong root fileID left the Item Prefabs list empty)
- Hierarchy-menu completion log pointed at a stale project path

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

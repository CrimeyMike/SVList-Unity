# Contributing / 贡献指南

Thanks for your interest in improving SVList! This project is maintained in a **low-touch style**: quality over velocity, async over meetings.

感谢你对 SVList 的关注!本仓库采用**低强度维护**:重质量、异步协作。

## Ground rules / 基本约定

- **Bug-fix PRs are welcome.** Keep them small, focused, and covered by an EditMode test under `Assets/SVList/Tests/EditMode/` when feasible.
  **欢迎修 bug 的 PR**:小而聚焦,能补测试就补(`Assets/SVList/Tests/EditMode/`)。
- **Feature PRs are usually not accepted** unless discussed in an issue first. Open an issue with your design before writing code.
  **功能型 PR 一般不接受**,除非先在 issue 里讨论过。先开 issue 说明设计,再动手。
- Response time is not guaranteed — issues are triaged in batches (roughly weekly).
  回复不保证及时,issue 按批处理(大约每周一次)。

## Before you submit / 提交前

1. All EditMode tests green: `Window → General → Test Runner → EditMode → Run All`
   EditMode 测试全绿
2. No new console warnings in the demo scene (`Assets/SVList/Examples/Scenes/SVListChatDemo.unity`)
   演示场景无新增 Console 警告
3. Public API changes need a `CHANGELOG.md` entry
   公共 API 变更需更新 `CHANGELOG.md`

## Project layout / 工程结构

```
Assets/SVList/          the package (Runtime / Editor / Examples / Tests)
Assets/Scripts, Prefabs demo-project assets (not part of the package)
```

## Licensing / 许可

By submitting a PR you agree your contribution is licensed under the repository's MIT license.
提交 PR 即表示你同意贡献以本仓库的 MIT 许可发布。

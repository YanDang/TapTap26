# P2 提交前检查

- 构建与矩阵见validation.md；最终源码资源及Assembly-CSharp.dll SHA256见candidate-sha256.json。该清单不包括文档/自动还原的版本锁文件。
- 新资源meta检查：[]；新增源码危险调用/明显硬编码凭据模式扫描：[]。
- git diff --cached --check仅报Unity原生序列化空字段尾空格（meta/asset/prefab/mat/unity），源码/报告无此问题；不为风格重序列化已验证资源。
- 本轮为人工源码自审+实际构建/Player门禁，没有独立reviewer工具可调用，不冒称独立审核通过。
- ProjectVersion/packages-lock已还原，无其他无关已跟踪重序列化改动；旧P2工作区大证据移至外部archived-worktree保留。
- 进程核对无本工程Unity/Player残留；其他工程integ_ours的CrashHandler保留未触碰。
- 外部integ_b_report.md已更新，但位于git根外，不包含在本仓库commit；仓内两份P2文档保留同等证据。

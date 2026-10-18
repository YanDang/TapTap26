# P4 提交前核查

- 最终候选build-final.log：0编译错误、2警告；normal 178/0，baseline 174/0，同一二进制。旧失败保留，未减少门禁断言。
- 新增行静态扫描：硬编码密码/密钥、os.system/shell=True、pickle、eval/exec未命中。共享类diff已按文件核查。
- 暂存路径已核查：P4源码、资源/meta、测试、报告；无.vsconfig、ProjectVersion、packages-lock、场景或构建/大证据。
- git diff --check并非干净：Unity .meta空字段自带尾空格；源移植UltArcGraphic.cs第54行空白尾空格。保留已验证候选，不为格式改动重写资源/代码；不是编译或运行失败。
- 本子代理环境没有delegate_task工具，未进行独立审查代理复核；不使用[verified]标记，交由主代理最终复核。静态扫描/自审不冒充独立审查。
- 功能通过不等于完整视觉通过；局限详见P4/handoff.md与两报告。全部自有Unity/player退出，未结束integ_ours外部CrashHandler。

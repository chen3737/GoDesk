# GoDesk 开发环境

## 前置条件

- Windows 11 22H2 或更高版本（x64）；
- .NET SDK 10.0.302；
- Git 2.53 或更高版本；
- 普通、非管理员 PowerShell。日常还原、构建和验证不应依赖管理员权限。

可在 PowerShell 中使用 `winget` 安装固定版本：

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --version 10.0.302
winget install --id Git.Git --exact --version 2.53.0
```

安装后重新打开普通 PowerShell，并检查环境：

```powershell
[Environment]::OSVersion.Version
$env:PROCESSOR_ARCHITECTURE
dotnet --version
git --version
```

## 一键验证

在仓库根目录运行：

```powershell
& ./scripts/verify.ps1
```

脚本会以锁定模式还原依赖、执行 Release 构建，并运行核心基础验证程序。脚本自身从 `$PSScriptRoot` 解析仓库根目录，因此也可从其他当前目录通过完整或相对脚本路径调用。

验证代码只能放在 `verification/`，基准代码只能放在 `benchmarks/`。仓库中禁止出现名为 `test` 或 `tests` 的目录，且 `.gitignore` 必须保留精确行 `test/`；一键验证会检查这两项约束。

## 依赖管理

NuGet 包版本集中维护在 `Directory.Packages.props`，项目文件只声明包引用，不分别固定版本。依赖变更后更新锁文件并重新运行完整验证：

```powershell
dotnet restore GoDesk.sln --force-evaluate
& ./scripts/verify.ps1
```

提交锁文件前应检查 `NuGet.config` 与 `dotnet nuget list source` 的来源，核对每个新增或升级包的许可证及再分发条件，并执行安全审计：

```powershell
dotnet list GoDesk.sln package --vulnerable --include-transitive
dotnet list GoDesk.sln package --deprecated --include-transitive
```

许可证信息应以包元数据、上游官方仓库和许可证原文交叉确认；来源、许可或安全状态不明确的依赖不得合入。

## 阶段 1 安全边界与后续集成

以下项目是后续阶段的可执行跟踪项。阶段 1 不修改对应生产 API；标记为“阻塞”的项目必须在指定门槛前完成。

| ID | 目标阶段与阻塞性 | 验收条件 |
| --- | --- | --- |
| `PAIR-SEC-001` | 阶段 2，阻塞配对安全实现 | 配对层先将不可信 SPKI 解析为完整 DER，确认消费全部输入字节并验证公钥是 P-256，之后才调用 `FromSubjectPublicKeyInfo` 计算精确 DER SPKI 的哈希。集成测试固定“解析 → 全字节消费 → P-256 验证 → 哈希”的顺序，并证明尾随数据、错误曲线和畸形输入在哈希前被拒绝。 |
| `PROTO-ERR-001` | 阶段 2，阻塞任何网络入口 | `ProtocolFrameException` 提供稳定的 reason/code 并映射到 `GoDeskErrorCode`；调用方和集成测试依据 code 分支，不依赖英文异常消息。覆盖所有可从网络触发的帧与 Hello 解码失败路径。 |
| `CORE-ERR-001` | 阶段 2，阻塞公共 API 冻结 | `GoDeskException` 拒绝 null `safeMessage` 和非法 enum，补充 `(code, safeMessage, innerException)` 构造方式；验证代码覆盖参数拒绝、属性保留和 `InnerException` 传递。 |
| `OP-EXEC-001` | 阶段 4 终端与阶段 5 文件，阻塞任何有副作用命令 | `BoundedOperationSet` 仅作为易失近期窗口。执行层必须先鉴权、按设备隔离，采用可信的容量与速率限制，并持久化 `pending` / `running` / `completed` / `indeterminate` 状态；重试返回既有状态，不重复执行。集成验证覆盖重复请求、进程重启和执行结果不确定三种场景。 |
| `DIAG-001` | 后续维护，非阻塞诊断改进 | `DevicePermissions` 的参数异常包含正确 `actualValue` 与可操作说明；`DeviceId` 的 canonical A/Q 错误消息一致、可诊断且不泄漏敏感输入。验证代码固定参数名、实际值和诊断语义。 |

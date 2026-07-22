# GoDesk 分阶段实施路线图

> 状态：已确认设计后的实施拆分  
> 更新日期：2026-07-22  
> 主规格：[GoDesk 设计方案](../specs/2026-07-21-godesk-design.md)  
> 终端规格：[Codex MCP 与持久远程终端设计](../specs/2026-07-22-godesk-codex-mcp-terminal-design.md)

## 1. 拆分原则

GoDesk 同时包含设备身份、加密传输、Windows 服务、终端、文件、音视频、输入、安全桌面、UI 和 MSI。它们不能在一个可审查的实施计划中可靠完成，因此按可独立构建、验证和提交的纵向里程碑拆分。

每个阶段都必须：

- 以前一阶段的已验证接口为依赖；
- 产生可以独立运行的验证程序或真实双机演示；
- 不把测试代码放入 `test/` 或 `tests/`；
- 保持 `.gitignore` 包含 `test/`；
- 在进入下一阶段前完成安全和规格一致性复核；
- 使用签名发布包之前，先在无签名开发构建上完成相同功能验收。

## 2. 实施顺序

### 阶段 1：核心基础（已完成）

产物：

- .NET 10 解决方案和统一构建规则；
- 设备权限模型；
- 设备 ID 与 Base32；
- 有界控制帧和 CBOR Hello 消息；
- 操作去重基础类型；
- 统一错误码、输入配额和本地验证入口。

验收证据：`scripts/verify.ps1` 在 Release 配置下完成锁定依赖还原、0 warning 构建和 20 项核心基础验证。

详细计划：`2026-07-22-godesk-core-foundation-plan.md`。

### 阶段 2：设备身份与首次配对

产物：

- Windows CNG ECDSA P-256 不可导出设备密钥；
- TPM 优先和软件 KSP 回退；
- 自签名设备证书及公钥固定；
- 临时加密配对会话；
- 六词安全校验与双端确认状态机；
- SQLite 信任库、权限版本和撤销；
- 本地配对 CLI 双实例演示。

### 阶段 3：安全传输与本地 IPC

产物：

- UDP/TCP `45830` 监听；
- QUIC/TLS 1.3 与 TCP/TLS 1.3 回退；
- TLS 双向认证、证书固定和随机挑战；
- Control、File、Terminal、Video、Audio、Telemetry 流抽象；
- LocalService Transport 主机；
- 按用户 SID 约束的命名管道；
- LAN/Tailscale 双机加密 echo 与断线重连演示。

### 阶段 4：Codex MCP 与持久终端

产物：

- `GoDesk.TerminalHost`；
- Windows ConPTY 和 Job Object；
- PowerShell 5.1、PowerShell 7、CMD 与 Conda 持久状态；
- Terminal 通道、输出序号、操作去重和重连；
- 结构化只读文件工具；
- `GoDesk.McpBridge` STDIO MCP；
- 共享终端的最小开发 UI；
- 普通终端双机演示。

管理员终端的 UAC 流程在阶段 7 与安全桌面一起完成；阶段 4 只保留权限和协议位置，不伪造提升能力。

### 阶段 5：文件管理

产物：

- 授权根目录与句柄复核；
- 目录分页、搜索、上传和下载；
- `4MiB` 分块、SHA-256、位图和 7 天续传；
- 移动、重命名、回收站删除、永久删除和文件执行；
- 拥塞时文件自动降速；
- 双机 10GiB 传输和破坏性虚拟磁盘验证。

### 阶段 6：远程画面与输入

产物：

- DXGI Desktop Duplication；
- D3D11 BGRA 到 NV12；
- Media Foundation H.264 编解码；
- Video 流丢旧帧和 IDR 恢复；
- WinUI 3 远控画布；
- Raw Input、扫描码、坐标映射和 `SendInput`；
- 多显示器枚举与 1 秒内切换；
- LAN 1080p60 基线演示。

### 阶段 7：声音、安全桌面与管理员终端

产物：

- WASAPI loopback 与 Opus；
- 音视频同步和设备切换；
- Broker 窄接口；
- SecureAgent 登录界面和 UAC 原型；
- 安全注意序列；
- 每次 UAC 的管理员 TerminalHost；
- 无 SYSTEM Shell 和不保存凭据的安全验证。

如果目标 Windows 安全策略阻止登录界面捕获，本阶段按主规格明确收缩能力，不以未文档化绕过替代。

### 阶段 8：产品 UI、安装、恢复与发布门槛

产物：

- Windows App SDK 2.2 / WinUI 3 完整 UI；
- 首次启动、配对、主页、文件、终端、活动和设置页面；
- Windows 服务安装与恢复策略；
- SQLite 迁移、日志轮换和诊断包；
- WiX MSI、升级、卸载和防火墙规则；
- Authenticode 签名流程；
- 双机完整功能、安全、性能和 8 小时耐久验收。

## 3. 阶段门槛

不得通过增加阶段间临时后门来加快开发。特别禁止：

- 在设备身份完成前使用固定共享密码作为正式协议；
- 在 TLS 双向认证完成前让 Terminal、File 或 Broker 接受网络消息；
- 让 Transport 以管理员或 SYSTEM 运行；
- 为管理员终端创建无 UAC 的计划任务或服务命令入口；
- 以“开发模式”为名允许路径穿越、关闭摘要校验或自动重放命令；
- 在 B 直接开放 MCP、HTTP Shell 或调试远程终端端口。

## 4. 当前执行入口

阶段 1 已完成；`scripts/verify.ps1` 已提供锁定依赖还原、Release 0 warning 构建和 20 项核心基础验证的可重复证据。

下一执行入口是阶段 2“设备身份与首次配对”。开始配对安全实现、开放网络入口或冻结公共 API 前，必须分别完成 `docs/development.md` 中的 `PAIR-SEC-001`、`PROTO-ERR-001` 和 `CORE-ERR-001`。

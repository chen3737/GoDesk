# GoDesk Codex MCP 与持久远程终端设计

> 状态：设计定稿，已完成用户审阅  
> 设计版本：1.0  
> 更新日期：2026-07-22  
> 关联主规格：[GoDesk 设计方案](./2026-07-21-godesk-design.md)  
> 适用范围：电脑 A 上的 Codex 通过 GoDesk 控制电脑 B 的持久终端并读取文件

## 1. 目标

在不要求电脑 B 安装 Codex、不新增远程 MCP 监听端口、也不绕过 GoDesk 设备认证的前提下，使电脑 A 上的 Codex 能够：

- 创建和控制电脑 B 的持久 PowerShell、PowerShell 7 或 CMD 终端；
- 保留当前目录、环境变量、Shell 状态和 Conda 激活环境；
- 与 GoDesk 内置控制台共享同一个终端；
- 读取、列出和搜索电脑 B 上已授权范围内的文件；
- 在 A 断网、GoDesk UI 退出或 Codex 退出后继续运行 B 上的任务；
- 重新连接同一个终端并恢复未被缓冲淘汰的输出；
- 在 B 本地明确开启后，通过标准 UAC 创建管理员终端。

该功能属于 GoDesk 第一版范围。安全目标是阻止未配对设备、低权限网络服务或未经本地开启的管理员能力获得终端控制权；它不试图限制已经获得 `terminal` 权限的可信设备在 B 当前用户权限内执行什么命令。

## 2. 明确不做

第一版不实现：

- B 上运行 Codex；
- B 向网络直接暴露 MCP 服务；
- localhost HTTP MCP；
- TCP 隧道、SSH 服务或通用端口转发；
- Linux PTY、WSL 终端或容器终端；
- 浏览器终端；
- 静默管理员提权；
- SYSTEM Shell；
- 管理员密码或 `runas /savecred` 凭据保存；
- 独立 Conda MCP 工具或 GoDesk 自行管理 Conda 环境；
- 独立的 MCP 文件写入工具；
- 基于命令文本的允许列表或危险命令解析器。

不实现命令允许列表的原因是 PowerShell 和 CMD 可以通过脚本、编码、子进程、别名和动态表达式表达同一操作。一个看似安全的字符串过滤器不能形成可靠安全边界。

## 3. 已确认的产品决策

- 使用本地 STDIO MCP 桥接方案；
- Codex 运行在 A，B 不需要安装或运行 Codex；
- B 使用真正的持久交互式 ConPTY；
- Codex 和 GoDesk UI 共享同一个终端；
- `terminal` 是设备级权限，不进行逐终端确认；
- `terminal` 按配对完全信任模型默认开启；
- `admin-terminal` 是独立权限并默认关闭；
- 管理员终端每次创建都必须经过标准 UAC；
- 终端在 A 或 Codex 断开后继续运行；
- MCP 提供结构化只读文件工具；
- MCP 不提供独立文件写入工具，写入通过可见终端完成；
- Codex MCP 默认建议使用 `writes` 审批模式。

## 4. 总体架构

```text
电脑 A
┌────────────────────────┐
│ Codex                  │
└───────────┬────────────┘
            │ STDIO MCP
┌───────────▼────────────┐
│ GoDesk.McpBridge       │  普通用户权限、无网络监听
└───────────┬────────────┘
            │ 带 ACL 的本地命名管道
┌───────────▼────────────┐
│ GoDesk.Transport       │  LocalService
└───────────┬────────────┘
            │ GoDesk 双向认证加密连接
════════════╪════════════════════════════════════
电脑 B      │
┌───────────▼────────────┐
│ GoDesk.Transport       │  LocalService
└───────────┬────────────┘
            │ 已认证的本地 IPC
┌───────────▼────────────┐
│ GoDesk.TerminalHost    │  当前用户或经 UAC 的高完整性用户
│ ConPTY + Shell         │
└────────────────────────┘
```

MCP 只存在于 A 的本地进程边界。跨电脑的身份、认证、权限、加密、重连和限速全部复用 GoDesk，不建立第二套远程信任体系。

## 5. 组件职责

### 5.1 GoDesk.McpBridge

`GoDesk.McpBridge.exe`：

- 由 Codex 以 STDIO MCP 方式启动；
- 以 A 当前 Windows 用户身份运行；
- 不监听 TCP、UDP、HTTP 或命名的网络管道；
- 通过受 ACL 保护的本地命名管道调用 A 的 Transport；
- 不直接连接 B；
- 不读取 `state.db`；
- 不持有设备私钥、TLS 密钥或 Tailscale 凭据；
- 将 MCP 工具输入转换为受限 GoDesk 消息；
- 将 GoDesk 错误映射为稳定、可读的 MCP 错误；
- STDIO 关闭时退出，但不结束 B 上的终端。

本地 IPC 校验调用者 SID、Windows 会话和完整性级别。Bridge 与 GoDesk UI 属于同一个本地用户信任域，但 Bridge 不能调用 Broker 的高权限接口。

### 5.2 GoDesk.TerminalHost

`GoDesk.TerminalHost.exe`：

- 是独立于 Agent 的用户会话进程；
- 每个 B 用户会话运行一个普通 TerminalHost；管理员终端使用单独的高完整性 TerminalHost；
- 每个终端创建独立 ConPTY、Shell 和 Job Object；
- 持续读取 ConPTY 输出，避免无人读取时远端程序阻塞；
- 保存终端元数据、输出环形缓冲、输入去重状态和重连状态；
- 与 Transport 的本地 IPC 断开后主动重新注册；
- Agent、A 的 UI、MCP Bridge 或 A 的 Transport 退出时继续运行；
- B 用户注销、系统重启、Shell 退出或用户明确结束时终止相应生命周期；
- 自身崩溃时由 Job Object 清理子进程，不留下不可见孤儿任务。

普通 TerminalHost 使用 B 当前交互用户的中等完整性令牌。管理员 TerminalHost 使用同一管理员用户经 UAC 获得的高完整性令牌，不使用 LocalService、LocalSystem 或 SYSTEM Shell。

### 5.3 GoDesk Shell Adapter

Shell Adapter 负责报告：

- 命令开始和结束；
- 操作 ID；
- 退出码；
- 当前工作目录；
- 当前 Conda 环境；
- Shell 是否正在等待交互输入。

这些状态通过 TerminalHost 与 Shell Adapter 之间的独立本地状态管道传递，不混入 ConPTY 的可见输出。普通终端输出不能伪造结构化完成事件。

状态管道不可用时，终端降级为原始交互模式：`terminal_send` 和 `terminal_read` 继续可用，`terminal_execute` 明确报告不能可靠判断完成状态。

## 6. 权限模型

每个可信设备增加：

- `terminal`：创建、连接、读取和控制该设备的全部普通 GoDesk 终端；
- `admin-terminal`：请求创建管理员终端。

规则：

- `terminal` 在成功配对后默认开启；
- `terminal` 是设备级授权，不进行逐终端确认；
- `admin-terminal` 默认关闭，是配对完全信任模型中的明确例外；
- `admin-terminal` 只能在 B 本地设置中开启或关闭；
- 开启 `admin-terminal` 需要 B 上的 Windows Hello 或管理员 UAC 确认；
- A、Codex、MCP Bridge 和远程协议不能修改自己的权限；
- 开启 `admin-terminal` 只允许发起 UAC，不代表提前批准 UAC；
- 每个管理员终端仍需完成一次真实 UAC；
- 撤销设备后立即拒绝该设备的全部终端输入和新建请求；
- 关闭 `terminal` 后已有任务不被远程强杀，B 本地用户决定保留或终止；
- 关闭 `admin-terminal` 后禁止创建新的管理员终端，已有管理员终端立即脱离远程输入并由 B 本地处理。

启用 `terminal` 等价于授予 B 当前用户级任意代码执行能力。启用并批准管理员终端后，可信设备可能取得整台 B 的管理能力。

## 7. MCP 工具

### 7.1 设备工具

#### `get_remote_device`

返回：

- 设备名称和设备 ID；
- 在线状态；
- B 当前交互用户是否存在；
- 普通终端和管理员终端权限状态；
- 协议和能力版本。

不返回私钥、证书内容、TLS 密钥、Tailscale 令牌或其他敏感网络配置。

### 7.2 终端工具

| 工具 | 主要参数 | 行为 |
|---|---|---|
| `terminal_list` | 状态过滤、分页 | 列出终端 ID、Shell、目录、权限级别、状态和创建时间 |
| `terminal_create` | Shell、初始目录、行列数、是否管理员 | 创建并返回持久终端 |
| `terminal_send` | 终端 ID、文本或控制输入、操作 ID | 发送原始交互输入，不等待命令完成 |
| `terminal_execute` | 终端 ID、完整命令、操作 ID、等待时间 | 在现有 Shell 中执行并尽量返回完成状态和退出码 |
| `terminal_read` | 终端 ID、起始输出序号、等待时间、最大字节数 | 分页读取终端输出 |
| `terminal_resize` | 终端 ID、行、列 | 调整 ConPTY 大小 |
| `terminal_interrupt` | 终端 ID、`ctrl-c` 或 `ctrl-break` | 中断前台任务，不结束 TerminalHost |
| `terminal_close` | 终端 ID、`detach` 或 `terminate-tree` | 脱离终端或结束终端及进程树 |

限制：

- 单次 `terminal_send` 或 `terminal_execute` 输入最多 `256KiB`；
- 单次 `terminal_read` 最多返回 `1MiB`；
- `terminal_read` 单次等待最多 30 秒；
- `terminal_execute` 达到等待上限时返回“仍在运行”，不发送中断；
- Shell 正在执行前台任务时，新的 `terminal_execute` 返回忙碌；需要交互时使用 `terminal_send`；
- `terminal_close` 的 `terminate-tree` 模式始终被标记为破坏性。

### 7.3 结构化只读文件工具

| 工具 | 行为 |
|---|---|
| `file_stat` | 返回类型、大小、时间和 Windows 属性 |
| `file_list` | 分页列出目录 |
| `file_read_text` | 按行或字节范围读取文本 |
| `file_search_name` | 按名称和通配符搜索 |
| `file_search_text` | 在限定目录内搜索文本并返回文件、行号和片段 |

限制：

- 单次文本返回最多 `1MiB`；
- 目录单页最多 `1,000` 项；
- 搜索最多返回 `500` 项；
- 搜索受时间、目录深度和扫描文件数配额约束；
- 二进制文件不作为文本返回；
- 无法可靠识别的编码返回明确错误，不猜测并替换内容；
- 路径使用主规格第 15 节的授权根目录、句柄复核、reparse point、UNC、ADS 和穿越防护；
- MCP 不提供独立文件写入工具。

由于 `terminal` 本身允许当前用户级任意命令，文件工具的根目录限制只约束结构化文件工具，不构成对已授权终端的沙箱。

## 8. Shell 与 Conda

支持：

- Windows PowerShell 5.1；
- 已安装时的 PowerShell 7；
- CMD。

终端创建时选择 Shell 和初始目录。默认加载用户 Shell 配置，使用户现有的 Conda 初始化继续有效。GoDesk 不修改 `.condarc`、PowerShell Profile 或 Conda 环境。

在持久终端中执行：

```powershell
conda activate pytorch
```

后，后续命令继续使用该环境。GoDesk 不把命令转换为 `conda run`，也不在终端外复制环境变量。

Shell Adapter 只报告当前可观察状态，不读取或保存 Conda 令牌、私有仓库凭据或完整环境变量。

## 9. 终端协议

主协议新增 Terminal 逻辑通道：

- QUIC 使用每个终端独立的可靠双向流；
- TCP/TLS 回退在加密连接中复用有序逻辑流；
- 输入、输出、窗口调整和控制事件带单调递增序号；
- 所有请求携带会话 ID、终端 ID、设备权限版本和操作 ID；
- 每个终端绑定 B 设备身份、B 用户 SID、完整性级别、Shell 类型和随机 128 位终端 ID；
- 终端 ID 不能跨设备、用户或权限级别复用。

ConPTY 的 UTF-8 VT 输出用于 GoDesk 控制台渲染。MCP Bridge 从同一输出生成文本视图，删除不影响语义的终端控制序列，并保留换行、可见字符和输出顺序。

终端输出始终是不可信内容：

- MCP Bridge 不把终端输出解析成 MCP 请求、权限指令或命令完成事件；
- MCP 文本视图移除除换行和制表符外的控制字符，并对截断位置作明确标记；
- UI 终端渲染器只实现必要的 VT 显示序列；
- 禁止终端输出写入本地剪贴板、自动打开 URI、下载文件或修改 GoDesk UI 之外的窗口状态；
- VT 控制序列具有长度上限，畸形或超长序列被丢弃并计数。

## 10. 输入排序与共享控制

GoDesk UI 与 Codex 可以同时附加同一终端，但 B 端只有一个串行输入队列：

- `terminal_execute` 的完整命令和回车作为一个原子输入单元；
- 用户已在 UI 输入但尚未回车时，Codex 完整命令暂缓或返回“正在手动输入”；
- B 本地用户输入优先于 A 和 Codex；
- A 的用户输入优先于后台 MCP 写入；
- B 或 A 点击“暂停 Codex”后，新的 MCP 输入立即被拒绝，读取仍可继续；
- 用户在 Codex 命令运行期间手动干预时，结果标记 `interleaved=true`；
- Codex 收到 `interleaved=true` 后必须重新读取终端状态，不能假定退出码只对应自己的输入。

`terminal_send` 用于 REPL、确认提示、交互式程序和原始控制字符，不提供完整命令的原子性保证。UI 必须显示输入来源，便于用户判断当前终端由谁操作。

## 11. 重连与幂等

B 持有终端和进程；A 只持有连接与读取游标。

每个终端保留：

- 最近 `16MiB` 输出；
- 单调递增输出序号；
- 最近操作 ID 的有界去重表；
- Shell 状态和当前前台任务状态。

所有终端合计最多使用 `64MiB` 输出缓冲。缓冲只存在 TerminalHost 内存，不默认落盘。超过上限时丢弃最旧输出，并返回最早可读序号和 `truncated=true`。

规则：

- A 断网、UI 退出、MCP Bridge 退出或 Codex 退出不结束 B 任务；
- 重连方从最后确认的输出序号继续；
- UI 与 MCP 各自维护读取游标，互不消费；
- 相同操作 ID 只执行一次；
- 未确认输入不自动重放；
- 无法确定输入是否已送入 ConPTY 时返回“结果不确定”；
- 客户端先按操作 ID 查询状态，不重新发送命令；
- Transport 重启后 TerminalHost 重新注册；
- Shell 退出后最终状态和输出保留 30 分钟；
- B 用户注销或系统重启结束全部终端。

## 12. 管理员终端

管理员终端流程：

1. B 用户在本地设置中开启 `admin-terminal`；
2. A 或 Codex 请求创建管理员终端；
3. B 通过标准 `runas` 流程进入 UAC；
4. 已授权的 GoDesk 安全桌面能力可以显示和操作该 UAC；
5. UAC 同意后启动已签名的高完整性 TerminalHost；
6. TerminalHost 通过不可写入命令行或环境变量的一次性本地注册句柄向 Transport 注册；
7. UI 使用红色标题和持续状态标记显示管理员终端。

规则：

- 每次创建管理员终端都需要 UAC；
- UAC 拒绝、超时或安全桌面不可用时不创建终端；
- 不缓存密码；
- 不创建持久的最高权限计划任务；
- 不允许普通 TerminalHost 在运行中升级为管理员；
- 普通和管理员终端使用不同 ID、进程、Job Object 和权限检查；
- 管理员 TerminalHost 仍不是 SYSTEM，但管理员命令可能进一步改变系统安全状态，因此被视为整机级高风险能力。

## 13. 终端生命周期与资源限制

每个 B 用户最多：

- 8 个普通终端；
- 1 个管理员终端。

生命周期：

- 活跃 Shell 未退出时持续保留；
- A 断开不改变生命周期；
- B 睡眠或休眠时由 Windows 暂停，唤醒后恢复；
- Shell 执行 `exit` 后进入 30 分钟只读保留期；
- 明确选择 `terminate-tree` 时结束 Job Object 内的完整进程树；
- B 用户注销或系统重启时终止；
- GoDesk 升级前发现活跃终端时，要求取消升级或显式结束全部终端；
- 升级和重启不尝试恢复或重放 Shell 命令。

## 14. 用户界面

### 14.1 控制台

GoDesk 主导航增加“终端”：

```text
B：书房电脑   PowerShell   普通权限   Conda: pytorch
[终端 1 ×] [训练任务] [+ 新建]              [暂停 Codex]

PS C:\Users\name\project> conda activate pytorch
(pytorch) PS C:\Users\name\project> python train.py
Epoch 18/100 ...

Codex：正在读取输出
```

界面提供：

- 多终端标签；
- Shell 选择；
- 普通或管理员状态；
- 当前目录和 Conda 环境；
- Codex 连接和暂停状态；
- 重连及输出截断提示；
- `Ctrl+C` 和 `Ctrl+Break`；
- “仅脱离”和“结束进程树”两个不同操作；
- 用户输入和 Codex 输入来源标识。

A 和 B 可以查看同一终端。B 本地输入优先。管理员终端始终使用红色标题，不允许通过主题关闭风险颜色以外的文字和图标提示。

### 14.2 B 本地状态

B 托盘和状态页持续显示：

- 普通终端和管理员终端数量；
- 是否有 Codex 连接；
- 是否暂停 Codex 输入；
- 管理员终端高风险状态；
- “暂停 Codex 输入”和“结束全部终端”入口。

远端不能隐藏该状态。

## 15. MCP 审批与工具元数据

GoDesk 的设备权限和 Codex 的 MCP 审批是独立层：

- GoDesk `terminal` 权限决定该设备能否访问终端；
- Codex 审批决定模型发起某次 MCP 工具调用时是否需要用户批准。

工具元数据必须如实表达：

- `get_remote_device`、`terminal_list`、`terminal_read` 和结构化文件工具为只读；
- `terminal_create`、`terminal_send`、`terminal_execute`、`terminal_resize` 和 `terminal_interrupt` 有副作用；
- `terminal_close` 因包含 `terminate-tree` 而标记为可能破坏性。

推荐 Codex 配置：

```toml
[mcp_servers.godesk]
command = "C:\\Program Files\\GoDesk\\GoDesk.McpBridge.exe"
args = ["--stdio"]
default_tools_approval_mode = "writes"
startup_timeout_sec = 10
tool_timeout_sec = 60
enabled = true
```

该默认值让只读文件和状态查询自动执行，终端写入由 Codex 界面请求批准。GoDesk 不再为同一命令弹出第二层确认。用户可以主动修改自己的 Codex MCP 审批配置，但 GoDesk 不静默放宽该配置。

## 16. 审计和敏感信息

默认审计保存：

- 设备和终端 ID；
- 普通或管理员权限级别；
- Shell 类型；
- 操作来源是用户还是 Codex；
- 操作 ID；
- 时间、输入字节数、完成状态和退出码；
- 完整命令输入的 SHA-256。

默认不持久化：

- 完整命令文本；
- 原始终端输出；
- 交互输入；
- 环境变量；
- Conda 配置、令牌或私有仓库凭据；
- 终端内存缓冲。

诊断包不得包含终端缓冲、完整命令或 Shell 环境。审计记录可以判断操作是否发生和是否重复，但不能用于恢复用户输入的秘密。

## 17. 异常和降级行为

- B 没有已登录用户：普通终端返回“等待用户登录”，不退回 SYSTEM；
- B 锁屏：已有用户终端和任务继续运行；
- Conda 不存在或未初始化：返回真实 Shell 错误，不自动下载或修改配置；
- Shell Adapter 失败：保留原始交互终端，禁用结构化完成判断；
- TerminalHost 崩溃：Job Object 清理进程树，不自动重放或重建终端；
- Transport 重启：TerminalHost 重新注册，任务继续；
- 输出洪泛：持续排空 ConPTY，淘汰最旧缓冲，不阻塞任务；
- MCP 工具等待超时：报告任务仍在运行，不发送中断；
- Shell 等待输入：报告交互状态，由 `terminal_send` 输入；
- MCP Bridge 关闭：只结束 Bridge；
- 权限关闭或设备撤销：立即拒绝新输入，不重放未确认输入；
- B 睡眠或休眠：唤醒后恢复原终端；
- Shell 主动退出：保留最终输出和状态 30 分钟；
- 管理员 UAC 失败：不降级为其他高权限执行方式。

所有降级遵循“可以减少功能，不能降低认证、权限、UAC 或输入幂等要求”。

## 18. 安装与更新

MSI 增加：

```text
Program Files\GoDesk\
  GoDesk.McpBridge.exe
  GoDesk.TerminalHost.exe
  GoDesk.ShellAdapter.dll
```

要求：

- 所有新增二进制使用 Authenticode 签名；
- 普通用户不能修改安装目录；
- MCP Bridge 安装后不自动写入全局 Codex 配置；
- GoDesk UI 提供复制配置片段和显式“配置 Codex MCP”操作；
- 修改 Codex 配置前显示目标路径和变更内容；
- 卸载时只删除名称、命令路径和安装时记录指纹都匹配的 GoDesk MCP 配置块；用户修改过的配置只提示手动清理，不自动覆盖；
- 升级前检查活跃终端；
- 协议升级至少兼容前一个正式版本；
- 不兼容时拒绝终端会话，不回退到未认证或未授权命令通道。

## 19. 验证代码位置

项目不创建或使用 `test/`、`tests/` 目录。`.gitignore` 必须持续包含：

```gitignore
test/
```

新增验证代码放置于：

```text
verification/
  mcp/
  terminal/

benchmarks/
  terminal/
```

破坏性终端验证只能在一次性 Windows 虚拟机、可恢复快照或明确创建的临时用户中运行，不得对日常用户目录、真实凭据或日常 Conda 环境执行删除和管理员破坏场景。

## 20. 验证矩阵

### 20.1 MCP

验证：

- STDIO 初始化、工具发现、调用和退出；
- 工具输入模式、大小限制和错误映射；
- 只读、有副作用和破坏性工具元数据；
- Codex 重启后重新连接；
- GoDesk 未启动、B 离线、权限关闭和协议不兼容；
- Bridge 不能读取设备私钥、`state.db` 或其他用户 IPC；
- 非当前 A 用户不能连接本地管道；
- 畸形 MCP 请求不能造成越权、无限内存分配或进程崩溃。

### 20.2 持久终端

PowerShell 5.1、PowerShell 7 和 CMD 分别验证：

- `cd` 后续保持；
- 环境变量持续存在；
- `conda activate` 后后续 Python 来自对应环境；
- 退出码和当前目录正确；
- 中文路径、UTF-8、长命令、多行命令和 ANSI 输出；
- OSC 52、自动打开 URI、畸形 VT 和超长控制序列不会影响 A 的剪贴板或触发外部动作；
- Python REPL 等交互程序；
- `Ctrl+C`、`Ctrl+Break` 和窗口调整；
- 用户和 Codex 输入不拼接成意外命令；
- B 本地输入优先；
- “暂停 Codex”立即阻止新输入。

### 20.3 重连和耐久

- 运行至少 30 分钟的 Conda 任务；
- 期间关闭 Codex、A 的 UI、断网并重启 A 端 Transport；
- B 任务不得结束；
- 恢复后连接相同终端 ID；
- 从最后输出序号继续；
- 缓冲淘汰明确报告截断；
- 相同操作 ID 不重复执行；
- 结果不确定操作不自动重放；
- 连续 100 次断开和重连；
- 同时运行 8 个普通终端和 1 个管理员终端；
- B 用户注销后没有孤儿进程。

### 20.4 文件工具

- UTF-8、UTF-16、BOM 和无法识别编码；
- 空文件、大文件和超长行；
- 精确行范围和分页；
- 100,000 项目录；
- 搜索超时和结果上限；
- 二进制文件拒绝文本读取；
- `..`、UNC、ADS、设备路径、符号链接、目录联接和检查后替换；
- 无权限、文件占用、读取中删除和读取中修改；
- 结构化工具不能逃出授权根目录。

### 20.5 管理员终端

- `admin-terminal` 初始关闭；
- A 和 Codex 不能修改开关；
- 每次创建都产生真实 UAC；
- 拒绝 UAC 后不创建终端；
- 普通终端为中等完整性；
- 管理员终端为当前管理员用户高完整性；
- 不产生 SYSTEM Shell；
- 不缓存密码或创建 `runas /savecred`；
- 关闭权限后不能新建管理员终端；
- 红色状态、本地提示和审计完整。

## 21. 性能目标

| 指标 | 目标 |
|---|---:|
| LAN 终端输入到 B 写入 ConPTY | p95 ≤ 50ms |
| LAN 输出到 A 控制台显示 | p95 ≤ 100ms |
| MCP 读取已有缓冲 | p95 ≤ 100ms |
| LAN 读取 1MiB 文本文件 | p95 ≤ 2 秒 |
| 空闲 TerminalHost 自身工作集，不含 Shell 子进程 | ≤ 50MiB |
| 8 个普通终端的 TerminalHost 自身开销，不含 Shell 和 Conda 进程 | ≤ 150MiB |
| 全部终端输出缓冲 | ≤ 64MiB |
| 连续输出 10GiB | 无内存线性增长、无死锁 |
| 连续运行 8 小时 | 无句柄、线程或工作集持续增长 |

## 22. 第一版验收

第一版必须完成：

1. A 上 Codex 通过本地 STDIO MCP 创建 B 的 PowerShell 终端；
2. Codex 切换目录并激活 B 已有 Conda 环境；
3. Codex 通过结构化工具读取项目文件并启动长时间任务；
4. A 和 B 的 GoDesk 控制台可以显示同一个终端；
5. 用户可以手动输入、暂停 Codex 并恢复；
6. A 断网后 B 任务继续，恢复后重新连接；
7. 普通终端不能静默提升；
8. B 本地开启管理员权限后，通过 UAC 创建管理员终端；
9. 撤销设备或关闭终端权限后，Codex 立即失去输入能力；
10. 全程没有新增远程 MCP 端口或绕过 GoDesk 认证的路径。

以下任一情况阻断发布：

- 未配对设备能够进入 Terminal 协议；
- 命令在断线、重连、服务重启或升级后自动重复执行；
- 普通终端获得高完整性或 SYSTEM；
- 管理员终端绕过 UAC；
- MCP Bridge 能读取 GoDesk 私钥或直接调用 Broker；
- 结构化文件工具逃出授权根目录；
- 终端输出能伪造结构化完成状态；
- 暂停、关闭权限或撤销设备后仍可发送输入；
- 输出洪泛造成任务死锁或进程内存随总输出线性增长；
- 卸载或升级在没有明确确认时杀死活跃任务。

## 23. 技术依据

- [Codex Model Context Protocol](https://learn.chatgpt.com/docs/extend/mcp)；
- [Codex approvals and security](https://learn.chatgpt.com/docs/agent-approvals-security)；
- [Windows Pseudoconsole](https://learn.microsoft.com/windows/console/creating-a-pseudoconsole-session)；
- [CreatePseudoConsole](https://learn.microsoft.com/windows/console/createpseudoconsole)；
- [Windows Job Objects](https://learn.microsoft.com/windows/win32/procthread/job-objects)；
- [Conda environment management](https://docs.conda.io/projects/conda/en/latest/user-guide/tasks/manage-environments.html)。

进入实现阶段时应重新核对 Codex MCP 配置字段、Windows ConPTY 行为和目标 Conda 版本的当前支持状态。

## 24. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-07-22 | 建立 Codex MCP、持久 ConPTY、结构化文件读取、管理员终端、重连、安全和验收设计 |
| 2026-07-22 | 用户完成最终审阅，扩展规格正式定稿 |

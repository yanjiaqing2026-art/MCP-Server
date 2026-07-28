# Claude Code · AVEVA E3D 3.1 外部驱动建模 全量指导书（唯一权威方法指南）

> **强制**：任何涉及 E3D 命令 / PML / 元素创建 / 属性写入 / 定位 / 连接 / 事务 / MCP 写工具的改动，
> **必须先完整读本文**。这是"怎么让 agent 的命令经 MCP 到 E3D 真执行成功"的权威方法。
> 证据层（不复制成第二套真相，只引用）：
> - 官方逐字命令：[E3D命令手册-官方逐字版.md](E3D命令手册-官方逐字版.md)（help.aveva.com DRMPGC/DBRM/QRM 铁路图）
> - 机读命令数据：[data/e3d-commands.json](data/e3d-commands.json)
> - 置信度速查：[E3D-PML命令参考-权威版.md](E3D-PML命令参考-权威版.md)
> - 09 侧诊断/验证：[../../09-配管智能体/docs/E3D命令-通病诊断与模拟验证-2026-07-03.md](../../09-配管智能体/docs/E3D命令-通病诊断与模拟验证-2026-07-03.md)

## 0. Claude Code 铁律（改 E3D 建模功能前必守）

1. **绝不凭工具名 / 直觉猜命令语法**。命令形态只从官方逐字手册 / e3d-commands.json / 本机 .NET API 取；查不到 → 停、搜、上真机 `$Q`，**不自行拼写**。
2. **区分三种状态**：research（查官方）/ implementation（写代码）/ target-version-verification（真机 E3D 3.1 验）。未经真机验证的命令模板**不得宣称"可用"**，只能标 ⚠。
3. **保留脏工作树**：只改与任务相关的文件，不碰无关的已改动。
4. **不擅自 commit / push / SAVEWORK**。SAVEWORK 由用户或显式步骤触发，绝不在命令序列前自动加一个 SAVEWORK。

## 1. 四层命令语言模型（别混）

| 层 | 管什么 | 例 |
|---|---|---|
| **PML language** | 变量 / 表达式 / 控制流 / 宏 | `!!CE`、`VAR !x`、`DO … ENDDO`、`IF … ENDIF` |
| **DBRM 命令语言** | CE 导航 / 属性 / 层级 / 事务 | `NEW`、`DELETE <type>`、`INCLUDE`、`CLAIM`、`UNDODB` |
| **DRMPGC Design 命令** | 管件选型 / 定位 / 定向 / 连接 | `SELECT WITH`、`POSITION … THROUGH`、`CONNECT`、`ROTATE` |
| **.NET API（typed）** | 进程内数据库/几何强类型操作 | `DbElement.CreateLast(type)`、`SetAttribute`、`Delete()`、`GetActualType()` |

## 2. 上下文规则（命令都作用于"当前"）

**CE（Current Element）是几乎所有改模命令的隐含对象**。定位 / 编辑 / 连接类动词（ROTATE/FLIP/CONNECT/POSITION/DISTANCE/THROUGH/DELETE/INCLUDE…）**作用于 CE，不接前导元素名**。
正确范式：**先把 CE 导航到目标（`!!CE = /name` 或 .NET `CurrentElement.Element = el`），再发裸命令**。
其它上下文：Owner、Current List Position、module（DESI/…）、MDB、可写 DB、claim 模式、工作单位、`gid`、DBREF、Position/Direction/Orientation。

### 2.1 元素创建的**先后依赖**（命令顺序 = 建模最大坑，DABACON 铁律）

> 来源：官方 PML 教程 `04-PML 语言/5. Error Handling & Branching`（唯一直接讲命令顺序的一章；其余章是语言语法，与建模顺序无关）。

DABACON 层级强制"父在子先、类型对位"——**顺序不对直接被数据库拒回**，报确定的模块/错误码：

| 场景 | 命令 | 结果 |
|---|---|---|
| 空 Site 下直接建管 | `NEW PIPE` | ❌ 报错 **(41,8)** "Need to be at a ZONE or below"——PIPE 必须建在 ZONE（或以下）里 |
| 先建 ZONE 再建管 | `NEW ZONE` → `NEW PIPE /MyPipe` | ✅ 成功 |
| 重复用同名 | `NEW PIPE /MyPipe`（已存在） | ❌ 报错 **(41,12)** "That name has already been used"（名字须唯一） |

**正确建模序列（铁律）**：`SITE → ZONE → PIPE → BRAN → 组件`，每一层都要**先把 CE 导到父级、命名唯一、再建子级**。跳级建（如 Site 下直接建 PIPE / 未导航就发裸命令）= 被拒或"不报错也没建出来"。

**错误码可用于自愈**：PML 的 `HANDLE (模块,错误号) / ELSEHANDLE ANY / ELSEHANDLE NONE` 能捕获顺序类错误就地补救——例：`NEW PIPE` 撞 (41,8) → 在 HANDLE 里先 `NEW ZONE` 再重试。09 侧 lint（`ce-verb-element-name` / `move-copy-to-element`）+ 17 侧 `CreateElement` 的 owner 校验都是这条铁律的"发送前 / 执行前"落地；若真机要更强兜底，可让插件识别 (41,8)/(41,12) 回执做结构化建议。

### 2.2 属性/参数的**读取与内省**（PML 教程代码级证据）

> 来源：`04-PML 语言` 第 2/6 章 PML 代码逐段实证。**关键澄清**：这套教程演示的是**读属性 + 内省**（发现某元素有哪些属性、某属性的合法值/单位），**不是 `SET ODIA 100` 式的写属性**——全教程无一处写属性命令。写属性走 `SET`/`PSPEC`/`SELECT WITH`/`CONNECT`（本指导书 §3 + 官方逐字手册），别把"读/内省"当"写"。

**① 内省 = agent「查该元素要填哪些属性 + 合法值」的官方手段**（正是 09 侧 `e3dTypeAttributes`/`e3dPeerAttributes`/`e3dSpecList` 想做的事，PML 给了真语法）：

| PML 表达式 | 作用 | 对应 09/17 能力 |
|---|---|---|
| `!ATT = !!CE.ATTRIBUTES()` | 列出**当前元素全部属性名**（数组，含 Spref/Ispec/Tspec/Angle/Radius/Cref/Ptno/Nwelds… + `:` 开头 UDA） | e3dPeerAttributes「读同类真实属性」 |
| `object ATTRIBUTE('SPREF').METHODS()` | 属性元对象方法：`VALIDVALUES` / `LIMITS` / `UNITS` / `ELEMENTTYPES` / `DEFAULTVALUE(type)` | **枚举合法值的正解** |
| `!X = POSITION` / `!X = ORIENTATION` | 裸关键字取**当前 CE** 的位置/朝向到变量（先选中元素成为 CE，再取） | e3dBridge 读属性 |
| `!x.METHODS()` / `!x.objecttype()` | 列对象方法 / 运行时类型探测 | — |

> **⚠ 17 插件增强机会**：当前 `SpecList` 用 .NET `DBElementCollection` best-effort 扫设计世界（规格在独立 spec 库时扫不到）。PML 的 `object ATTRIBUTE('SPREF').VALIDVALUES` 是**取某属性合法取值集**的官方途径——真机核实后，用它（或 `.LIMITS`/`.UNITS`）经 `ReadPmlViaGlobal` 捕获，可能比 DBElementCollection 更准地枚举可选管等级/合法值。待真机验证 `.VALIDVALUES` 对 SPREF 的返回形态。

**② 位置/朝向的单位与形态**（写坐标时按此，别搞错单位）：
- `POSITION`：`E 1825mm N -10030mm U 3600mm WRT /参考元素`——单位 **mm**、可为负、`WRT` 指参考系、`ORIGIN` 是 DBREF。子属性 `EAST/NORTH/UP/ORIGIN`。
- `ORIENTATION`：`ALPHA/BETA/GAMMA`（欧拉角，**无单位度数**，如 180/0/-90）+ `ORIGIN`；文本形态 `Y is W ... and Z is D`。

**③ 采集/枚举语法**（列元素的官方形，呼应 §3「列元素」行）：
- PML1：`var !x COLLECT all <type> with <where-expr> for <from> /Reference`（`with matchwild(name, |*AREA03*|)` 过滤，`for ce` 限当前元素域，`|...|` 是原义串定界）。
- PML2：`object COLLECTION()` → `.types(...)` → `.scope(!!ce)` → `.filter(object EXPRESSION(...))` → `.results()`（建集合→定类型→定范围→过滤→取结果，顺序固定）。

**总结**：教程的 E3D 价值集中在 **CE 上下文 + 内省/读属性 + 采集** 三块（本 §2/§2.1/§2.2 已提炼）；**建模写命令的顺序/参数以本指导书 §3 矩阵 + 官方逐字手册为准**，教程不覆盖写。

## 3. 正确 / 错误 命令对照矩阵（本仓已按此落地）

| 操作 | ❌ 旧伪范式（真 E3D 拒） | ✅ 官方正解 | 依据 |
|---|---|---|---|
| 建元素 | `NEW BRAN B, OWNER /X` | `!!CE = /X` → `NEW BRAN /B`（CE 落 owner 再 NEW） | DBRM |
| 建管件 | `New Elbo **CHOOSE** With Stype LR` | `New Elbo **SELECT WITH STYPE** LR`（**CHOOSE 弹 GUI 会卡无人值守**；SELECT 是文本非交互形） | DRMPGC24.09.13 |
| 设管等级 | `SET SPREF /X` | `!!CE = /pipe` → `PSPEC /X`（Pipe 级级联；组件级用 SPREF/SELECT 语义） | DRMPGC24.09.03 |
| 旋转 | `ROTATE /X ABOUT E BY 45` | `!!CE = /X` → `ROTATE ABOUT E BY 45`（无前导名，axis=bdir U/N/E） | DRMPGC23.08.13 |
| 反转 | `REVERSE /X` | `!!CE = /X` → `FLIP` | DRMPCM24.09.25 |
| 连接 | `CONNECT /A TO /B` | `!!CE = /A` → `CONN PH TO /B`（作用于 CE 的 p-point PH/PT；**先设分支 PSPE**） | DRMPGC24.09.04 |
| 定位·穿点 | `/X THR /T` | `!!CE = /X` → `POSITION PH THROUGH <bpos>`（marker 必填，PH/PT） | DRMPGC(POSition) |
| 定位·距离 | `/X DIST 100` | `!!CE = /X` → `DISTANCE 100`（marker 可选） | DRMPGC(DISTANCE) |
| 拷属性 | `COPYA /S /T attrs` | `!!CE = /T` → `COPY ATTRIBUTES OF /S`（拷全部属性入 CE） | DRMPGC(COPY) |
| 拷元素 | `COPY /S TO /T` | `COPY /S [RENAME /S /T]`（拷进当前 CE，无 "TO"） | DRMPGC(COPY) |
| 删元素 | `/X DELETE` | 结构化 `e3d_element_delete`（typed .NET `el.Delete()`）；PML 手写则 `!!CE=/X` → `DELETE <type>` | DBRM4.05.4 |
| 层级搬迁 | `MOVE /X TO /Y` | `!!CE = /Y` → `INCLUDE /X`（归属转到 CE；官方 MOVE 只移头/尾点） | DBRM4.05.5 |
| 撤销 | `UNDO` | `UNDODB`（重做 `REDODB`、边界 `MARKDB 'c'`） | DBRM7.08.1 |
| 释放 claim | `RELEASE /X` | `UNCLAIM /X`（顶层无独立 RELEASE，RELEASE 仅 `EXTRACT RELEASE`） | DBRM6.07.2 |
| 列元素 | `EXTRACT ALL PIPE` | `COLLECT` / .NET 检索（EXTRACT 语义=多写库提取，非查询） | DBRM |
| 切管/合并 | `CUT` / `SPLIT` / `GAP` / `JOIN` | 官方裁决**无此命令**（黑名单拦回）；切管走"插组件+新 BRAN"，间隙用 `CLEARANCE` | 附录 |
| 排序 DB 成员 | `/arr.SORT()` | 官方**无此命令**（`.SORT()` 是 PML 数组方法，不适用元素路径） | — |

## 4. 双后端策略（本仓实现）

- **typed .NET 为主**：导航（`GetElement`/`CurrentElement`）、校验、普通删除（`el.Delete()`）、读写属性（`SetAttribute`）。
- **PML 命令模板**限用于：`PSPEC`、`SELECT WITH`（管件选型）、`CONNECT`/`FCONNECT`、受约束定位、专用 Design 语义。
- **⚠ 例外（真机教训）**：**建管件绝不用 .NET `CreateLast`**——它建出**无几何空壳**（没从管等级选型），必须走 PML `New <comp> SELECT WITH …`。

## 5. 当前文件 remediation map（本轮已改）

| 文件 | 改了什么 |
|---|---|
| `Tools/E3dApiWrapper.cs` | `NavigateThenWrite`/`ToBdir` helper；InsertComponent CHOOSE→SELECT WITH；MoveElement→INCLUDE；DeleteElement→typed .NET `Delete()`；Rotate/Reverse/Flip/Connect/ForceConnect/PositionThr/PositionDist/CopyElement/CopyAttributes/ClearAttribute 全 CE-导航；ReleaseElement→UNCLAIM；DbUndo→UNDODB；MoveElement 曾误拦已恢复；ArraySort 诚实拦回。**2026-07-04 新增 `SpecList(max)`**：规格枚举走 §3「列元素 = .NET 检索」——复用已验证的 Search 检索范式（`ResolveType("SPEC")` + `TypeFilter` + `DBElementCollection` 迭代取 NAME），不发 PML、不猜语法。⚠ best-effort 扫设计世界 `/*`；规格若在独立 catalogue/spec 库、扫到 0 条 → **诚实告知需真机核实 spec 世界根路径**，绝不谎报"无规格"（真机确认根后在此加第二 root 即可） |
| `Tools/E3dToolDefinitions.cs` | `e3d_pml_eval` 描述更正（它不是命令 dry-run）；**新增 `e3d_spec_list` 工具定义**（枚举项目管等级供「选管等级」，治 09 侧"规格 AI 瞎猜"） |
| `Tools/E3DToolHandler.cs` / `E3dCommandReference.cs` | CUT/GAP/JOIN 黑名单双层拦回（保留）；**新增 `e3d_spec_list` 分发 + `HandleSpecList`** |
| `09/src/agent/builtin/e3dSpecList.tool.ts` | **首选调 `e3d_spec_list` 真枚举**，老插件无此工具/诚实空结果时自动回退 `e3d_spec_query` 诚实降级路径（绝不编造等级名） |
| `09/src/agent/builtin/e3dPmlBatch.ts` | batch `e3d_connect` → CE 导航 + `CONN PH TO`（旧 `CONNECT a TO b` 被 lint 拦） |
| `09/src/agent/pml/pmlLinter.ts` | 加 `ce-verb-element-name` / `move-copy-to-element` 拦通病（发送前静态） |
| `09/src/agent/systemPrompt/sections.ts` | 建模铁律 + 最常用速查：SELECT WITH / INCLUDE / DELETE<type> / UNDODB / UNCLAIM / CE-导航；类型码 ELBO/EQUI |

## 6. 未知命令协议（SOP）

命令不确定时：**停止猜** → 查官方逐字手册 + e3d-commands.json → 真机 `$Q`/`Q SYNTAX` 探 3.1 语法 → 在**可丢弃 DB**上建最小测试 → 抓**逐字命令 + 真实错误 + 上下文** → 只有拿到证据才更新置信度状态。

置信度状态：`official-verbatim` / `official-concept` / `local-api-confirmed` / `target-version-verified` / `community` / `not-found` / `rejected-on-target`。

## 7. 模拟验证 E3D 命令（4 办法，本仓已落 ①③）

1. **离线 PML lint（发送前，无需 E3D）**：`pmlLinter` 拦 CE-动词带前导名 / MOVE·COPY…TO / NEW…OWNER / 读查询混入 exec。只覆盖 `e3d_pml_exec` 通道。
2. **e3d_pml_eval**：只求值表达式、**不是**建模命令 dry-run（描述已更正）。
3. **真机 roundtrip harness（决定性）**：`09/test/verify-e3d-command-roundtrip.ts`——每条命令 A(旧)/B(候选) 对照真机接受度。跑法 `$env:PC_E3D_ELEM="/scratch"; npx tsx …`，从不 SAVEWORK，跑完 Abandon。
4. **`$Q` 语法帮助**：真机查 3.1 命令语法树（人工，权威裁判）。

## 8. L0-L3 真机验收（在 E3D 3.1 上）

- **L0 层级**：ZONE→PIPE→BRAN 建出、CE 正确流转、`DELETE <type>` 删 CE、`INCLUDE` 改归属。
- **L1 spec**：`PSPEC` 级联、`SELECT WITH STYPE` 选出**带几何**管件（不是空壳）。
- **L2 定位/连接**：`POSITION … THROUGH`、`DISTANCE`、`CONN PH/PT TO`、`FCONNECT`、`ROTATE`/`FLIP`。
- **L3 事务**：`CLAIM`/`UNCLAIM`、`UNDODB`/`REDODB`、`SAVEWORK`；以及**故意发非法命令**确认被诚实拒回（不假成功）。

命令片段一律标 `illustrative` / `official-verbatim` / `target-version-verified`；**无真机证据者不得标 production-ready**。

## 9. 部署与生效

改插件 C# → `dotnet build -c Release`（产物 `bin\Release\net472\MCPServer.dll`）→ 部署到 E3D 机（`deploy-to-e3d.ps1` 或覆盖 `<E3D>\E3DAddins\MCPServer\MCPServer.dll`）→ **关闭再重开 E3D**。改 09 侧 TS → 重载/重启 app。

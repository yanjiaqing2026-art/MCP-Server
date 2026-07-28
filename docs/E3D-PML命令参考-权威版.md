# AVEVA E3D / PDMS PML 命令参考（权威版 · 按置信度分级）

> 📖 **完整逐字大全见 [E3D命令手册-官方逐字版.md](E3D命令手册-官方逐字版.md)**（从 help.aveva.com 官方
> DRMPGC/DBRM/QRM/SOFTCG/PDUV 手册逐字抓取，含铁路图语法 + 15 章 + 黑名单 + 附录）。
> 机读数据集 [data/e3d-commands.json](data/e3d-commands.json)（供 11-KB 检索 + 09 引用）。
> 本文件是**工具置信度速查**（DIST/CUT 等可疑命令的禁用裁决），两者互补、口径一致。
>
> ## ⚠ 口径优先级（2026-07-25 加 —— 本文件曾与指导书打架）
>
> **建模命令形态以 [Claude-Code-E3D建模全量指导书.md](Claude-Code-E3D建模全量指导书.md) §3 对照矩阵为准**，
> 本文件是"按置信度分级的速查"，两者冲突时**听指导书的**。
>
> 起因：指导书 §3（2026-07-04）已把建管件改成 `New Elbo SELECT WITH STYPE LR`（CHOOSE 会卡无人值守 GUI），
> 09 侧 [systemPrompt/sections.ts] 也跟了；但本文件（2026-07-03）第一节仍把 `New Elbo Choose Default` /
> `Choose All` / `Choose With` 标成 ✅已确证可用 **且置信度最高** —— 于是 kbSearch 检索到本文件的 agent
> 会拿到**旧口径**，而这正是"命令瞎猜"的一个真实供给源：不是模型在猜，是**真相源自己有两个版本**。
> 已在下表就地更正。本文件末尾"维护约定"要求的三处同口径（本文件 / `E3dToolDefinitions.cs` /
> `assembler.ts`），**改任一处都要回来对一遍**。
>
> **用途**：插件（`17-E3D插件`）建模路径 + agent 系统 prompt（建模铁律）**唯一引用的命令真相源**。
> 每条命令带**置信度 + 出处**，绝不把"未验证"当"可用"写进工具承诺。
>
> **单一裁判**：真机 E3D（:8286 插件）+ `$Q` 语法帮助。本文件是"发给真机前的最佳已知"，
> 不是"已在你这台真机验过"。凡标 ⚠/【未验证】的，**真机日志闭环逐条裁决后再回填此表**。
>
> **来源标记**：
> - 【官方】= help.aveva.com 手册原文（E3D 1.1 SOFTCG，PML 语言基础跨版本极稳定，附 URL）
> - 【真机·代码】= `E3dApiWrapper.cs` 注释中标注"依据培训资料/真机日志闭环"的结论（真机证据）
> - 【社区】= 公开命令清单（thepiping.com E3D/PDMS 12.1、makepipingeasy 分类表、me-hungry 博客真实代码）
> - 【未验证】= 任何来源都没查到 → **绝不猜，绝不写进工具承诺**
>
> 溯源底稿：[PML命令语法核查报告.md](../PML命令语法核查报告.md)（11 问逐条）+
> [compass_artifact...md](../compass_artifact_wf-60a7dc0d-b873-4d32-888a-ef1b6dc6e7a8_text_markdown.md)（官方 3.1 手册 DRMPGC/PDUV/SOFTCG 公开在线）。

---

## 一、✅ 已确证可用（有出处，真机闭环存活 / 官方原文）

### 建模层级（CE 驱动）
| 命令 | 逐字形态 | 出处 | 备注 |
|---|---|---|---|
| 建管 | `New Pipe /名字` | 【真机·代码】【社区】 | CE 先在 ZONE；建完 CE 推进到新 PIPE |
| 建分支 | `New Branch /名字` | 【真机·代码】 | CE 先在 PIPE；自动继承管等级 |
| 建管件（选型） | `New Elbo **Select With** Stype LR` / `New Tee **Select With** Pbor0 100 Pbor3 80`；无 STYPE 时 `Choose Default`（非交互默认选择） | 【官方】DRMPGC24.09.13 + 指导书 §3 | CE 在 BRANCH；从当前管等级选实件。**⚠ 2026-07-25 更正：不用 `Choose With` / `Choose All`** —— CHOOSE 只在 DEV GRAPHICS 模式弹图形选型器，MCP 里插件是**无人值守执行、会卡住等 GUI**；文本非交互形是 `SELECT WITH`。旧写法见下方勘误 |
| 建站/区 | `New Site /X` → `New Zone /X` → `New Pipe /X` | 【社区】 | 层级链，逐级 CE 推进 |

- **关键机制**：`New` 在 **CE 下**创建并**把 CE 推进到新元素**。建模必须先把 CE 导航到正确 owner（ZONE→PIPE→BRANCH→管件）。
- **选型硬约束**：管件**从管等级（Pspec）里 Choose 实件**，不是自己填 ODIA/WALL/材料。几何/壁厚/材料由 catalogue 派生（接 [modeling-is-selection-not-filling]）。

### 管等级 / 保温 / 示踪
| 命令 | 逐字形态 | 出处 |
|---|---|---|
| 管等级 | `Pspec /等级名`（CE 在 PIPE 或 BRANCH） | 【真机·代码】 |
| 保温等级 | `Ispec /等级名` | 【真机·代码】 |
| 示踪等级 | `Tspec /等级名` | 【真机·代码】 |

> ⚠ **不是 `SET SPREF`**。生产代码把 SPREF/PSPEC 请求统一路由到 `Pspec`（真机闭环存活）。

### 坐标 / 朝向
| 命令 | 逐字形态 | 出处 | 备注 |
|---|---|---|---|
| 分支头坐标 | `HPOS E 1000 N 2000 U 500` | 【社区】骨架 | CE 在 BRANCH 直接发 |
| 分支尾坐标 | `TPOS E 5000 N 2000 U 1500` | 【社区】骨架 | 同上 |
| 带参考系 | `... WRT /*`（相对世界） | 【社区·真实代码】me-hungry `VAR !PPOS IDP@ WRT /*` | `/*` = 世界坐标系 |
| 头/尾方向 | `HDIR E` / `TDIR W`（单词 N/S/E/W/U/D） | 【社区】 | 单方向词 |

> **单位**：数值遵循**会话当前工作单位**（metric 项目通常 mm）。显式后缀（`1000mm`）规则见官方 "Units in Real Expressions"（SOFTCG5.05.7），**建议命令显式带 `mm` 消歧 + 真机验接受度**。

### 建件后精确定位（优先用这些，别用 Dist）
| 命令 | 语义 | 出处 |
|---|---|---|
| `THRO PH` / `THRO PT` | 元件穿过分支头 / 尾位置 | 【社区】thepiping E3D 12.1 |
| `THRO PREV` | 穿过上一元件的点 | 【社区】 |
| `CONN` | 连接到相邻件 | 【社区】 |
| `CONN PH TO FIR MEM` / `CONN PT TO LAS MEM` | 分支头/尾连首/末成员 | 【社区】 |
| `FCONN` | 强制连接（慎用） | 【社区】 |

> **推荐定位序**：`THRO` 族 + `CONN` 族（全有出处）→ `POS ... WRT ...` 兜底。**不要用 `Dist 数值`（见二）。**

### 查询 / 读回
| 命令 | 返回 | 出处 |
|---|---|---|
| `Q ATT` | CE 全部属性"名 值"多行清单 | 【社区】 |
| `Q MEM` | CE 成员（子元素）清单 | 【社区】 |
| `Q ITLE` | 相邻元件间隐含管段长度（负值=干涉/反向） | 【社区】 |

> **别解析猜的格式**：输出缩进/分隔符各版本有差异 → 用 `Command.Result` 直采原文（Utilities XML 已验证该属性存在），拿到真格式再写解析。

### PML 变量作用域 + API 读回 ⭐【官方原文级】
- **全局 `!!name`：存活整个会话**（或直到删除）。**局部 `!name`：只在一个 PML Function/宏内部有效。**
  【官方】"Local and Global Variable Names" — `https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG3.03.07.html`
- **跨 API 调用读回必须用 `!!`**（局部 `!` 生命周期限于那次宏执行，读回本就不可靠）。
- **读回带全名前缀**：`GetStringFromPML("!!MCPQ", out …)`（【社区·真实代码】me-hungry `GetPMLVariableString("!PPOS")` 带前缀传入）。
- **变量取查询值形态**：`VAR !!x <查询词>`（**无等号、无 Q**）—— `VAR !!x NAME` / `VAR !!x BORE` / `VAR !!x IDP@ WRT /*`。
- **删除**：PML `VAR !!x DELETE`（`.NET DeletePMLVar` 是 internal，不可编译调用 → 用 PML 自删）。
- ✅ **本插件 P0 读回通道**（`EvalViaPmlGlobal` + `!!` 全局 + 前缀读回）**与官方一致**。

### 数据库操作
| 操作 | 逐字形态 | 出处 | 备注 |
|---|---|---|---|
| 存盘 | `SAVEWORK` | 【社区】+ .NET `MDB.SaveWork` 已验证 | 建模后必发 |
| 取更新 | `GETWORK` | 【社区】 | |
| 认领/释放 | **走 .NET `MDB.Claim/Release(DbElement[])`** | XML 已验证 | PML `CLAIM /元素` 形态【未验证】，绕开 |
| 撤销 | **走 .NET `Undo.UndoTransaction`** | XML 已验证 | PML 命令行形态【未验证】；官方专章 SOFTCG9.09.10 |

---

## 二、⚠ 可疑 / 反证（**不要写进工具承诺，真机裁决前禁用或降级**）

| 命令 | 问题 | 处置 |
|---|---|---|
| **`{name} DIST {值}` 作为定位** | 无任何来源；同源里 `DIST` 是**量距查询**（量两元件中心距），高度疑与查询混淆 | 已给 `e3d_pos_dist` 工具打 ⚠（[E3dToolDefinitions.cs](../../17-E3D插件/Tools/E3dToolDefinitions.cs)）+ 五管件工具推荐改 `e3d_pos_thr(THRO PH/PT/PREV)`。真机裁决前不推荐 |
| **`CUT/GAP/JOIN/ROUTE`** | 四命令**任何来源都没出现** + 代码注释自定性"伪 PML 残留"。Split Pipe 是 UI 功能，命令行形态未见记载 | `e3d_pipe_cut/gap/join/route` 极可能被拒收。真机逐条裁决：找到正解或降级为"未实现"从 schema 移除承诺（指导书 P2-C 名单） |
| **`VAR !!x = Q ATT`（等号+Q）** | `Q ATT` 是命令不是表达式；等号右侧要表达式。代码注释称 line 1057 在用，但 P0 读回通道当时是坏的、**从未端到端见过真值** | 整版输出走 `Command.Result` 直采（第一节），不绕变量。真机裁决 |
| **`SPRE /X` 属性赋值式** | 【社区】出现过但与真机结论（用 Pspec）并存、未在真机验证 | 用 Pspec 族（已过真机）；SPRE 标未验证 |
| **`WRT OWNER`** | WRT 后跟元素引用（`/名字`、`/*`）有真实代码；跟关键字 OWNER 没查到实例 | 用 `WRT /*` |
| **`HDIR N 45 U`（复合方向）** | .NET `Direction.Create(Axis, angle, Axis)` 暗示支持，但 PML 逐字形态未查到 | 真机 `HDIR $Q` 自查 |
| **`BOM` / `EXTRACT ALL {type}`** | 作为命令未在任何来源出现 | `e3d_bom` / `e3d_db_extract` 底层命令需真机核实 |

---

## 三、真机自查法（交 Claude Code / 真机操作员）

- **`$Q` = PML 语法帮助**（【社区】PML Basics 确认）。在命令行输入 `NEW ELBO CHOOSE $Q` / `HDIR $Q` → E3D 打印该位置合法的后续子句。**这是拿到本版本权威子句表最快的途径。**
- **待验证清单**（溯源底稿第 120-134 行 11 项）：
  1. Choose 后完整子句表 → `NEW ELBO CHOOSE $Q`
  2. `SPRE /X` 是否与 Pspec 等效 → 各发一次比对 `Q SPRE`
  3. HPOS 单位后缀 `1000mm` 接受度；`WRT OWNER` → 真机 + Units 章 SOFTCG5.05.7
  4. 复合方向 `HDIR N 45 U` → `HDIR $Q`
  5. **`Dist 数值` 是否为定位命令**（疑与量距 DIST 混淆）→ 真机；被拒则移除说法
  6. **CUT/GAP/JOIN/ROUTE 是否存在**（高度可疑）→ 真机逐条 + 帮助搜 "Split"
  7. `VAR !!x = Q ATT` 等号形态 → 真机（读回通道修好后再测）
  8. `GetStringFromPML` 前缀规则终裁 → 同一变量分别以 `!!NAME`/`NAME` 读
  9. CLAIM/RELEASE PML 形态；UNDO 命令行形态 → 官方 Undo 章 or 固定走 .NET
  10. `BOM`、`EXTRACT ALL {type}` 底层命令 → 真机 + 帮助文档
  11. 安装目录本版命令参考（.chm/PDF）→ 搜 `C:\...\Everything3D3.1` help/doc

---

## 四、官方手册（公开在线，无需登录，可逐页抓）

- **Software Customisation Guide（PML 语言基础，1.1 版）**：`https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/`
  - 变量作用域 SOFTCG3.03.07 · 删除变量 SOFTCG3.03.26 · 查询变量值 SOFTCG13.13.13 · Units SOFTCG5.05.7 · Undo/Redo SOFTCG9.09.10
- **3.1 手册族**（compass artifact 确认公开）：DRMPGC（Design Reference）· PDUV（Design User Guide）· SOFTCG。
  路径规律 `help.aveva.com/AVEVA_Everything3D/{版本}/{手册}/{文件}.html`。
- **版本注意**：在线官方是 1.1，真机是 3.1。**PML 语言基础跨版本极稳定；建模命令逐字形态仍以真机为最终裁判。**

---

## 五、维护约定

- 真机验过一条 → 把它从**二/三**移到**一**，标 `【真机·YYYY-MM-DD】` + 结论（接受/拒收/正确形态）。
- 被真机拒的工具 → schema 降级为"未实现"，别留假承诺（诚实边界，接 [E3D_MCP_完善指导书] P2-C）。
- 本文件与 `E3dToolDefinitions.cs` 工具描述、`assembler.ts` 建模铁律**保持同口径**——三处引用同一置信度分级。

# Claude Code E3D Full Guide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付一份供 Claude Code 强制遵循的 AVEVA E3D 3.1 外部驱动建模全量指导书，并在两个关联仓库建立唯一入口。

**Architecture:** 主指导书负责方法、语法、改造和验证；已有官方逐字手册与 JSON 数据集作为证据层，不复制成另一套命令真相。两个 `CLAUDE.md` 只增加路由与硬规则，所有详细内容链接回主指导书。

**Tech Stack:** Markdown、AVEVA E3D 3.1 DBRM/DRMPGC/SOFTCG、C#/.NET Framework 4.7.2、TypeScript、MCP、PowerShell/Git 验证命令。

---

## File map

- Create `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`：唯一主指导书。
- Modify `C:\项目\17-E3D插件\CLAUDE.md`：插件侧强制阅读入口和最低安全规则。
- Modify `C:\项目\09-配管智能体\CLAUDE.md`：Agent/中间层侧强制阅读入口和跨仓路径。
- Reference only `C:\项目\17-E3D插件\docs\E3D命令手册-官方逐字版.md`：官方逐字命令证据。
- Reference only `C:\项目\17-E3D插件\docs\E3D-PML命令参考-权威版.md`：置信度速查。
- Reference only `C:\项目\17-E3D插件\docs\data\e3d-commands.json`：机读命令数据。

### Task 1: Lock the evidence and current-code map

**Files:**
- Read: `C:\项目\17-E3D插件\Tools\E3dApiWrapper.cs`
- Read: `C:\项目\17-E3D插件\Tools\E3dToolDefinitions.cs`
- Read: `C:\项目\17-E3D插件\Tools\E3DToolHandler.cs`
- Read: `C:\项目\09-配管智能体\src\agent\builtin\e3dPmlBatch.ts`
- Read: `C:\项目\09-配管智能体\src\agent\pml\pmlLinter.ts`
- Read: `C:\项目\09-配管智能体\src\agent\systemPrompt\assembler.ts`

- [ ] **Step 1: Capture all command-emission sites**

Run:

```powershell
rg -n -S 'RunWrite\(|RunPml\(|TryRunPml\(|NavigateThenWrite\(|NEW |CONNECT|DISTANCE|UNDODB|SAVEWORK' C:\项目\17-E3D插件\Tools C:\项目\09-配管智能体\src\agent
```

Expected: command producers in `E3dApiWrapper.cs`, `e3dPmlBatch.ts`, system prompt, and linter are visible with line numbers.

- [ ] **Step 2: Confirm local typed APIs**

Run:

```powershell
python C:\项目\09-配管智能体\dump_dotnet_api.py C:\项目\17-E3D插件\lib\Aveva.Core.Database.dll DbElement
python C:\项目\09-配管智能体\dump_dotnet_api.py C:\项目\17-E3D插件\lib\Aveva.Core.Database.dll MDB
```

Expected: output contains `CreateLast`, typed `SetAttribute`, `Delete`, `Claim`, `Release`, `SaveWork`, and `GetWork`.

- [ ] **Step 3: Record the authority model in the guide outline**

The outline must use these exact states:

```text
official-verbatim
official-concept
local-api-confirmed
target-version-verified
community
not-found
rejected-on-target
```

### Task 2: Write foundations and command-language boundaries

**Files:**
- Create: `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`

- [ ] **Step 1: Write the mandatory Claude Code rules**

Include explicit rules that Claude Code must not generate production raw PML, must not infer syntax from tool names, must preserve dirty worktrees, and must distinguish research, implementation, and target-version verification.

- [ ] **Step 2: Write the four-layer model**

Define these layers separately:

```text
PML language: variables, expressions, control flow, macros
DBRM command language: CE navigation, attributes, hierarchy, transactions
DRMPGC Design commands: piping selection, positioning, orientation, connection
.NET API: typed in-process database and geometry operations
```

- [ ] **Step 3: Write context rules**

Cover CE, Owner, Current List Position, module, MDB, writable DB, primary elements, claim mode, working units, `gid`, DBREF, Position, Direction, and Orientation.

- [ ] **Step 4: Verify headings and forbidden placeholders**

Run:

```powershell
rg -n '^#|^##|^###' C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md
rg -n -i 'TBD|TODO|fill in|implement later' C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md
```

Expected: foundation headings are present; placeholder search returns no matches.

### Task 3: Write the complete operation and syntax guide

**Files:**
- Modify: `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`

- [ ] **Step 1: Add navigation and hierarchy operations**

Document `/name`, `OWNER`, `END`, `FIRST/LAST/NEXT/PREVIOUS`, `MEMBER`, `SAME`, `NEW`, `DELETE <type>`, `NAME`, `RENAME`, `COPY`, `INCLUDE`, and `REORDER`, with CE preconditions and wrong-form corrections.

- [ ] **Step 2: Add attribute typing and assignment**

Document REAL, INTEGER, TEXT, LOGICAL, REF, WORD, POSITION, DIRECTION, ORIENTATION and arrays. State that bare `ATTR value` and `!!CE.ATTR = expression` are valid forms, while POS uses `AT`; provide typed .NET mappings.

- [ ] **Step 3: Add PIPE/BRAN/spec/component modeling**

Document PSPE vs SPREF, PSPEC inheritance, SELECT vs CHOOSE, component creation from specifications, and why a bare component without SPREF has no geometry.

- [ ] **Step 4: Add positioning and connection**

Document HPOS/TPOS, HDIR/TDIR, HBORE/TBORE, HCONN/TCONN, AT, BY, DISTANCE, THROUGH, CLEARANCE, DIRECTION, ORIENTATE, ROTATE, FLIP, Branch CONNECT, Component CONNECT, and FCONNECT.

- [ ] **Step 5: Add database transactions**

Document CLAIM/UNCLAIM, GETWORK, MARKDB/UNDODB/REDODB, SAVEWORK comments, and the prohibition against an automatic leading SAVEWORK.

- [ ] **Step 6: Add correct/wrong matrix**

The matrix must include at least these corrections:

```text
!!CE = /X -> CurrentElement.Element or /X navigation command
SET SPREF -> PSPEC for PIPE/BRAN or SELECT/SPREF semantics for component
NEW ELBO CHOOSE -> NEW ELBO SELECT WITH ... for unattended execution
/X POS x y z -> navigate then AT E... N... U... or typed Position
CONNECT /A TO /B -> Branch marker connect or adjacent Component connect
/X DELETE -> navigate then DELETE <generic-type>
UNDO -> UNDODB
RELEASE -> UNCLAIM
EXTRACT ALL PIPE -> COLLECT/.NET search; EXTRACT has different semantics
MOVE /X TO /Y -> target CE then INCLUDE /X
```

### Task 4: Write the implementation architecture for both repositories

**Files:**
- Modify: `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`

- [ ] **Step 1: Define E3DPlan and operation schemas**

Include complete JSON examples for `create_element`, `set_attribute`, `set_position`, `set_pipe_spec`, `insert_component`, `connect_branch_end`, `connect_component`, `delete_element`, and `save_work`.

- [ ] **Step 2: Define the dual backend**

Specify typed .NET as primary for navigation, validation, ordinary create/read/write/delete/copy/claim/save; restrict command templates to PSPEC, SELECT, connection, constrained positioning, and specialized Design semantics.

- [ ] **Step 3: Define execution and result contracts**

Specify preconditions, main-thread execution, CE before/after, command result/error capture, typed post-read assertions, rollback state, and structured MCP error responses.

- [ ] **Step 4: Add the exact current-file remediation map**

Cover at least:

```text
C:\项目\17-E3D插件\Tools\E3dApiWrapper.cs
C:\项目\17-E3D插件\Tools\E3dToolDefinitions.cs
C:\项目\17-E3D插件\Tools\E3DToolHandler.cs
C:\项目\17-E3D插件\Tools\E3dCommandReference.cs
C:\项目\09-配管智能体\src\agent\builtin\e3dPmlBatch.ts
C:\项目\09-配管智能体\src\agent\builtin\dispatchE3DCommands.ts
C:\项目\09-配管智能体\src\agent\pml\pmlLinter.ts
C:\项目\09-配管智能体\src\agent\systemPrompt\assembler.ts
C:\项目\09-配管智能体\src\agent\mcp\e3dWriteGate.ts
C:\项目\09-配管智能体\e3d_middleware\mock_e3d_mcp_server.py
```

For each file state what to keep, remove, replace, and verify.

### Task 5: Write Claude Code SOP, diagnostics, and test matrix

**Files:**
- Modify: `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`

- [ ] **Step 1: Add the unknown-command protocol**

Require Claude Code to stop guessing, search official sources, inspect target-version `$Q`, create a minimal disposable-DB test, capture exact command/error/context, and update confidence state only after evidence.

- [ ] **Step 2: Add logging and error taxonomy**

Define JSONL fields for job, step, E3D version, module, MDB, units, CE, owner, operation, compiled command/API, result, error code/message, postconditions, rollback, and save state.

- [ ] **Step 3: Add L0-L3 testing**

Provide exact acceptance cases for hierarchy, spec, SELECT, positioning, Branch and Component connections, claim/unclaim, undo/redo, save, and deliberate invalid commands.

- [ ] **Step 4: Add copy-ready command fragments and checklists**

Templates must be labelled `illustrative`, `official-verbatim`, or `target-version-verified`; none may be labelled production-ready without true-machine evidence.

### Task 6: Add mandatory CLAUDE.md entry points

**Files:**
- Modify: `C:\项目\17-E3D插件\CLAUDE.md`
- Modify: `C:\项目\09-配管智能体\CLAUDE.md`

- [ ] **Step 1: Patch the plugin CLAUDE.md**

Add a top-level section after project overview:

```markdown
## 修改 E3D 建模功能前必读

任何涉及 E3D 命令、PML、元素创建、属性写入、定位、连接、事务或 MCP 写工具的工作，必须先完整阅读 `docs/Claude-Code-E3D建模全量指导书.md`。不得根据工具名猜命令；未经目标 E3D 3.1 真机验证的模板不得宣称可用。
```

- [ ] **Step 2: Patch the Agent CLAUDE.md**

Add the same rule with the absolute authority path:

```markdown
## 修改 E3D 建模功能前必读

唯一权威方法指南：`C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`。修改命令生成、PML lint、系统提示、MCP 工具、写回闸或 mock 前必须完整阅读；本仓文档不得另造冲突语法。
```

### Task 7: Self-review and verify the deliverable

**Files:**
- Verify: `C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md`
- Verify: both `CLAUDE.md` files

- [ ] **Step 1: Run structural checks**

Run:

```powershell
$guide='C:\项目\17-E3D插件\docs\Claude-Code-E3D建模全量指导书.md'
(Get-Content -Encoding UTF8 $guide | Measure-Object -Line -Word -Character)
rg -n '^## ' $guide
rg -n -i 'TBD|TODO|未经解释|以后补|待补' $guide
```

Expected: all designed sections exist; no unresolved placeholders.

- [ ] **Step 2: Check required facts**

Run:

```powershell
rg -n 'SELECT WITH|CHOOSE|PSPEC|SPREF|UNDODB|UNCLAIM|INCLUDE|E3DPlan|fail-closed|L0|L1|L2|L3' $guide
```

Expected: every required topic has one or more substantive matches.

- [ ] **Step 3: Check authority links**

Run:

```powershell
rg -n 'help\.aveva\.com|E3D命令手册-官方逐字版|e3d-commands\.json' $guide
rg -n 'Claude-Code-E3D建模全量指导书' C:\项目\17-E3D插件\CLAUDE.md C:\项目\09-配管智能体\CLAUDE.md
```

Expected: official links and both Claude Code entry points are present.

- [ ] **Step 4: Inspect isolated diffs**

Run:

```powershell
git -C C:\项目\17-E3D插件 diff -- docs/Claude-Code-E3D建模全量指导书.md CLAUDE.md
git -C C:\项目\09-配管智能体 diff -- CLAUDE.md
```

Expected: only the guide and two intended entry-point changes are part of this task; existing unrelated dirty changes remain untouched.

- [ ] **Step 5: Commit documentation changes separately per repository**

Run in `17-E3D插件`:

```powershell
git add -- docs/Claude-Code-E3D建模全量指导书.md CLAUDE.md docs/superpowers/plans/2026-07-03-claude-code-e3d-full-guide.md
git commit -m "docs: add authoritative E3D modeling guide"
```

Run in `09-配管智能体`:

```powershell
git add -- CLAUDE.md
git commit -m "docs: link authoritative E3D modeling guide"
```

Expected: each commit contains only the listed documentation files.

# Claude Code E3D 建模全量指导书设计规格

日期：2026-07-03  
状态：已在对话中确认，待用户审阅书面规格

## 1. 目标

编写一份 Claude Code 可以直接遵循的 AVEVA E3D 3.1 外部驱动建模指导书，使其在阅读、修改 `17-E3D插件` 与 `09-配管智能体` 时能够：

- 区分 PML、数据库命令、Design 建模命令和 E3D .NET API；
- 不猜测命令，不把未经验证的语法包装成可用工具；
- 根据元素类型、当前元素、Owner、模块、单位和管等级生成合法操作；
- 优先使用强类型 .NET API，仅在确有必要时使用受控命令模板；
- 对每次写入执行预检、审批、事务、逐步验证、失败回滚和结构化审计；
- 依据官方资料、目标版本 `$Q` 和真机结果维护命令置信度。

## 2. 交付物

### 2.1 唯一主指导书

路径：`docs/Claude-Code-E3D建模全量指导书.md`

它是 Claude Code 进行 E3D 命令生成和插件修改时的唯一方法论入口。已有资料不重复改写为另一套真相，而作为证据层引用：

- `docs/E3D命令手册-官方逐字版.md`
- `docs/E3D-PML命令参考-权威版.md`
- `docs/data/e3d-commands.json`

### 2.2 Claude Code 启动入口

在以下文件加入简短、强制性的“修改 E3D 前必读”段落：

- `C:\项目\17-E3D插件\CLAUDE.md`
- `C:\项目\09-配管智能体\CLAUDE.md`

入口只承担路由作用，不复制命令正文，避免多份规则漂移。

## 3. 文档结构

主指导书按“先建立正确心智模型，再教命令，最后教改代码”的顺序组织：

1. 文档权威级别、适用版本和 Claude Code 强制规则；
2. E3D 四层语言模型：PML、DBRM 数据库命令、DRMPGC Design 命令、.NET API；
3. CE、Owner、成员列表位置、模块、MDB、工作单位和引用类型；
4. 官方证据、社区证据、真机证据的置信度分级和晋级流程；
5. 元素导航和层级合法性；
6. 属性类型、赋值方式、单位、Position/Direction/Orientation；
7. NEW、DELETE、NAME/RENAME、COPY、INCLUDE、REORDER；
8. SITE、ZONE、EQUI、STRU、PIPE、BRAN、NOZZ 建模；
9. PSPEC、SPREF、ISPEC、TSPEC、SELECT、CHOOSE 和 Catalogue 派生关系；
10. 配管组件插入、前后顺序、管径与连接类型继承；
11. HPOS/TPOS、HDIR/TDIR、AT、BY、DISTANCE、THROUGH、CLEARANCE、ROTATE；
12. Branch 头尾连接与 Component 相邻连接；
13. CLAIM/UNCLAIM、MARKDB/UNDODB/REDODB、GETWORK/SAVEWORK；
14. .NET API 优先策略和命令后端的限定范围；
15. `E3DPlan` 强类型操作模型、编译、执行与验证协议；
16. MCP 工具 schema 设计、错误码和回执契约；
17. 当前两个仓库的逐文件、逐函数整改清单；
18. PML/命令 linter 从正则升级为语法与语义校验的方法；
19. 模拟器、合同测试、真机测试、命令晋级和回归测试；
20. 故障诊断、日志字段、最小复现和未知语法处理；
21. Claude Code 标准工作流程、修改前后检查清单；
22. 正确/错误命令对照、可复制模板、黑名单和术语表。

## 4. 核心技术裁决

指导书将明确以下不可妥协的边界：

- LLM 不直接生成生产 PML；LLM 生成结构化 `E3DPlan`。
- 导航、普通创建、属性读写、类型化几何属性、删除、Claim 和 SaveWork 优先走 .NET API。
- PSPEC、SELECT、CONNECT、受约束定位等 E3D 语义命令走版本化模板。
- `CHOOSE` 属于交互式图形选型；无人值守主链使用 `SELECT WITH`。
- PIPE/BRAN 的 PSPE 与 Component 的 SPREF 不得互相替换。
- 每个写操作必须声明目标、前置条件、影响、可逆性和读回断言。
- 未安装审批门时生产写操作 fail-closed。
- 不在任务开头执行 SAVEWORK；最终保存必须在全量验证和用户确认之后。
- 原始 PML 逃生舱必须管理员限定，并使用逐行 AST 安全检查，不能只检查字符串首词。

## 5. 数据流设计

```text
用户意图
  -> Claude Code 生成 E3DPlan JSON
  -> schema 校验
  -> E3D 上下文预检（模块/MDB/CE/Owner/单位/Claim/规格）
  -> 编译为 Typed API 操作或版本化 E3D 命令
  -> 用户预览并批准
  -> MARKDB 事务边界
  -> E3D 主线程逐步执行
  -> 每步读回并验证 postcondition
  -> 失败 UNDODB / 成功等待最终 SAVEWORK 确认
  -> 结构化结果与审计日志
```

## 6. 错误处理设计

每一步返回结构化对象，而不是依赖 `OK:`/`Error:` 文本嗅探：

```json
{
  "jobId": "mcp-20260703-001",
  "stepId": "step-04",
  "operation": "insert_component",
  "status": "verified|rejected|failed|rolled_back",
  "ceBefore": "/P-101/B1",
  "ceAfter": "/P-101/B1/ELBO-1",
  "command": "NEW ELBO SELECT WITH STYP LR",
  "e3dError": null,
  "assertions": ["TYPE=ELBO", "SPREF is valid"],
  "evidence": ["Command.RunInPdms=true", "post-read TYPE=ELBO"]
}
```

任何“命令返回成功但读回不满足断言”的情况都按失败处理。

## 7. 测试与验收

指导书将定义四层验证：

- L0：官方语法样例与 AST 编译 golden tests；
- L1：MCP schema、handler、mock 和前端工具签名一致性；
- L2：模拟 CE、Owner、成员列表和事务状态机；
- L3：目标 E3D 3.1 专用测试 MDB 真机验证。

真机矩阵至少覆盖创建、属性、规格、组件选型、定位、连接、复制、层级重组、删除、Claim、Undo/Redo、SaveWork。未经 L3 验证的模板不能进入生产白名单。

## 8. 写作标准

- 中文解释，命令和 API 保留官方英文拼写；
- 每个高风险结论附官方 URL、目标版本说明或真机证据状态；
- 每节同时给出“正确写法、错误写法、为什么、适用上下文、验证方法”；
- 不把“文档中未找到”写成“官方确认不存在”；统一标为 `not-found`，由真机裁决；
- 当前代码问题使用绝对文件路径和行号，便于 Claude Code 直接定位；
- 不包含 TBD、TODO 或未解释占位符。

## 9. 范围边界

本轮只编写指导书并更新两个 `CLAUDE.md` 入口，不修改现有 C#、TypeScript、Python 业务代码，不覆盖或提交用户现有未提交改动。后续代码整改应基于本指导书另行制定实施计划。

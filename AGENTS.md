# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## 项目概述

AVEVA E3D 3.1 C# 插件 — 在 E3D 中选取管道，提取属性数据，通过 HTTP 发给中间层 `09-配管智能体` 的 E3D Middleware（FastAPI :7861）进行 ASME B31.3 规范验证。

**中间层项目**: `C:\项目\09-配管智能体`

## 常用命令

```bash
# 构建（PMLNet.dll 已就位，可直接编译）
dotnet build E3DPlugin.csproj -c Release

# 中间层启动
cd "C:\项目\09-配管智能体" && npm run server   # → localhost:7861
```

## 架构

```
E3D PML: !!E3DPlugin.E3DAddin.Validate()
  → E3DAddin (标记 [PMLNetCallable])
    → AvevaE3DContext (PMLNetAny.getGlobalVariable("!!CE"))
      → AvevaElement (包装 PMLNetAny)
        → PipelineAttributeExtractor (读取 Db 属性)
          → E3DPipelineAttribute (POCO)
            → E3DMiddlewareClient (HttpClient → JSON)
              → POST /api/e3d/validate → :7861
```

## E3D 3.1 PMLNet API 要点

- **命名空间**: `Aveva.Core.PMLNet`（非 `Aveva.PDMS.*`）
- **`PMLNetCallable`**: 是 Attribute，不是基类。标记 `[PMLNetCallable]` 在类和公开方法上
- **`PMLNetAny`**: 无公开构造函数，用静态方法创建:
  - `PMLNetAny.getGlobalVariable("!!CE")` — 获取当前选中元素
  - `PMLNetAny.newTemporary("STRING", args, nargs)` — 创建临时对象
- **`PMLNetEngine.GetProperty(token, name, ref val)`** — 第三个参数是 `ref Object`
- **`PMLNetAny.getStructureMember(name, ref result)`** — 返回 bool, 结果通过 `ref Object`
- **`PMLNetAny.invokeMethod(name, args, nargs, ref result)`** — 调用 PML 方法
- **`PMLNetAny.setValue(value, dimension, units)`** — 三个参数都是必需的

## 数据协议

POST `http://localhost:7861/api/e3d/validate`，字段与 E3D 属性映射：

| C# 字段 | E3D 属性 | 来源 |
|----------|---------|------|
| PipelineId | NAME | PIPE |
| LineNumber | NAME | BRAN (OWNER) |
| NominalPipeSize | SBOR | BRAN |
| Schedule | SCHD | BRAN |
| OuterDiameter | ODIA | PIPE |
| WallThickness | WALL | PIPE |
| MaterialSpec | MTYP / SPREF | PIPE |
| MaterialCategory | TXSP / MTYP | SPEC |
| DesignPressure | PRES | PIPE |
| DesignTemperature | TEMP | PIPE |
| ConnectionType | FUNC | PIPE |
| EntityType | "pipe" (固定) | — |

## E3D 部署

将 `bin\Release\net48\` 下的 `E3DPlugin.dll`、`Newtonsoft.Json.dll`、`E3DPlugin.dll.config` 复制到 E3D 机器。

在 E3D PML 控制台中加载：
```
PMLNetEngine.LoadAssembly("C:\路径\E3DPlugin.dll")
!!E3DPlugin.E3DAddin.Validate()
!!E3DPlugin.E3DAddin.ShowForm()
!!E3DPlugin.E3DAddin.HealthCheck()
```

## 项目结构

```
E3DPlugin.csproj              # net48, 引用 PMLNet.dll
E3DAddin.cs                   # [PMLNetCallable] 入口, Validate/ShowForm/HealthCheck
MainForm.cs                   # WinForms 界面
DevTestHarness.cs             # 独立测试控制台
Models/E3DModels.cs           # POCO — 匹配中间层 JSON 协议
Services/
  IE3DContext.cs              # E3D 元素/上下文抽象接口
  PipelineAttributeExtractor.cs  # BRAN→PIPE→SPEC 层级导航 + 属性提取
  E3DMiddlewareClient.cs      # HttpClient → POST /api/e3d/validate
  AvevaApiAdapter.cs          # AvevaElement/AvevaE3DContext — PMLNetAny 桥接
  MockE3DContext.cs           # Mock 实现 — 脱离 E3D 测试
```

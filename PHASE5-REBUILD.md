# Phase 5 — Rebuild & Reload

The Phase 5 commit adds 8 new MCP tools (3 pipe-analysis + 2 batch + 3 event-bus) to the
E3D plug-in. Those land as C# source files; they only become available to the agent
after you rebuild the DLL and reload the addin in E3D 3.1.

## What was added

| File | New | Purpose |
|------|----:|---------|
| `Tools/E3dPipeAnalysis.cs` | new | `e3d_pipe_slope_check`, `e3d_pipe_drain_holes`, `e3d_support_spacing_plan` |
| `Tools/E3dBatchOps.cs` | new | `e3d_element_batch_create`, `e3d_attr_batch_set_multi` |
| `Tools/E3dEventBus.cs` | new | `e3d_subscribe`, `e3d_unsubscribe`, `e3d_poll_events` |
| `Tools/E3dToolDefinitions.cs` | edit | registers the 8 new tool definitions |
| `Tools/E3DToolHandler.cs` | edit | routes the 8 new tool names |

Total tool count in `E3dToolDefinitions.cs`: was 86, now 94.

## Rebuild

```powershell
cd C:\项目\17-E3D插件
# Visual Studio Developer Command Prompt or msbuild on PATH:
msbuild MCPServer.csproj /p:Configuration=Release /p:Platform="Any CPU"
```

Output DLL: `bin\Release\net472\MCPServer.dll`.

## Reload in E3D

The plug-in is loaded on E3D start via `IAddinInjected`. To pick up the new
build:

1. Save your design in E3D (`SAVEWORK`).
2. Close E3D 3.1.
3. Copy the new `MCPServer.dll` plus any updated dependencies into the same
   folder the running E3D uses (your existing deploy script in
   `C:\项目\09-配管智能体\install.ps1` does this for the desktop build).
4. Restart E3D.
5. Verify `http://127.0.0.1:8286/tools/list` reports 94 tools (was 86).

## Optional: wire CurrentElementChanged to E3dEventBus

`E3dEventBus.Publish(kind, elementPath, detail)` is a static hook intended
to be called from `McpServerAddin.cs` when E3D fires events. Until that
hook is added, subscribers will see an empty queue on every `poll_events`
call — useful only as a protocol smoke test. Suggested glue (one-time):

```csharp
// in McpServerAddin.cs, inside the init code that already wires CE changes:
CurrentElement.CurrentElementChanged += (s, e) =>
{
    E3DMcpServer.Tools.E3dEventBus.Publish(
        "current_element_changed",
        CurrentElement.Element?.FullName,
        "");
};
```

This is intentionally NOT wired in this commit so the build stays a pure
addition. Toggle it on once you've verified the new tools load.

## Verification checklist after reload

- [ ] `e3d_pipe_slope_check` on a known liquid line returns slope% per segment.
- [ ] `e3d_pipe_drain_holes` on a sagged line returns at least one low point.
- [ ] `e3d_support_spacing_plan` on a 200mm pipe returns spacing ≈5800mm by default.
- [ ] `e3d_element_batch_create` with 3 dummy elements returns ok=3 fail=0.
- [ ] `e3d_attr_batch_set_multi` with 2 elements × 3 attrs reports correct counts.
- [ ] `e3d_subscribe events="*"` returns a subscription_id; `e3d_poll_events` returns
      events after you click around in the GUI (only once the publisher is wired).

## Rollback

Revert the three .cs creates + the two .cs edits with `git revert <hash>` in
`C:\项目\17-E3D插件`. The mock parity allow-list in
`C:\项目\09-配管智能体\e3d_middleware\tests\test_mock_signature_parity.py`
includes these 8 names — if you revert in the plug-in you must remove
them from the allow-list too, otherwise the parity test will fail.

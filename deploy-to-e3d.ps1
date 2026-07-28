<#
  deploy-to-e3d.ps1 — 把编译好的 MCPServer.dll 部署 + 注册进 E3D，让 E3D 启动时加载插件。
  【在装了 E3D 的那台机器上跑】。编译只产出 bin\Release\net472\MCPServer.dll，
  E3D 从 <E3D安装目录>\E3DAddins\MCPServer\ 加载 —— 中间这步拷贝+注册不做，E3D 就不会加载。

  用法（管理员 PowerShell）：
    .\deploy-to-e3d.ps1                              # 用默认 E3D 目录 D:\AVEVA\Everything3D3.1
    .\deploy-to-e3d.ps1 -E3DDir "C:\AVEVA\E3D3.1"    # E3D 装别处就指定
    .\deploy-to-e3d.ps1 -DllPath "\\devpc\17-E3D插件\bin\Release\net472\MCPServer.dll"  # DLL 在开发机(网络路径)

  跑完【关掉 E3D 再重开】。验证：浏览器开 http://127.0.0.1:8286/tools/list 应列出 ~96+ 个工具。
#>
param(
  [string]$E3DDir  = "D:\AVEVA\Everything3D3.1",
  [string]$DllPath = "$PSScriptRoot\bin\Release\net472\MCPServer.dll"
)

$ErrorActionPreference = "Stop"
Write-Host "==== E3D MCP 插件部署 ====" -ForegroundColor Cyan

# 1. 校验 E3D 目录
if (-not (Test-Path (Join-Path $E3DDir "Aveva.ApplicationFramework.dll"))) {
  Write-Host "[✗] 这不是有效的 E3D 安装目录（找不到 Aveva.ApplicationFramework.dll）：$E3DDir" -ForegroundColor Red
  Write-Host "    用 -E3DDir 指定真实 E3D 安装路径再跑。" -ForegroundColor Yellow
  exit 1
}
Write-Host "[✓] E3D 目录: $E3DDir"

# 2. 校验源 DLL
if (-not (Test-Path $DllPath)) {
  Write-Host "[✗] 找不到编译产物 DLL：$DllPath" -ForegroundColor Red
  Write-Host "    先在开发机 dotnet build -c Release，或用 -DllPath 指向真实 DLL（可网络路径）。" -ForegroundColor Yellow
  exit 1
}
$src = Get-Item $DllPath
Write-Host ("[✓] 源 DLL: {0}  ({1:N0} KB, {2})" -f $src.FullName, ($src.Length/1KB), $src.LastWriteTime)

# 3. 拷到 E3DAddins\MCPServer\
$addinDir = Join-Path $E3DDir "E3DAddins\MCPServer"
New-Item -ItemType Directory -Force -Path $addinDir | Out-Null
$destDll = Join-Path $addinDir "MCPServer.dll"
if (Test-Path $destDll) {
  $old = Get-Item $destDll
  Write-Host ("    覆盖旧 DLL ({0:N0} KB, {1})" -f ($old.Length/1KB), $old.LastWriteTime) -ForegroundColor DarkGray
}
Copy-Item -LiteralPath $src.FullName -Destination $destDll -Force
Write-Host "[✓] 已部署: $destDll"

# 4. 注册进 DesignAddins.xml（<ArrayOfString>，加一条相对路径条目；幂等）
$xmlPath = Join-Path $E3DDir "DesignAddins.xml"
$entry = "E3DAddins\MCPServer"
if (-not (Test-Path $xmlPath)) {
  Write-Host "[!] $xmlPath 不存在，新建一个只含本插件的清单（若 E3D 另有插件清单位置，请把这条并进去）。" -ForegroundColor Yellow
  @"
<?xml version="1.0" encoding="utf-8"?>
<ArrayOfString xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <string>$entry</string>
</ArrayOfString>
"@ | Set-Content -LiteralPath $xmlPath -Encoding UTF8
  Write-Host "[✓] 已建 DesignAddins.xml 并注册"
} else {
  $raw = Get-Content -LiteralPath $xmlPath -Raw
  if ($raw -match [regex]::Escape($entry) -or $raw -match 'E3DAddins[\\/]+MCPServer') {
    Write-Host "[✓] DesignAddins.xml 已注册 MCPServer（无需改）"
  } else {
    Copy-Item -LiteralPath $xmlPath -Destination "$xmlPath.bak" -Force
    $patched = $raw -replace '(?s)(\s*)</ArrayOfString>', ("`r`n  <string>$entry</string>`$1</ArrayOfString>")
    Set-Content -LiteralPath $xmlPath -Value $patched -Encoding UTF8
    Write-Host "[✓] 已把 <string>$entry</string> 加进 DesignAddins.xml（原文件备份为 .bak）"
  }
}

Write-Host ""
Write-Host "==== 完成。下一步 ====" -ForegroundColor Green
Write-Host "  1) 关闭 E3D 3.1（先在 E3D 里 SAVEWORK）"
Write-Host "  2) 重新打开 E3D 3.1"
Write-Host "  3) 浏览器验证: http://127.0.0.1:8286/tools/list  （应列出工具；插件里含新的命令黑名单守卫）"
Write-Host "  若仍不加载：E3D 的插件清单可能不叫 DesignAddins.xml 或在别处——看 E3D 启动日志/用 %TEMP%\pipingclaw_e3d_pml.log 排查。" -ForegroundColor DarkGray

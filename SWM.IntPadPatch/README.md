# 斯达划片间 SWM 整数版 PAD 补丁

项目对现场 `SWM.dll` 2024.10.1.6 做定点 IL 修补。沿用现场 `AutoReviewSystem.Data.dll` 1.0.0.3，`WSTICParameters.IsEnablePadErase` 保持 `int`，原值继续写入 `InspectionParam[9]`。补丁保留托管程序集版本、公共接口及原生导入声明。

## 参数协议

| 现场 `BrightBlackJudge` | 代理目标 | 每个 PAD 的 `double` 数 |
| --- | --- | --- |
| `3`、`4`、`5` | `inspection_new.dll` | 4，`X/Y/Width/Height` |
| 其他值 | `inspection_old.dll` | 5，保留现场 SWM 的第 5 项及其计算逻辑 |

旧核在 RVA `0x20242` 到 `0x20263` 读取四个坐标，在 `0x20560` 将 PAD 指针前移 `0x28` 字节。新核在 `0x3BE93` 到 `0x3BEB4` 读取四个坐标，在 `0x3C17C` 前移 `0x20` 字节。现场代理的路由函数在 RVA `0x16A40`，分别对 `3.0`、`4.0`、`5.0` 分流。RVA 以 DLL 映像基址为起点。

| 文件 | SHA-256 |
| --- | --- |
| `SWM.dll` | `21FECEFF447D5A641BF6A4DC8FEB12C0C72DB46A6428B6C48F33960D723E1AF3` |
| `AutoReviewSystem.Data.dll` | `D9D6D54696DC301C8909E78F218AAFF0FF100FB4F0B343285A9B153758DD9A20` |
| `inspection.dll` | `CD14F0EAC01D5964931C855766F98CF165B715C98D40DDD0E97D5826AE1FEDBB` |
| `inspection_old.dll` | `29CBF7075244988D095D04CB9933555CEAF8F5D896BD879B007809FDE5C739AE` |
| `inspection_new.dll` | `FB478DCC1FE2F18391AC0D0B3B655C1292EE224893B32D90A6471BC4D4528786` |

`PadRois` 将配方矩形裁剪到 Die 像素范围并保留正面积交集。`ImageArrayNum` 和 `GetParam` 共用同一个 PAD 步长计算。原始 `padContractSize`、`marginT` 和 Probe Mark 模式继续下发。补丁通过原 DLL 哈希确认 IL 偏移对应的版本。

## 构建及本地回归

在 Windows PowerShell 5.1 使用 Mono.Cecil 0.11.5 的 `net40` 程序集。将下列路径指向现场原件目录和对应的 Mono.Cecil。输出路径需是新文件。

```powershell
$field = 'D:\Projects\opencvProject\WaferInspectionPatternGpu\swm\dll\斯达划片间'
$cecil = Join-Path $env:USERPROFILE '.nuget\packages\mono.cecil\0.11.5\lib\net40\Mono.Cecil.dll'
New-Item -ItemType Directory -Path .\bin -Force | Out-Null
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Build-Patch.ps1 -InputAssembly "$field\SWM.dll" -OutputAssembly .\bin\SWM.dll -CecilPath $cecil
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Test-Patch.ps1 -SwmPath .\bin\SWM.dll -DataPath "$field\AutoReviewSystem.Data.dll"
```

回归调用实际托管入口 `SWM.Parameters.GetParam`。合成配方覆盖 `BrightBlackJudge=0..5`、PAD 模式 `0/1/2/3/-1`、相邻 IPROI、多个 FOV、跨右下边界和零面积交集。现场放行仍需用实际机种回放缺陷输出，确认 PAD 区域屏蔽和既有 Probe Mark 检出。

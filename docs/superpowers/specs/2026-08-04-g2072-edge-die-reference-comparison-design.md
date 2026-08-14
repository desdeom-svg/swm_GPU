# G2072 边缘三 Die 规划对照设计

## 目标

验证旧 SWM 在不含边缘三 Die 组的规划下能否加载，并与当前新 SWM 在原始完整规划下比较参考图选择和触发检测模式。

## 基线

- 原始 Recipe：`D:\Config\Recipe\G2072A00_ME_DEP`
- 旧 DLL：`D:\新程序\算法备份\旧版本\SWM.dll`
- 新 DLL：`D:\新程序\SWM.dll`
- 原始 Recipe 的 78 条 Scan 行中，Slice 0、1、76、77 各有 15 FOV。
- 原始 15-FOV 行来自上下两组边缘 Die：
  - 上边缘：`(-1,-14)`、`(0,-14)`、`(1,-14)`
  - 下边缘：`(-1,14)`、`(0,14)`、`(1,14)`

## 对照 Recipe

新建克隆 Recipe `D:\Config\Recipe\G2072A00_ME_DEP_NoEdge3Die`，不修改原始 Recipe。

克隆 Recipe 的 TestPlan 排除上述六个 Die，再通过上位机 Recipe 规划流程重新生成序列化 Recipe 字节。不能只改 `diesInShotY`，也不能只删除 TestPlan 而不重新规划，因为 SWM 实际使用的是重算后的 `ScanSequence` 与 `FovMap`。

## 对照运行

1. 旧 DLL + 不含两组边缘三 Die 的克隆 Recipe。
2. 新 DLL + 原始完整 Recipe。
3. 新 DLL + 克隆 Recipe，作为区分 DLL 差异与 Recipe 规划差异的补充对照。

每次运行记录 GetParam 是否成功、返回行数、ImageCount、Scan 行数、每行 Scan 数、活动 Trigger 数及漏图信息。

## 参考图差异报告

以当前图的物理中心坐标作为匹配键，不使用全局图号直接匹配，因为排除边缘 Die 后全局索引会变化。

CSV 至少包含：

- 对照名称与 DLL
- 当前图全局索引、Slice、局部 Scan、物理中心坐标
- ref1/ref2 的全局索引及物理中心坐标
- 参数块 `+24` 检测模式
- Trigger 是否活动
- 对齐状态：相同、参考图不同、当前图仅在原始 Recipe、当前图仅在克隆 Recipe

## 验收条件

- 原始 Recipe 保持未修改。
- 克隆 Recipe 的规划中不再出现由上述边缘 Die 产生的 15-FOV 行。
- 旧 DLL 对克隆 Recipe 的 GetParam 不再返回 `-8`。
- 生成可审计的参考图差异 CSV 和结论摘要。

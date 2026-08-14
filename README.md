# SWM_GPU (Wafer Inspection Scan & Wafer Map)

晶圆外观检测（Wafer AOI）系统中的 **SWM (Scan & Wafer Map)** 规划与 GPU 检测参数生成核心模块。

---

## 📌 项目概述

SWM_GPU 模块作为上位机配方（Recipe）与底层 GPU 算法检测管线之间的桥梁，负责解析晶圆几何结构、规划扫描切片（Slice）与视场（FOV）路径、分配触发采集点（Trigger）、计算各区域的参考图（Golden Die / Golden ROI），并最终生成供 GPU 算子并行检测所需的完整参数块。

---

## ✨ 核心特性

- **扫描路径与 FOV 规划 (Scan & Slice Planning)**
  - 基于晶圆尺寸、Die 布局以及相机 FOV 规格，自动生成最优扫描切片（Slice）与扫描行（Scan）。
  - 支持蛇形往返扫描（Reverse Scan）方向校正与视场重叠区域（Overlap/Exception Area）计算。
  - 支持物理中心坐标与全局索引的精准映射。

- **参考图匹配与 Golden ROI 规划 (Reference & Golden Selection)**
  - 支持多模态参考图选择（同 Die 比对、相邻 Die 比对、Golden Die 比对等）。
  - 针对边缘 Die（Edge Die）与边界排除区（Exclusion Limit）提供自适应参考图匹配策略。
  - 细粒度 ROI（IPROI / GoldenROI）几何交集与有效检测区域裁剪。

- **GPU 算子参数生成 (Parameters Engine)**
  - 对接 `AutoReviewSystem.Data` 数据契约与底层 GPU 图像处理算子。
  - 高性能多线程参数计算与序列化支持。
  - 输出结构化的 Trigger 检测映射表及图像比对配置。

- **回归测试与对照工具 (Regression Testing & Tools)**
  - 内置 `SWM.RegressionTests` 自动化回归测试工程。
  - 提供参考快照导出工具（`Export-SwmReferenceSnapshot.ps1`）与配方对照生成工具（`New-G2072NoEdge3DieRecipe.ps1`）。
  - 支持针对边缘 Die 差异的详细 CSV 审计与结果验证。

---

## 📁 目录结构

```text
swm/
├── Base.cs                   # 基础实体与公共定义
├── Die.cs                    # 晶粒 (Die) 几何结构与属性
├── GoldenDie.cs              # Golden Die 实体定义
├── GoldenROI.cs              # Golden ROI 参考区域定义
├── IPROI.cs                  # 图像处理 ROI 区域定义
├── Parameters.cs             # 核心参数生成引擎（接口与底层参数转换）
├── Scan.cs                   # 扫描点 / FOV 实体
├── ScanPlan.cs               # 扫描规划方案管理
├── Slice.cs                  # 扫描切片实体
├── SWMCore.cs                # 核心调度与区域交集/重叠计算逻辑
├── Wafer.cs                  # 晶圆整体模型
├── FnXMLSerializer.cs        # XML 序列化辅助工具
├── SWM.csproj                # SWM 核心类库项目文件
├── SWM.sln                   # Visual Studio 解决方案
├── SWM.RegressionTests/      # 回归测试控制台项目
├── tools/                    # 自动化与数据导出脚本
│   ├── Export-SwmReferenceSnapshot.ps1
│   └── New-G2072NoEdge3DieRecipe.ps1
├── comparison_artifacts/     # 配方规划与参考图对照产物 (CSV / JSON)
├── doc/                      # 接口说明与技术文档
└── docs/                     # 开发计划与技术设计规范
```

---

## 🛠️ 环境要求与构建

### 开发环境
- **操作系统**：Windows 10 / 11 / Server x64
- **IDE**：Visual Studio 2019 / 2022
- **运行时环境**：.NET Framework 4.8
- **依赖项**：
  - `OpenCvSharp4`
  - `AutoReviewSystem.Data`

### 构建步骤
1. 使用 Visual Studio 打开 `SWM.sln`。
2. 确保依赖项库（如 `AutoReviewSystem.Data.dll`、`OpenCvSharp.dll`）在引用路径中可用。
3. 选择平台配置 `x64 | Release` 或 `x64 | Debug`。
4. 执行 **生成解决方案 (Build Solution)**，编译输出 `SWM.dll`。

---

## 🧪 运行与测试

可通过 PowerShell 脚本或回归测试工程验证 SWM 输出一致性：

```powershell
# 运行回归测试
dotnet run --project SWM.RegressionTests/SWM.RegressionTests.csproj

# 导出参考快照对比
pwsh ./tools/Export-SwmReferenceSnapshot.ps1
```

---

## 📄 许可与版权

Copyright (C) Suzhou HYC Technology Co., LTD. All rights reserved.

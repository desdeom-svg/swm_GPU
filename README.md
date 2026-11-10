# SWM_GPU (Wafer Inspection Scan & Wafer Map) - 晶圆扫描路径规划与 GPU 检测参数引擎

`SWM_GPU` 是半导体晶圆外观缺陷检测系统（Wafer AOI）中的核心业务与参数解算引擎。作为上位机工艺配方（Recipe）与底层 GPU 算法加速管线之间的中枢桥梁，负责解析晶圆物理几何拓扑、规划相机视场切片（Slice）与飞拍触发采集路径（Trigger）、解算复杂参考图（Golden Die / Golden ROI），并最终构建直接输入给 GPU 底层算子的高性能参数流。

---

## 📌 核心架构与功能特性

- **高精度晶圆几何拓扑与 FOV 路径规划**：
  - 基于晶圆尺寸（8寸/12寸）、Die 物理间距、划片槽（Street）以及工业线扫/面阵相机 FOV 规格，自动推导最优扫描切片（Slice）与扫描行（Scan）；
  - 支持双向蛇形往返（Boustrophedon Scanning）运动反向补偿与视场重叠区域（Overlap/Exception Area）亚像素对齐。
- **自适应参考图匹配与 Golden ROI 选择体系**：
  - 支持多层级比对策略：同 Die 内部阵列比对、相邻左/右邻 Die 比对（D2D）、全局标准 Die（Golden Die）比对；
  - 针对边缘 Die（Edge Die / Partial Die）与边缘排除限（Exclusion Limit）提供容错参考图搜索与自动降级方案；
  - 提供细粒度 ROI（IPROI / GoldenROI）几何布尔运算、交集裁剪与无效检测区掩蔽。
- **GPU 算子检测参数构建 (Parameters Engine)**：
  - 深度契合底层 GPU 图像处理动态库的数据契约；
  - 采用高度并行化的内存数据流组装机制，毫秒级完成数万个 Trigger 点位检测参数块的构建与内存对齐。
- **严密的数据审计与回归基准套件**：
  - 内置 `SWM.RegressionTests` 自动化回归工程；
  - 提供参考快照导出工具（`Export-SwmReferenceSnapshot.ps1`）与配方对照脚本，保障工艺版本演进零退化。

---

## 📁 目录结构

```text
WaferInspectionPatternGpu/swm/
├── Base.cs                   # 基础实体、数学结构与坐标转换
├── Die.cs                    # 晶粒 (Die) 物理几何与网格定义
├── GoldenDie.cs              # Golden Die 实体与参考特征池
├── GoldenROI.cs              # Golden ROI 参考区域定义
├── IPROI.cs                  # 图像处理 ROI 区域定义
├── Parameters.cs             # 核心参数生成引擎（底层 C ABI 参数对齐与打包）
├── Scan.cs                   # 扫描点 / 单视野触发实体
├── ScanPlan.cs               # 扫描规划方案管理与序列化
├── Slice.cs                  # 扫描切片实体
├── SWMCore.cs                # 核心拓扑调度、重叠区布尔运算逻辑
├── Wafer.cs                  # 晶圆整盘物理拓扑模型
├── FnXMLSerializer.cs        # 高性能 XML 序列化辅助工具
├── SWM.csproj                # C# 类库工程文件 (.NET Framework 4.8)
├── SWM.sln                   # Visual Studio 解决方案
├── SWM.RegressionTests/      # 回归测试工程
├── tools/                    # 自动化与数据导出脚本
└── README.md                 # 项目架构与技术规格说明
```

---

## 🛠️ 环境要求与构建

### 1. 开发环境
- **操作系统**：Windows 10 / 11 / Server (x64)
- **编译工具**：Visual Studio 2019 / 2022
- **运行时环境**：.NET Framework 4.8
- **核心依赖**：
  - `OpenCvSharp4`
  - `AutoReviewSystem.Data` (上位机通信契约库)

### 2. 编译指南
1. 使用 Visual Studio 打开 `SWM.sln`；
2. 选择编译配置为 `Release | x64`；
3. 执行 **“生成解决方案”**，编译目标将输出至 `bin/Release/SWM.dll`。

### 3. 运行回归测试
```powershell
dotnet test SWM.RegressionTests/SWM.RegressionTests.csproj
```

---

## 🔒 扫描数据排查与安全规范
- 实际现场机台扫描采集生成的大型二进制结果（`*.swmr`, `*.dump` 单文件通常在数十兆至百兆级别）已通过 `.gitignore` 规则强制拦截，仅保留纯净的核心算法源码、回归用例与技术文档。

# HexMap 地形系统

## 项目概述

基于六边形网格的地形生成系统，支持通过噪声图自动生成地形高度与颜色。

## 文件结构

| 文件 | 职责 |
|------|------|
| HexGrid.cs | 地图总控制器，管理 chunk 和 cell 的创建、地形生成、异步初始化 |
| HexGridChunk.cs | 地图块，管理所属 cell 的 mesh 刷新（延迟到 LateUpdate 执行） |
| HexCell.cs | 单个六边形单元，存储坐标、高度、颜色、邻居关系 |
| HexMesh.cs | mesh 三角剖分与生成，处理阶梯化连接、颜色混合 |
| HexMetrics.cs | 常量定义与工具方法，管理噪声图缓存 |
| HexMapEditor.cs | 编辑器交互，支持笔刷修改高度和颜色 |
| HexMapCamera.cs | 摄像机控制 |
| HexCoordinates.cs | 六边形坐标系统（立方坐标 ↔ 偏移坐标） |
| HexCoordinatesDrawer.cs | Inspector 中坐标的显示 |
| HexDirection.cs | 六方向枚举及扩展方法（Opposite/Previous/Next） |
| HexEdgeType.cs | 边缘类型枚举（Flat/Slope/Cliff） |
| EdgeVertices.cs | 一条边上细分后的 5 个顶点及插值工具 |

## 地图结构

```
HexGrid
├── HexGridChunk[] (chunkCountX × chunkCountZ 个)
│   └── HexMesh (三角化渲染)
└── HexCell[] (cellCountX × cellCountZ 个)
    └── 每个 cell 通过 neighbors[] 连接 6 个方向邻居
```

- 每个 chunk 默认 5×5 个 cell
- 总 cell 数 = chunkCountX × chunkCountZ × 25

## 噪声图采样

噪声图通过 Inspector 拖入 `noiseSource`（Texture2D），启动时调用 `GetPixels()` 一次性缓存全部像素到 `Color[]` 数组，后续所有采样均为纯 CPU 数组索引访问。

通道分工：

| 通道 | 用途 |
|------|------|
| R | 颜色 Red |
| G | 颜色 Green |
| B | 颜色 Blue |
| A | 地形高度映射 [minElevation, maxElevation] |

采样入口：`HexMetrics.SampleNoise(Vector3)` — 传入世界坐标，返回 `Vector4(RGBA)`。

## 地形生成流程

1. `HexGrid.Awake()` → 设置噪声源、初始化缓存
2. `CreateChunksAsync()` — UniTask 分帧创建 chunk（每行等一帧），创建后禁用脚本防止空 mesh
3. `CreateCellsAsync()` — UniTask 分帧创建 cell 并建立邻居连接
4. `GenerateTerrain()` — 遍历所有 cell，采样噪声图设置高度和颜色
5. 全部 chunk 调用 `Refresh()` → `enabled = true`
6. `LateUpdate()` 三角化 mesh → `enabled = false`

## 编辑器交互

通过 `HexMapEditor` 支持：

- **颜色绘制** — 选择颜色后点击/拖拽修改 cell 颜色
- **高度修改** — 通过 slider 设置高度等级，笔刷点击/拖拽修改
- **笔刷大小** — 可调整影响范围
- **地形重新生成** — `GenerateTerrain()` 方法可绑定到 UI 按钮触发

## 刷新机制

- **单 cell 编辑** → `HexCell.Refresh()` → 仅刷新该 cell 所属 chunk 及邻居 chunk
- **批量地形生成** → 使用 `SetElevationNoRefresh` / `SetColorNoRefresh` 跳过逐 cell 刷新，最后统一标记所有 chunk
- **延迟三角化** — `HexGridChunk.Refresh()` 仅设置 `enabled = true`，实际 mesh 重建在 `LateUpdate()` 中执行一次后自动禁用

## 高度系统

- `elevationStep = 3f` — 每级高度对应 3 个世界单位
- `elevationPerturbStrength = 1.5f` — 高度扰动强度，基于噪声图采样
- `cellPerturbStrength = 0f` — 水平扰动（当前关闭）
- `terracesPerSlope = 2` — 相邻 cell 高度差 1 时生成 2 级阶梯，共 5 个步长
- 边缘类型：Flat（高度相同）、Slope（高度差 1）、Cliff（高度差 > 1）

## 依赖

- **UniTask** — 用于分帧异步创建 chunk 和 cell
- **Unity 6.0+**

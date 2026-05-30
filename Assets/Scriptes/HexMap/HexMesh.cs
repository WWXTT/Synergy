using UnityEngine;
using System.Collections.Generic;

//依赖MeshFilter和MeshRenderer组件
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class HexMesh : MonoBehaviour
{
    //存储通过vertices计算生成后的mesh
    private Mesh hexMesh;

    //存储所有正六边形的顶点位置信息
    //private List<Vector3> vertices;
    private static List<Vector3> vertices = new List<Vector3>();

    //索引，每个三角面片顶点的渲染顺序
    //private List<int> triangles;
    private static List<int> triangles = new List<int>();

    //为了检测射线碰撞Collider
    private MeshCollider meshCollider;

    //存储cell每个顶点的颜色信息
    //splat 编码下，colors 的 RGB 通道存储 3 个地形的混合权重（可插值，和≈1）
    //private List<Color> colors;
    private static List<Color> colors = new List<Color>();

    //splat 编码：每个顶点携带 3 个地形索引（归一化），存入 UV1。
    //同一三角形 3 个顶点的索引三元组完全相同，插值后保持精确，不会出现索引被线性插值的错误。
    private static List<Vector3> cellIndices = new List<Vector3>();

    //权重基向量：对应索引三元组 (x,y,z) 的三个地形
    private static readonly Color W100 = new Color(1f, 0f, 0f, 1f);
    private static readonly Color W010 = new Color(0f, 1f, 0f, 1f);
    private static readonly Color W001 = new Color(0f, 0f, 1f, 1f);

    //按量化世界位置累加面法线，用于焊接平滑法线（避免合并顶点破坏 splat 数据）
    private static readonly Dictionary<Vector3, Vector3> normalAccum = new Dictionary<Vector3, Vector3>();

    private void Awake()
    {
        //初始化MeshFilter组件的，实例化hexMesh，并给其命名
        GetComponent<MeshFilter>().mesh = hexMesh = new Mesh();
        hexMesh.name = "Hex Mesh";

        //为HexMesh物体添加MeshCollider组件
        meshCollider = gameObject.AddComponent<MeshCollider>();

        //初始化vertices、triangles链表 用于存储顶点和面片信息
        //vertices = new List<Vector3>();
        //triangles = new List<int>();

        //初始化colors链表，用于存储顶点颜色信息
        //colors = new List<Color>();
    }

    /// <summary>
    /// 根据数组长度创建cell的Mesh
    /// </summary>
    /// <param name="cells">存储所有Hex Cell实例的数组</param>
    public void Triangulate(HexCell[] cells)
    {
        //清空原有的数据
        hexMesh.Clear();
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        cellIndices.Clear();

        //依次读取数组中的Hex Cell实例，录入每个Hex Cell的顶点信息
        for (int i = 0; i < cells.Length; i++)
        {
            Triangulate(cells[i]);
        }

        //将所有的顶点位置信息，顶点位置信息的索引存储到链表中
        hexMesh.vertices = vertices.ToArray();
        hexMesh.triangles = triangles.ToArray();

        //将所有顶点的颜色信息存储在colors链表中（splat 权重）
        hexMesh.colors = colors.ToArray();

        //splat 地形索引存入 UV1 通道
        hexMesh.SetUVs(1, cellIndices);

        //重新计算法线
        hexMesh.RecalculateNormals();

        meshCollider.sharedMesh = hexMesh;
    }

    /// <summary>
    /// 按量化世界位置焊接法线：同一位置的所有顶点共享一个由周围所有面累加得到的平滑法线。
    /// 顶点本身不合并，故每个三角形的 UV1 地形索引与顶点色权重都被保留。
    /// </summary>
    private void RecalculateSmoothNormals()
    {
        normalAccum.Clear();

        for (int t = 0; t < triangles.Count; t += 3)
        {
            Vector3 p0 = vertices[triangles[t]];
            Vector3 p1 = vertices[triangles[t + 1]];
            Vector3 p2 = vertices[triangles[t + 2]];

            //与 Unity RecalculateNormals 同向：cross(p1-p0, p2-p0)，未归一化即面积加权
            Vector3 fn = Vector3.Cross(p1 - p0, p2 - p0);
            Accum(NormalKey(p0), fn);
            Accum(NormalKey(p1), fn);
            Accum(NormalKey(p2), fn);
        }

        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            normals[i] = normalAccum[NormalKey(vertices[i])].normalized;
        }
        hexMesh.normals = normals;
    }

    //量化到 0.01 单位栅格，足够细不会误并相邻顶点，又能容忍浮点误差归并同一逻辑点
    private static Vector3 NormalKey(Vector3 p)
    {
        const float q = 100f;
        return new Vector3(Mathf.Round(p.x * q), Mathf.Round(p.y * q), Mathf.Round(p.z * q));
    }

    private static void Accum(Vector3 key, Vector3 n)
    {
        normalAccum.TryGetValue(key, out Vector3 cur);
        normalAccum[key] = cur + n;
    }

    /// <summary>
    /// 使用HexDirection方位，为单个cell循环添加其6个顶点信息
    /// 此方法之后会进行优化合并
    /// </summary>
    /// <param name="cell">单个cell的实例</param>
    private void Triangulate(HexCell cell)
    {
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            Triangulate(d, cell);
        }
    }

    /// <summary>
    /// 通过单个Hex Cell实例，计算其6个顶点位置，并创建三角形面片
    /// </summary>
    /// <param name="cell">单个Hex Cell的实例</param>
    //private void Triangulate(HexCell cell)
    private void Triangulate(HexDirection direction, HexCell cell)
    {
        //获取单个cell的中点位置
        //Vector3 center = cell.transform.localPosition;

        //这里是获cell扰动后的位置
        Vector3 center = cell.Position;

        //通过六边形一条边上的两个端点信息，计算出细分的中间两个点的信息
        EdgeVertices e = new EdgeVertices(
            center + HexMetrics.GetFirstSolidCorner(direction),
            center + HexMetrics.GetSecondSolidCorner(direction)
        );

        //构建三角面片
        TriangulateEdgeFan(center, e, cell.Color);

        //TriangulateConnection方法增加新的参数，自身不在进行顶点的计算了
        if (direction <= HexDirection.SE)
        {
            TriangulateConnection(direction, cell, e);
        }
    }

    /// <summary>
    /// splat：为三角面片的 3 个顶点写入相同的索引三元组 + 各自的权重
    /// </summary>
    private void AddTriangleCellData(Vector3 indices, Color w1, Color w2, Color w3)
    {
        cellIndices.Add(indices);
        cellIndices.Add(indices);
        cellIndices.Add(indices);
        colors.Add(w1);
        colors.Add(w2);
        colors.Add(w3);
    }

    /// <summary>
    /// splat：三角面片 3 个顶点共用同一权重（单一地形）
    /// </summary>
    private void AddTriangleCellData(Vector3 indices, Color weight)
    {
        AddTriangleCellData(indices, weight, weight, weight);
    }

    /// <summary>
    /// splat：两条 A→B 混合权重，按 (1-b, b, 0) 编码
    /// </summary>
    private static Color WeightAB(float b)
    {
        return new Color(1f - b, b, 0f, 1f);
    }

    /// <summary>
    /// 添加单个三角面片的顶点位置信息和索引，顶点带扰动
    /// </summary>
    /// <param name="v1">顺时针 第一个顶点的Vector3</param>
    /// <param name="v2">顺时针 第二个顶点的Vector3</param>
    /// <param name="v3">顺时针 第三个顶点的Vector3</param>
    private void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        //获取当前vertices链表中已经录入的数量
        int vertexIndex = vertices.Count;

        //在vertices链表中添加新增的顶点位置信息
        //vertices.Add(v1);
        //vertices.Add(v2);
        //vertices.Add(v4);

        //这里的坐标变为扰动后的坐标
        vertices.Add(Perturb(v1));
        vertices.Add(Perturb(v2));
        vertices.Add(Perturb(v3));

        //在triangles链表中添加新增顶点信息的索引
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
    }

    /// <summary>
    /// 添加单个三角面片的顶点位置信息和索引，顶点不扰动！
    /// </summary>
    /// <param name="v1">顺时针 第一个顶点的Vector3</param>
    /// <param name="v2">顺时针 第二个顶点的Vector3</param>
    /// <param name="v3">顺时针 第三个顶点的Vector3</param>
    private void AddTriangleUnperturbed(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int vertexIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
    }

    /// <summary>
    /// 创建颜色混合区域的三角面片定点信息和索引，这个区域是一个四边形，所以有4个顶点
    /// </summary>
    /// <param name="v1">三角面片第一个顶点位置信息</param>
    /// <param name="v2">三角面片第二个顶点位置信息</param>
    /// <param name="v3">三角面片第三个顶点位置信息</param>
    /// <param name="v4">三角面片第四个顶点位置信息</param>
    private void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        //获取当前vertices链表中已经录入的数量
        int vertexIndex = vertices.Count;

        //在vertices链表中添加新增的顶点位置信息
        //vertices.Add(v1);
        //vertices.Add(v2);
        //vertices.Add(v4);
        //vertices.Add(v5);

        //这里的坐标变为扰动后的坐标
        vertices.Add(Perturb(v1));
        vertices.Add(Perturb(v2));
        vertices.Add(Perturb(v3));
        vertices.Add(Perturb(v4));

        //在triangles链表中添加新增顶点信息的索引
        //两个三角面片组成了颜色混合区域，分别为：V1V3V2 和 V2V3V4
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 3);
    }

    /// <summary>
    /// splat：四边形 4 个顶点共用同一索引三元组，各自的权重分别赋值
    /// </summary>
    private void AddQuadCellData(Vector3 indices, Color w1, Color w2, Color w3, Color w4)
    {
        cellIndices.Add(indices);
        cellIndices.Add(indices);
        cellIndices.Add(indices);
        cellIndices.Add(indices);
        colors.Add(w1);
        colors.Add(w2);
        colors.Add(w3);
        colors.Add(w4);
    }

    /// <summary>
    /// splat：四边形混合区域只混合 2 个地形，4 个顶点用 2 个权重
    /// </summary>
    private void AddQuadCellData(Vector3 indices, Color w1, Color w2)
    {
        AddQuadCellData(indices, w1, w1, w2, w2);
    }

    /// <summary>
    /// 构建阶梯状连接区域
    /// 这里不再使用单一的顶点，而是直接使用cell与阶梯区域相连接的边，通过计算得出边上的顶点位置以及每个顶点的颜色
    /// </summary>
    /// <param name="begin">第一个cell与相邻阶梯化区域的边上顶点</param>
    /// <param name="beginCell">第一个cell的实例</param>
    /// <param name="end">第二个cell与相邻阶梯化区域的边上顶点</param>
    /// <param name="endCell">第二个cell的实例</param>
    private void TriangulateEdgeTerraces(EdgeVertices begin, HexCell beginCell, EdgeVertices end, HexCell endCell)
    {
        float beginIdx = beginCell.Color.r;
        float endIdx = endCell.Color.r;

        // 第一段
        EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
        float b2 = 1f * HexMetrics.horizontalTerraceStepSize;
        TriangulateEdgeStrip(begin, 0f, e2, b2, beginIdx, endIdx);

        // 中间段
        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            EdgeVertices e1 = e2;
            float b1 = b2;
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            b2 = i * HexMetrics.horizontalTerraceStepSize;
            TriangulateEdgeStrip(e1, b1, e2, b2, beginIdx, endIdx);
        }

        // 最后一段
        TriangulateEdgeStrip(e2, b2, end, 1f, beginIdx, endIdx);
    }


    //这里不再使用单个顶点，而直接使用EdgeVertices进行顶点计算
    private void TriangulateConnection(HexDirection direction, HexCell cell, EdgeVertices e1)
    {
        //HexCell neighbor = cell.GetNeighbor(direction) ?? cell;

        HexCell neighbor = cell.GetNeighbor(direction);

        //当一个方位没有相邻的cell时，不生成双色混合区域
        if (neighbor == null)
        {
            return;
        }

        //先计算出颜色混合区域的高度，在通过v1 v2计算出v3 v5，这样就知道了矩形颜色混合区域的四个顶点了
        Vector3 bridge = HexMetrics.GetBridge(direction);
        //Vector3 v4 = v1 + bridge;
        //Vector3 v5 = v2 + bridge;
        //这里为连接相邻cell的v3 v4顶点加上其所在cell的高度
        //v4.y = v5.y = neighbor.Elevation * HexMetrics.elevationStep;

        //这里在获取相邻cell的位置时，也是使用了扰动后的坐标位置
        //v4.y = v5.y = neighbor.Position.y;

        //这里要计算与矩形连接区域相邻的，另一侧cell新增的两个顶点位置信息
        //Vector3 e3 = Vector3.Lerp(v4, v5, 1f / 3f);
        //Vector3 e4 = Vector3.Lerp(v4, v5, 2f / 3f);

        //先计算出两个相邻cell的高度差
        bridge.y = neighbor.Position.y - cell.Position.y;

        //利用高度差和第一个cell的坐标，获得连接区域另外一边的4个顶点位置
        EdgeVertices e2 = new EdgeVertices(e1.v1 + bridge, e1.v5 + bridge);

        //进行矩形颜色混合区域的三角面片构建
        //AddQuad(v1, v2, v4, v5);
        //AddQuadColor(cell.color, neighbor.color);
        //以上方法注释掉，使用新的 TriangulateEdgeTerraces  进行替换
        //TriangulateEdgeTerraces(v1, v2, cell, v4, v5, neighbor);
        //在这里新加入判断，当两个相邻cell的连接类型为Slope的时候，才会创建阶梯化连接
        if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
        {
            //TriangulateEdgeTerraces(v1, v2, cell, v4, v5, neighbor);

            //这里也使用EdgeVertices计算的顶点来构建矩形
            //TriangulateEdgeTerraces(e1.v1, e1.v5, cell, e2.v1, e2.v5, neighbor);

            //将新的顶点信息传入构建阶梯连接区域的方法中
            TriangulateEdgeTerraces(e1, cell, e2, neighbor);
        }
        else
        {
            //当连接类型不为Slope的时候，连接区域是矩形的
            //AddQuad(v1, v2, v4, v5);
            //AddQuadColor(cell.color, neighbor.color);

            //这里使用新增的顶点进行连接区域的构建
            //AddQuad(v1, e1, v4, e3);
            //AddQuadColor(cell.color, neighbor.color);
            //AddQuad(e1, e2, e3, e4);
            //AddQuadColor(cell.color, neighbor.color);
            //AddQuad(e2, v2, e4, v5);
            //AddQuadColor(cell.color, neighbor.color);

            //这里使用新增的顶点进行连接区域的构建
            TriangulateEdgeStrip(e1, 0f, e2, 1f, cell.Color.r, neighbor.Color.r);
        }

        //获取相邻方位的下一个方位 的cell
        HexCell nextNeighbor = cell.GetNeighbor(direction.Next());

        //这里三个彼此相邻的cell都存在的时候，才会创建三角形混合区域
        //if (nextNeighbor != null)
        //为了避免三角形混合区域的重叠，这里只需要生成NE和E方位的即可
        if (direction <= HexDirection.E && nextNeighbor != null)
        {
            //声明一个新的vector3变量来存储高度改变后的顶点位置
            //v5的本质其实就是v2 + HexMetrics.GetBridge(direction.Next()加上高度值
            //Vector3 v5 = v2 + HexMetrics.GetBridge(direction.Next());

            //这里也使用EdgeVertices计算的顶点来构建矩形
            Vector3 v5 = e1.v5 + HexMetrics.GetBridge(direction.Next());

            //v5.y = nextNeighbor.Elevation * HexMetrics.elevationStep;

            //这里在获取相邻cell的位置时，也是使用了扰动后的坐标位置
            v5.y = nextNeighbor.Position.y;

            //参考图 http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-10-1.png
            //这里要注意，只是要找出3个cell中高度最低的一个
            //因为三角形连接区域的3个cell，其坐标是固定的，找出最低的一个时，其他两个cell的入参顺序就是固定的了

            //注意，教程4.1是有错误的但是最后给的代码是对的，这里注释掉的语句是教程错误的语句
            if (cell.Elevation <= neighbor.Elevation)
            {
                //并且cell1高度小于cell3
                if (cell.Elevation <= nextNeighbor.Elevation)
                {
                    //cell1最低
                    //TriangulateCorner(v2, cell, v5, nextNeighbor, v5, nextNeighbor);
                    //TriangulateCorner(v2, cell, v5, neighbor, v5, nextNeighbor);

                    //这里也使用EdgeVertices计算的顶点来构建矩形
                    TriangulateCorner(e1.v5, cell, e2.v5, neighbor, v5, nextNeighbor);
                }
                else
                {
                    //cell3 最低
                    //TriangulateCorner(v5, nextNeighbor, v2, cell, v5, nextNeighbor);
                    //TriangulateCorner(v5, nextNeighbor, v2, cell, v5, neighbor);

                    //这里也使用EdgeVertices计算的顶点来构建矩形
                    TriangulateCorner(v5, nextNeighbor, e1.v5, cell, e2.v5, neighbor);
                }
            }
            //如果cell1>cell2，且cell2<cell3
            else if (neighbor.Elevation <= nextNeighbor.Elevation)
            {
                //cell2最低
                //TriangulateCorner(v5, nextNeighbor, v5, nextNeighbor, v2, cell);
                //TriangulateCorner(v5, neighbor, v5, nextNeighbor, v2, cell);

                //这里也使用EdgeVertices计算的顶点来构建矩形
                TriangulateCorner(e2.v5, neighbor, v5, nextNeighbor, e1.v5, cell);
            }
            else
            {
                //cell3最低
                //TriangulateCorner(v5, nextNeighbor, v2, cell, v5, nextNeighbor);
                //TriangulateCorner(v5, nextNeighbor, v2, cell, v5, neighbor);

                //这里也使用EdgeVertices计算的顶点来构建矩形
                TriangulateCorner(v5, nextNeighbor, e1.v5, cell, e2.v5, neighbor);
            }

            //v2 + HexMetrics.GetBridge(direction.Next()) 为三角形的最后一个顶点位置
            //首先通过HexMetrics.GetBridge(direction.Next()获取 相邻的第二个cell的矩形连接区域宽度，可以理解为一个向量
            //v2顶点位置再加上这个向量，得出了三角形最后一个顶点的位置
            //AddTriangle(v2, v5, v2 + HexMetrics.GetBridge(direction.Next()));

            //AddTriangle(v2, v5, v5);
            //AddTriangleColor(cell.color, neighbor.color, nextNeighbor.color);
        }
    }

    /// <summary>
    /// 阶梯化矩形连接区域
    /// </summary>
    /// <param name="beginLeft">cell到neighbor连接区域的第一个起点</param>
    /// <param name="beginRight">cell到neighbor连接区域的第二个起点</param>
    /// <param name="beginCell">cell自身实例，用于获取颜色</param>
    /// <param name="endLeft">连接区域 连接到的neighbor的第一个终点</param>
    /// <param name="endRight">连接区域 连接到的neighbor的第二个终点</param>
    /// <param name="endCell">连接到的neighbor实例，用于获取颜色</param>
    //private void TriangulateEdgeTerraces(
    //Vector3 beginLeft, Vector3 beginRight, HexCell beginCell,
    //Vector3 endLeft, Vector3 endRight, HexCell endCell)
    //{
    //    //这里先生成阶梯的第一个矩形面片。通过给定插值来计算出矩形面片的另外两个顶点
    //    Vector3 v4 = HexMetrics.TerraceLerp(beginLeft, endLeft, 1);
    //    Vector3 v5 = HexMetrics.TerraceLerp(beginRight, endRight, 1);
    //    Color c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, 1);

    //    AddQuad(beginLeft, beginRight, v4, v5);
    //    AddQuadColor(beginCell.Color, c2);

    //    //阶梯的其他矩形面片，可以通过循环来生成
    //    //旧的矩形面片终点V3 V4，就是新面片的起点 V1 V2
    //    //然后再利用插值计算新面片的终点即可
    //    //颜色计算同理
    //    for (int i = 2; i < HexMetrics.terraceSteps; i++)
    //    {
    //        Vector3 v1 = v4;
    //        Vector3 v2 = v5;
    //        Color c1 = c2;
    //        v4 = HexMetrics.TerraceLerp(beginLeft, endLeft, i);
    //        v5 = HexMetrics.TerraceLerp(beginRight, endRight, i);
    //        c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, i);
    //        AddQuad(v1, v2, v4, v5);
    //        AddQuadColor(c1, c2);
    //    }

    //    //连接阶梯的剩余区域
    //    AddQuad(v4, v5, endLeft, endRight);
    //    AddQuadColor(c2, endCell.Color);
    //}

    /// <summary>
    /// 构建三角形连接区域的方法
    /// 判断相邻3个cell高低的工作，在TriangulateConnection方法中实现了，这里只负责创建连接区域
    /// 注意，TriangulateConnection方法只是对入参的顺序做了调整，但是并没有告知3个cell之间相对的连接类型
    /// 所以要在这个方法中对连接类型进行判断，这样才能决三角形连接区域定用什么方式进行三角剖分
    /// </summary>
    /// <param name="bottom">bottom cell的坐标</param>
    /// <param name="bottomCell">bottom cell的实例</param>
    /// <param name="left">left cell的坐标</param>
    /// <param name="leftCell">left cell的实例</param>
    /// <param name="right">right cell的坐标</param>
    /// <param name="rightCell">right cell的实例</param>
    private void TriangulateCorner(Vector3 bottom, HexCell bottomCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        //这里先获取Left和Right两个cell，相较于Bottom cell的高度类型，这样才能决定怎样做三角剖分
        HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
        HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

        //这里通过获取的Left和Right 相较于Bottom的连接类型进行判断，具体三个cell的高度关系
        //判断完成后，直接调用对应的方法构建三角形连接区域，而不使用之前通用的方法构建
        if (leftEdgeType == HexEdgeType.Slope)
        {
            //这是SSF类型正常情况，即2个cell高度为1，一个cell高度为0
            if (rightEdgeType == HexEdgeType.Slope)
            {
                //这里判断为SSF类型
                TriangulateCornerTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
            }

            //SSF变体1 即2个cell高度为0，一个cell高度为1，且高度为1的cell在左侧
            else if (rightEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(left, leftCell, right, rightCell, bottom, bottomCell);
            }
            else
            {
                //Slope-Cliff连接类型
                //bottom最低，left比bottom高1，right比bottom高1及以上
                TriangulateCornerTerracesCliff(bottom, bottomCell, left, leftCell, right, rightCell);
            }
        }

        else if (rightEdgeType == HexEdgeType.Slope)
        {
            //SSF变体2 即2个cell高度为0，一个cell高度为1，且高度为1的cell在右侧
            if (leftEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
            }
            else
            {
                //Slope-Cliff连接 镜像 类型
                //bottom最低，right比bottom高1，left比right高1及以上
                TriangulateCornerCliffTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
            }
        }

        //bottom最低，与left和right高差都大于1，并且left和right高差为1，称为 CCS类型
        //如果left比right高1，那么就是CCSL，反之right比left高1，那就是CCSR
        else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            //CCSR
            if (leftCell.Elevation < rightCell.Elevation)
            {
                TriangulateCornerCliffTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
            }
            //CCSL
            else
            {
                TriangulateCornerTerracesCliff(left, leftCell, right, rightCell, bottom, bottomCell);
            }
        }
        else
        {
            //纯三角形连接区域（无阶梯化）
            //splat：三个 cell 的索引存入三元组，三个顶点各取对应基向量权重
            AddTriangle(bottom, left, right);
            Vector3 indices = new Vector3(bottomCell.Color.r, leftCell.Color.r, rightCell.Color.r);
            AddTriangleCellData(indices, W100, W010, W001);
        }
    }

    /// <summary>
    /// 针对SSF组合类型 创建阶梯状的三角形连接区域
    /// SFF及其变体的组合，参考图
    /// http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-11-1.png
    /// http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-12-1.png
    /// </summary>
    /// <param name="begin">初始cell位置</param>
    /// <param name="beginCell">初始cell实例</param>
    /// <param name="left">左侧cell位置</param>
    /// <param name="leftCell">左侧cell实例</param>
    /// <param name="right">右侧cell位置</param>
    /// <param name="rightCell">右侧cell实例</param>
    private void TriangulateCornerTerraces(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        //splat：索引三元组 (begin, left, right)；权重沿阶梯从 begin 分别插值到 left / right
        Vector3 indices = new Vector3(beginCell.Color.r, leftCell.Color.r, rightCell.Color.r);

        Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);

        float h = 1f * HexMetrics.horizontalTerraceStepSize;
        Color w3 = Color.Lerp(W100, W010, h);
        Color w4 = Color.Lerp(W100, W001, h);

        AddTriangle(begin, v3, v4);
        AddTriangleCellData(indices, W100, w3, w4);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color w1 = w3;
            Color w2 = w4;
            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            h = i * HexMetrics.horizontalTerraceStepSize;
            w3 = Color.Lerp(W100, W010, h);
            w4 = Color.Lerp(W100, W001, h);
            AddQuad(v1, v2, v3, v4);
            AddQuadCellData(indices, w1, w2, w3, w4);
        }

        AddQuad(v3, v4, left, right);
        AddQuadCellData(indices, w3, w4, W010, W001);
    }

    /// <summary>
    /// 针对Slope-Cliff连接类型 创建三角形连接区域
    /// 参考图 http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-13-3.png
    /// </summary>
    /// <param name="begin">初始cell位置</param>
    /// <param name="beginCell">初始cell实例</param>
    /// <param name="left">左侧cell位置</param>
    /// <param name="leftCell">左侧cell实例</param>
    /// <param name="right">右侧cell位置</param>
    /// <param name="rightCell">右侧cell实例</param>
    private void TriangulateCornerTerracesCliff(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        //splat：索引三元组 (begin, left, right)
        Vector3 indices = new Vector3(beginCell.Color.r, leftCell.Color.r, rightCell.Color.r);

        float b = 1f / (rightCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;

        Vector3 boundary = Vector3.Lerp(Perturb(begin), Perturb(right), b);
        // boundary 位于 begin→right 之间，权重由 W100 向 W001 插值
        Color boundaryWeights = Color.Lerp(W100, W001, b);

        TriangulateBoundaryTriangle(begin, W100, left, W010, boundary, boundaryWeights, indices);

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices);
        }
        else
        {
            AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
            AddTriangleCellData(indices, W010, W001, boundaryWeights);
        }
    }

    /// <summary>
    /// Slope-Cliff连接类型种 创建底部区域
    /// 这里将Slope-Cliff的三角形连接区域分为了两部分
    /// 当两个cell高度差为1时，上下均要从边界点进行阶梯化
    /// 当两个cell高度差大于1时，只需要细分下半部分即可
    /// 参考图 http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-15-1.png
    /// http://magi-melchiorl.gitee.io/pages/Pics/Hexmap/3-15-2.png
    /// </summary>
    /// <param name="begin">初始cell位置</param>
    /// <param name="beginCell">初始cell实例</param>
    /// <param name="left">左侧cell位置</param>
    /// <param name="leftCell">左侧cell实例</param>
    /// <param name="right">Cliff斜面的分界点位置</param>
    /// <param name="rightCell">Cliff斜面的分界点的颜色</param>
    private void TriangulateBoundaryTriangle(
        Vector3 begin, Color beginWeights,
        Vector3 left, Color leftWeights,
        Vector3 boundary, Color boundaryWeights, Vector3 indices)
    {
        Vector3 v2 = Perturb(HexMetrics.TerraceLerp(begin, left, 1));
        float h = 1f * HexMetrics.horizontalTerraceStepSize;
        Color w2 = Color.Lerp(beginWeights, leftWeights, h);

        AddTriangleUnperturbed(Perturb(begin), v2, boundary);
        AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            Vector3 v1 = v2;
            Color w1 = w2;
            v2 = Perturb(HexMetrics.TerraceLerp(begin, left, i));
            h = i * HexMetrics.horizontalTerraceStepSize;
            w2 = Color.Lerp(beginWeights, leftWeights, h);
            AddTriangleUnperturbed(v1, v2, boundary);
            AddTriangleCellData(indices, w1, w2, boundaryWeights);
        }

        AddTriangleUnperturbed(v2, Perturb(left), boundary);
        AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
    }

    /// <summary>
    /// 针对Slope-Cliff连接 镜像 类型 创建三角形连接区域
    /// </summary>
    /// <param name="begin">初始cell位置</param>
    /// <param name="beginCell">初始cell实例</param>
    /// <param name="left">左侧cell位置</param>
    /// <param name="leftCell">左侧cell实例</param>
    /// <param name="right">右侧cell位置</param>
    /// <param name="rightCell">右侧cell实例</param>
    private void TriangulateCornerCliffTerraces(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        //splat：索引三元组 (begin, left, right)
        Vector3 indices = new Vector3(beginCell.Color.r, leftCell.Color.r, rightCell.Color.r);

        float b = 1f / (leftCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;

        Vector3 boundary = Vector3.Lerp(Perturb(begin), Perturb(left), b);
        // boundary 位于 begin→left 之间，权重由 W100 向 W010 插值
        Color boundaryWeights = Color.Lerp(W100, W010, b);

        TriangulateBoundaryTriangle(right, W001, begin, W100, boundary, boundaryWeights, indices);

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices);
        }
        else
        {
            AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
            AddTriangleCellData(indices, W010, W001, boundaryWeights);
        }
    }

    /// <summary>
    /// 通过世界内的一个点(vector3)，经过彩色噪点图扰动后，返回扰动后的Vect3
    /// </summary>
    /// <param name="position">世界坐标内的点</param>
    /// <returns>经过噪点图扰动后的点坐标</returns>
    private Vector3 Perturb(Vector3 position)
    {
        //利用世界空间内一点，在彩色噪点图上进行采样，得到彩色噪点图内一点的RGBA信息
        Vector4 sample = HexMetrics.SampleNoise(position);

        //增加了每个点的扰动强度
        position.x += (sample.x * 2f - 1f) * HexMetrics.cellPerturbStrength;
        //为了让cell表面变得平坦，这里不再在垂直方向上进行扰动。
        //position.y += (sample.y * 2f - 1f) * HexMetrics.cellPerturbStrength;
        position.z += (sample.z * 2f - 1f) * HexMetrics.cellPerturbStrength;

        return position;
    }

    /// <summary>
    /// 使用计算好的5个顶点，对cell的六边形其中一个三角面片进行细分
    /// splat：单一地形，索引三元组 (idx,idx,idx)，权重恒为 (1,0,0)
    /// </summary>
    /// <param name="center">cell中心点位置</param>
    /// <param name="edge">一条边上细分后的5个顶点信息</param>
    /// <param name="color">cell的颜色（R=地形索引）</param>
    private void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color)
    {
        float idx = color.r;
        Vector3 indices = new Vector3(idx, idx, idx);

        AddTriangle(center, edge.v1, edge.v2);
        AddTriangleCellData(indices, W100);

        AddTriangle(center, edge.v2, edge.v3);
        AddTriangleCellData(indices, W100);
        AddTriangle(center, edge.v3, edge.v4);
        AddTriangleCellData(indices, W100);

        AddTriangle(center, edge.v4, edge.v5);
        AddTriangleCellData(indices, W100);
    }

    /// <summary>
    /// 创建2个cell之间细分后的连接区域（splat 编码）
    /// 索引三元组 = (idxA, idxB, idxB)；权重在 e1 边为 WeightAB(b1)，e2 边为 WeightAB(b2)。
    /// 平地连接 b1=0、b2=1；阶梯连接由 TriangulateEdgeTerraces 传入各段的混合比例。
    /// </summary>
    private void TriangulateEdgeStrip(EdgeVertices e1, float b1, EdgeVertices e2, float b2, float idxA, float idxB)
    {
        Vector3 indices = new Vector3(idxA, idxB, idxB);
        Color w1 = WeightAB(b1);
        Color w2 = WeightAB(b2);

        AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
        AddQuadCellData(indices, w1, w2);
        AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
        AddQuadCellData(indices, w1, w2);
        AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
        AddQuadCellData(indices, w1, w2);
        AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
        AddQuadCellData(indices, w1, w2);
    }

}
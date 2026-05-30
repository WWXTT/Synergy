using UnityEngine;
using Cysharp.Threading.Tasks;

#pragma warning disable 649

public class HexGrid : MonoBehaviour
{
    //这两个变量的值可以通过地图中有几个chunk和每个chunk的尺寸计算出来
    private int cellCountX;
    private int cellCountZ;

    //存放地图单元格的预置
    public HexCell cellPrefab;

    //存放所有实例化的地图单元
    private HexCell[] cells;

    //彩色噪点图的实例，直接将图片拖拽至Inspector面板对应位置赋初始值
    public Texture2D noiseSource;

    //噪声采样设置
    [Header("Noise Sampling")]
    //采样范围：读取噪声图的窗口宽度（<1 即缩小采样范围，纹理特征被放大）
    [Range(0.05f, 1f)] public float noiseSampleRange = 1f;
    //随机种子：决定在噪声图上的采样起点位置
    public int noiseSeed = 0;

    //定义整个地图长宽各有多少个chunk
    public int chunkCountX = 1;
    public int chunkCountZ = 1;

    //地形生成高低落差参数
    [Header("Terrain Generation")]
    public int maxElevation = 6;

    //地形类型参数
    [Header("Terrain Types")]
    public int terrainTypeCount = 8;

    //颜色查找表 — 每种地形的参考颜色，Inspector 中配置
    [Header("Terrain Color Lookup")]
    public Color[] terrainTypeColors = new Color[]
    {
        new Color(0.2f, 0.5f, 0.1f),  // 0: 草地
        new Color(0.4f, 0.3f, 0.1f),  // 1: 泥土
        new Color(0.5f, 0.5f, 0.5f),  // 2: 岩石
        new Color(0.8f, 0.75f, 0.5f), // 3: 沙地
        new Color(0.9f, 0.9f, 0.95f), // 4: 雪地
        new Color(0.1f, 0.3f, 0.6f),  // 5: 水域
        new Color(0.3f, 0.2f, 0.1f),  // 6: 深土
        new Color(0.6f, 0.6f, 0.3f),  // 7: 干草
    };

    //逻辑变成了 地图初始化 -> 创建chunk ->创建cell
    //这里要先引用Chunk的Prefab
    public HexGridChunk chunkPrefab;

    //用来存储实例化的chunk
    private HexGridChunk[] chunks;

    private void Awake()
    {
        HexMetrics.noiseSource = noiseSource;
        ApplyNoiseSampling();
        HexMetrics.InitializeNoiseCache();

        cellCountX = chunkCountX * HexMetrics.chunkSizeX;
        cellCountZ = chunkCountZ * HexMetrics.chunkSizeZ;

        // 写入 chunk 世界尺寸全局常量，shader 据此让贴图整图铺满一个 chunk
        float chunkWorldX = HexMetrics.chunkSizeX * (HexMetrics.innerRadius * 2f);
        float chunkWorldZ = HexMetrics.chunkSizeZ * (HexMetrics.outerRadius * 1.5f);
        Shader.SetGlobalVector("_ChunkWorldSize", new Vector4(chunkWorldX, chunkWorldZ, 0f, 0f));

        InitializeAsync().Forget();
    }

    private async UniTaskVoid InitializeAsync()
    {
        await CreateChunksAsync();
        await CreateCellsAsync();
        GenerateTerrain();
    }

    private async UniTask CreateChunksAsync()
    {
        chunks = new HexGridChunk[chunkCountX * chunkCountZ];
        for (int z = 0, i = 0; z < chunkCountZ; z++)
        {
            for (int x = 0; x < chunkCountX; x++)
            {
                HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
                chunk.transform.SetParent(transform);
                chunk.gameObject.SetActive(true);
                // 禁用脚本，防止 LateUpdate 在 cells 填充前执行
                chunk.enabled = false;
            }
            await UniTask.Yield();
        }
    }

    private async UniTask CreateCellsAsync()
    {
        cells = new HexCell[cellCountZ * cellCountX];

        for (int z = 0, i = 0; z < cellCountZ; z++)
        {
            for (int x = 0; x < cellCountX; x++)
            {
                CreateCell(x, z, i++);
            }
            // 每创建一行cell等待一帧
            await UniTask.Yield();
        }
    }

    public void GenerateTerrain()
    {
        if (noiseSource == null)
        {
            Debug.LogWarning("Noise source texture is not assigned!");
            return;
        }

        // 应用当前采样范围/种子（编辑器内改参数后点生成即可生效）
        ApplyNoiseSampling();

        for (int i = 0; i < cells.Length; i++)
        {
            HexCell cell = cells[i];
            Vector3 cellPosition = cell.transform.localPosition;

            // 使用已缓存的 SampleNoise（避免 GPU→CPU 传输）
            Vector4 noiseSample = HexMetrics.SampleNoise(cellPosition);

            // 透明通道映射到 [minElevation, maxElevation]
            int elevation = Mathf.RoundToInt(noiseSample.w * (maxElevation));
            elevation = Mathf.Clamp(elevation, 0, maxElevation);

            cell.SetElevationNoRefresh(elevation);

            // 将噪声颜色映射为地形类型索引，原始索引存入 cell（splat：UV1 直接取用，无需解码）
            int terrainIdx = ColorToTerrainIndex(noiseSample.x, noiseSample.y, noiseSample.z);
            cell.SetColorNoRefresh(new Color(terrainIdx, terrainIdx, 0f, 1f));
        }

        // 标记所有 chunk 需要刷新
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].Refresh();
        }
    }

    /// <summary>
    /// 把采样范围与随机种子写入 HexMetrics：缩小采样窗口，并由种子决定窗口在噪声图上的起点
    /// </summary>
    private void ApplyNoiseSampling()
    {
        float range = Mathf.Clamp(noiseSampleRange, 0.05f, 1f);
        HexMetrics.noiseSampleRange = range;

        // 窗口必须落在 [0,1) 内，故起点上限为 (1 - range)。用种子做确定性随机。
        float span = 1f - range;
        var rng = new System.Random(noiseSeed);
        float ox = (float)rng.NextDouble() * span;
        float oy = (float)rng.NextDouble() * span;
        HexMetrics.noiseSampleOrigin = new Vector2(ox, oy);
    }

    /// <summary>
    /// 通过最近颜色匹配，将噪声颜色映射为地形类型索引
    /// </summary>
    private int ColorToTerrainIndex(float r, float g, float b)
    {
        if (terrainTypeColors == null || terrainTypeColors.Length == 0) return 0;

        // terrainTypeCount 限制可产生的最大地形索引：只在前 N 个参考颜色中匹配
        int usable = Mathf.Clamp(terrainTypeCount, 1, terrainTypeColors.Length);
        float minDist = float.MaxValue;
        int bestIdx = 0;
        for (int i = 0; i < usable; i++)
        {
            Color c = terrainTypeColors[i];
            float dr = r - c.r, dg = g - c.g, db = b - c.b;
            float dist = dr * dr + dg * dg + db * db;
            if (dist < minDist)
            {
                minDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private void OnEnable()
    {
        //Unity在Play模式中，Awake只会在脚本被实例化时调用一次，如果之后噪点图改变了，没办法重新为静态变量赋值
        //所以这里再次进行赋值，之后只要disable后在enable，静态变量就会被重新赋值
        HexMetrics.noiseSource = noiseSource;
    }

    private void Update()
    {
        //之后鼠标点击交互相关代码会移动到其他脚本中
        //检测鼠标左键是否点击
        //此方法移动到了HexMapEditor中
        //if (Input.GetMouseButtonUp(0))
        //{
        //    HandleInput();
        //}
    }

    /// <summary>
    /// 鼠标左键单击会调用此方法，以鼠标为发射点，经过主摄像机练成射线
    /// 检测射线穿过Collider的位置
    /// 此方法移动到了HexMapEditor中
    /// </summary>
    //private void HandleInput()
    //{
    //    //射线起点为鼠标位置，经过主摄像机
    //    Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);

    //    //检测射线是否碰撞到了collider
    //    RaycastHit hit;
    //    if (Physics.Raycast(inputRay, out hit))
    //    {
    //        TouchCell(hit.point);
    //    }
    //}

    /// <summary>
    /// 将射线的触碰点转换到自身的坐标系中
    /// </summary>
    /// <param name="position">触碰到的collider的位置</param>
   /* private void TouchCell(Vector3 position)
    {
        //将触碰点的坐标系，转换到自身的坐标系
        position = transform.InverseTransformPoint(position);

        //string strtmp= "原始坐标为" + position.ToString();
        //LoggerTool.LogMessage(strtmp);

        //调用转换坐标的方法，定位具体点击到哪个cell上了
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);

        //Debug.Log(coordinates.ToString());

        //计算出cell位于cells[]数组中的位置
        //在四边形网格中就是X+Z乘以宽度，但在这里还需要加上一半的Z轴偏移。????
        int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;

        //Debug.Log(index);

        //获取这个cell的实例
        HexCell cell = cells[index];

        //为这个cell赋值颜色
        cell.Color = touchedColor;

        //重新构建整个map的mesh
        //hexMesh.Triangulate(cells);

        //Debug.Log("touched at " + coordinates.ToString());

        //Debug.Log("touched at " + position);
        //Debug.Log("<color=#00FF00>原始坐标为" + position + "</color>");
    }*/

    /// <summary>
    /// 为被点击的cell赋值对应的颜色
    /// </summary>
    /// <param name="_position">鼠标点击hexmap的位置</param>
    /// <param name="_color">选中的颜色</param>
    //public void ColorCell(Vector3 _position, Color _color)
    //{
    //    //将鼠标点击的位置，转换到Hexmap的位置上，为unity坐标
    //    _position = transform.InverseTransformPoint(_position);
    //    //将Unity坐标转换为Hexmap坐标
    //    HexCoordinates _coordinates = HexCoordinates.FromPosition(_position);
    //    //通过计算的Hexmap坐计算出cell的索引
    //    int _index = _coordinates.X + _coordinates.Z * width + _coordinates.Z / 2;
    //    //通过索引在cells数组中找到这个cell的实例
    //    HexCell _cell = cells[_index];
    //    //为这个cell的实例赋值颜色
    //    _cell.color = _color;
    //    //重新构建所有的cell
    //    //这里注意，每次进行颜色的改变，都会重新构建整个cells数组，这个遗留问题之后会修正
    //    hexMesh.Triangulate(cells);
    //}


    /// <summary>
    /// 通过鼠标点击的Unity坐标值，来获取被点击cell的实例
    /// </summary>
    /// <param name="_position">鼠标点击hexmap的位置</param>
    /// <returns>对应被点击位置的cell实例</returns>
    public HexCell GetCell(Vector3 position)
    {
        position = transform.InverseTransformPoint(position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);
        int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;

        //返回被点击cell的实例
        return cells[index];
    }

    /// <summary>
    /// 通过hexmap中的坐标，来获取cell的实例
    /// </summary>
    /// <param name="coordinates">hexmap中cell的坐标</param>
    /// <returns>对应hexmap坐标值的cell实例</returns>
    public HexCell GetCell(HexCoordinates coordinates)
    {
        //为了避免产生数组越界，这里要先检查X和Z坐标是否在范围内
        int z = coordinates.Z;
        if (z < 0 || z >= cellCountZ)
        {
            return null;
        }

        int x = coordinates.X + z / 2;
        if (x < 0 || x >= cellCountX)
        {
            return null;
        }

        return cells[x + z * cellCountX];
    }

    /// <summary>
    /// 重新构建整个地图
    /// </summary>
    //public void Refresh()
    //{
    //    hexMesh.Triangulate(cells);
    //}

    /// <summary>
    /// 创建一个地图单元
    /// </summary>
    /// <param name="x">地图单元是 横行中的第几个</param>
    /// <param name="z">地图单元是 纵列中的第几个</param>
    /// <param name="i">地图单元在cells数组中的索引</param>
    private void CreateCell(int x, int z, int i)
    {
        //声明一个Vector3，根据这个Cell在数组中的位置，计算其在游戏场景中的实际位置
        Vector3 position;

        //所以在偶数行正好抵消了偏移量，而在奇数行，z * 0.5f - z / 2 * (HexMetrics.innerRadius * 2f)正好是一个内切圆半径长度
        position.x = (x + z * 0.5f - z / 2) * (HexMetrics.innerRadius * 2f);

        position.y = 0f;

        //position.z = z * 10f;////正方形Cell时，两个cell的垂直间距
        position.z = z * (HexMetrics.outerRadius * 1.5f);//两个正六边形Cell中点的垂直间距

        //在数组cells的i位置实例化地图单元
        //cell用来给这个被实例化的单元设置父级和位置
        HexCell cell;
        cells[i] = Instantiate<HexCell>(cellPrefab);
        cell = cells[i];

        //设置被实例化地图单元的父级和位置
        //这里不再对cell实例设置父级，将其分配到对应chunk后，由chunk进行实例父级的设置
        //cell.transform.SetParent(transform, false);
        cell.transform.localPosition = position;

        //在不改变cell排列的情况下，重新计算每个cell的坐标位置
        cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);

        //以下为将 周围cell与自身相链接的代码部分----------------------------------------
        //判断cell是否为每一行第一个
        //如果不是第一个，则cell会有W方位相邻的cell，就可以建立E-W链接
        if (x > 0)
        {
            //cells[i - 1]即为其左侧的cell
            cell.SetNeighbor(HexDirection.W, cells[i - 1]);
        }

        //注意，这里行数索引是从0开始，也就是说，实际看到的第一行索引是0，也就是说起始是偶数行
        //在使用SetNeighbor方法进行cell的链接时，自身和对应cell会相互建立连接
        //所以，这里选择除了第一行，其他行都只进行SE和SW方向的链接，再加上之前的W方向，其实就完成了所有6个方向的相互链接
        if (z > 0)
        {
            //这里的&为位运算符 MSDN：https://docs.microsoft.com/zh-cn/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators
            //这里使用位运算符，判断是否为偶数行
            if ((z & 1) == 0)
            {
                //当为偶数行的时候，创建 SE-NW 方向的链接
                //cells[i - width]为SE方向的实例，也就是右下方的cell
                cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);

                //每行的第一个cell是没有左下角(SW)方向的链接，这里要判断cell是否为第一个
                if (x > 0)
                {
                    //cells[i - width - 1]为SW方向的实例，也就是左下方的cell，创建SW-NE方向的链接
                    cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
                }
            }
            //这里是奇数行建立链接的部分
            else
            {
                //i - width 为自身SW方向的实例
                cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);

                //判断奇数行cell是否为每行最后一个，因为奇数行最后一个cell是没有SE方向的实例
                if (x < cellCountX - 1)
                {
                    //i - width + 1 为奇数行自身SE方向的实例
                    cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
                }
            }
        }

        //在地图初始状态下，每个cell的海拔高度都经过扰动
        cell.Elevation = 0;

        AddCellToChunk(x, z, cell);
    }

    /// <summary>
    /// 通过cell在数组中的坐标计算后，将其分配到对应的chunk中
    /// </summary>
    /// <param name="x">cell在整体数组中的横坐标</param>
    /// <param name="z">cell在整体数组中的纵坐标</param>
    /// <param name="cell">cell自身的实例</param>
    private void AddCellToChunk(int x, int z, HexCell cell)
    {
        //通过cell整体数组的横纵坐标，计算出cell属于哪个chunk
        int chunkX = x / HexMetrics.chunkSizeX;
        int chunkZ = z / HexMetrics.chunkSizeZ;

        //通过计算得到的坐标，获取对应chunk的实例
        HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

        //通过cell整体数组坐标，计算出其在对应chunk数组中的下标
        int localX = x - chunkX * HexMetrics.chunkSizeX;
        int localZ = z - chunkZ * HexMetrics.chunkSizeZ;

        //得到下标后，将cell实例添加到对应chunk的数组中
        chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);
    }

    }
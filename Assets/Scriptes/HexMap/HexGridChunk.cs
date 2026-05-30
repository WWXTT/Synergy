using UnityEngine;

#pragma warning disable 649

public class HexGridChunk : MonoBehaviour
{
    private HexCell[] cells;

    [SerializeField] private HexMesh hexMesh;

    private void Awake()
    {
        cells = new HexCell[HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ];
    }

    public void AddCell(int index, HexCell cell)
    {
        cells[index] = cell;
        cell.chunk = this;
        cell.transform.SetParent(transform, false);
    }

    public void Refresh()
    {
        enabled = true;
    }

    private void LateUpdate()
    {
        hexMesh.Triangulate(cells);
        enabled = false;
    }
}

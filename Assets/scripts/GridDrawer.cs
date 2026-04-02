using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridDrawer : MonoBehaviour
{
    public int gridSize = 20;      // размер сетки
    public float cellSize = 1f;    // размер клетки

    void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;

        for (int x = -gridSize; x <= gridSize; x++)
        {
            Vector3 start = new Vector3(x * cellSize, 0, -gridSize * cellSize);
            Vector3 end = new Vector3(x * cellSize, 0, gridSize * cellSize);

            Gizmos.DrawLine(start, end);
        }

        for (int z = -gridSize; z <= gridSize; z++)
        {
            Vector3 start = new Vector3(-gridSize * cellSize, 0, z * cellSize);
            Vector3 end = new Vector3(gridSize * cellSize, 0, z * cellSize);

            Gizmos.DrawLine(start, end);
        }
    }
}

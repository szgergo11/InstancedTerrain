using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Unity.Mathematics;
using System.IO;

[CreateAssetMenu(fileName = "TerrainElementData", menuName = "Instanced Terrain/Terrain Element Data", order = 0)]
public class TerrainElementData : ScriptableObject
{
    [System.Serializable]
    public class LocalCell
    {
        // Cell's extent in the y direction from center
        public float yMax;
        public float yMin;

        public List<Matrix4x4> matrices;
    }

    public Material material;
    public Mesh renderMesh;

    public Vector3 boundingBoxSize;

    [Min(0.00000001f)]
    public float boundingBoxSizeMultiplier = 1f;

    // Key is x and z(y) on the cells' 2d grid
    // Storing in a json file is more flexible (runtime modifications, etc.)
    [System.NonSerialized]
    public SDictionary teMatricesCellOrder = new SDictionary();

    // Stored for later error messages (like manager's celldata and itds' cellsize isn't matching)
    public Vector3 CellSize 
    { 
        get => latestCellSize; 
    }

    public Vector3 MultipliedBoundingBoxSize
    {
        get => boundingBoxSizeMultiplier * boundingBoxSize;
    }

    public Vector3 MultipliedBoundingBoxExtents
    {
        get => 0.5f * boundingBoxSizeMultiplier * boundingBoxSize;
    }

    [HideInInspector]
    private Vector3 latestCellSize = Vector3.zero;

    [HideInInspector]
    public bool changedCellStructure = false;

    /// <summary>
    /// Use for json location
    /// </summary>
    [HideInInspector]
    private string assetPath;

    private string JsonPath
    {
        get => Application.streamingAssetsPath + "/InstancedTerrain/" + assetPath + ".json";
    }



    public TerrainElementData()
    {

    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        assetPath = UnityEditor.AssetDatabase.GetAssetPath(this).Replace("/", "");
#endif
        LoadMatrices();
    }

    private void OnDisable()
    {
        SaveMatrices();
    }

    public void SaveMatrices()
    {
        Debug.Log("Saved terrain element data matrices");
        File.WriteAllText(JsonPath, JsonUtility.ToJson(teMatricesCellOrder));
    }

    public void LoadMatrices()
    {
        Debug.Log("Loaded terrain element data matrices");
        teMatricesCellOrder = JsonUtility.FromJson<SDictionary>(File.ReadAllText(JsonPath));
    }

    public void ClearPositions()
    {
        teMatricesCellOrder.Clear();
    }

    public void AddElement(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if(latestCellSize == Vector3.zero)
        {
            Debug.LogError("CellSize not set. Please set CellSize property before calling AddPosition");
            return;
        }

        int x = Mathf.FloorToInt(position.x / CellSize.x);
        int y = Mathf.FloorToInt(position.y / CellSize.y);
        int z = Mathf.FloorToInt(position.z / CellSize.z);
        int3 key = new int3(x, y, z);
        if (teMatricesCellOrder.ContainsKey(key))
        {
            var lc = teMatricesCellOrder[key];

            if (lc.yMax < position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier)
                lc.yMax = position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier;

            if (lc.yMin > position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier)
                lc.yMin = position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier;

            lc.matrices.Add(Matrix4x4.TRS(position, rotation, scale));
        }
        else
        {
            teMatricesCellOrder.Add(key, new LocalCell
            {
                matrices = new List<Matrix4x4>() { Matrix4x4.TRS(position, rotation, scale) },
                yMax = position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier,
                yMin = position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier
            });
        }
        changedCellStructure = true;
    }

    public void AddElement(Matrix4x4 matrix)
    {
        Vector3 position = new Vector3(matrix[0, 3], matrix[1, 3], matrix[2, 3]);

        if (latestCellSize == Vector3.zero)
        {
            Debug.LogError("CellSize not set. Please set CellSize property before calling AddPosition");
            return;
        }

        int x = Mathf.FloorToInt(position.x / CellSize.x);
        int y = Mathf.FloorToInt(position.y / CellSize.y);
        int z = Mathf.FloorToInt(position.z / CellSize.z);
        int3 key = new int3(x, y, z);
        if (teMatricesCellOrder.ContainsKey(key))
        {
            var lc = teMatricesCellOrder[key];

            if (lc.yMax < position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier)
                lc.yMax = position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier;

            if (lc.yMin > position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier)
                lc.yMin = position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier;

            lc.matrices.Add(matrix);
        }
        else
        {
            teMatricesCellOrder.Add(key, new LocalCell
            {
                matrices = new List<Matrix4x4>() { matrix },
                yMax = position.y + boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier,
                yMin = position.y - boundingBoxSize.y * 0.5f * boundingBoxSizeMultiplier
            });
        }
        changedCellStructure = true;
    }

    public void Reorder(Vector3 cellSize)
    {
        latestCellSize = cellSize;

        SDictionary oldOrder = teMatricesCellOrder;
        teMatricesCellOrder = new SDictionary();

        foreach (var vs in oldOrder.Values)
        {
            foreach (var matrix in vs.matrices)
            {
                AddElement(matrix);
            }
        }

        SaveMatrices();
    }



    [System.Serializable]
    public class SDictionary : SerializableDictionary<int3, LocalCell> { }

    [System.Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        // load dictionary from lists
        public void OnAfterDeserialize()
        {
            this.Clear();

            if (keys.Count != values.Count)
                throw new System.Exception(string.Format("there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

            for (int i = 0; i < keys.Count; i++)
                this.Add(keys[i], values[i]);
        }
    }
}

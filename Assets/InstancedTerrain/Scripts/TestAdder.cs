using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAdder : MonoBehaviour
{
    public TerrainElementData teData;
    public int pCount = 100;
    public Vector3 area;
    public Vector3 scale;

    public bool SetPositions = false;


    private void OnValidate()
    {
        if(SetPositions)
        {
            SetPositions = false;
            teData.ClearPositions();
            

            for (int i = 0; i < pCount; i++)
            {
                float x = Random.Range(-area.x, area.x);
                float y = Random.Range(-area.y, area.y);
                float z = Random.Range(-area.z, area.z);

                teData.AddElement(new Vector3(x, y, z), Quaternion.identity, scale);
            }

            teData.SaveMatrices();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

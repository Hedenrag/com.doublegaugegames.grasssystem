using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class GrassRenderer : MonoBehaviour
{

    [SerializeField] public Material material;
    [SerializeField] public Mesh mesh;

    GraphicsBuffer commandBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
    ComputeBuffer positionsBuffer;

    const int commandCount = 1;

    public Bounds grassBounds = new Bounds(Vector3.zero, 10000 * Vector3.one);
    public List<Vector3> GetGrassPositions => grassPositions;
    [SerializeField, HideInInspector] List<Vector3> grassPositions = null;

    public void RegenerateGrassField()
    {
        DeInit();
        Init();
    }

    private void Start()
    {
        Init();
    }
    private void OnEnable()
    {
        if (!rendererReady)
        {
            Init();
        }
    }
    private void OnDisable()
    {
        DeInit();
    }

    private void OnDestroy()
    {
        DeInit();
    }

    void Init()
    {
        if (grassPositions == null) { CreateGrassArray(new(20,20), 0.2f); }
        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
        GenerateRenderSettings();
        rendererReady = true;
    }
    void DeInit()
    {
        rendererReady = false;
        commandBuffer?.Release();
        commandBuffer = null;
        positionsBuffer?.Release();
        positionsBuffer = null;
        rendererReady = false;
    }

    void OnValidate()
    {
        if (material == null) { rendererReady = false; return; }
        if (mesh == null) { rendererReady = false; return; }

        DeInit();
        Init();
    }
    bool rendererReady = false;

    //RenderOutput
    RenderParams renderParams;

    void GenerateRenderSettings()
    {
        //preprocess
        positionsBuffer?.Release();
        positionsBuffer = new ComputeBuffer(grassPositions.Count, sizeof(float) * 3);

        positionsBuffer.SetData(grassPositions);

        //Rendering
        renderParams = new RenderParams(material);
        renderParams.worldBounds = grassBounds;
        renderParams.matProps = new MaterialPropertyBlock();
        renderParams.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
        renderParams.matProps.SetBuffer("_GrassPositions", positionsBuffer);
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)grassPositions.Count;
        commandBuffer.SetData(commandData);
        transform.hasChanged = false;
    }

    void Update()
    {
        if (!rendererReady) return;

        if (transform.hasChanged)
        {
            GenerateRenderSettings();
            transform.hasChanged = false;
        }
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBuffer, commandCount);
    }

    public void CreateGrassArray(Vector2Int grassSize, float grassDensity)
    {
        grassPositions = new((int)((grassSize.x/grassDensity) * (grassSize.y/grassDensity)));

        for (int i = 0; i < grassSize.x; i++)
        {
            for (int j = 0; j < grassSize.y; j++)
            {
                grassPositions.Add(new Vector3((i-grassSize.x/2f) * grassDensity + Random.Range(-0.15f, 0.15f), 0f, (j-grassSize.y/2f) * grassDensity + Random.Range(-0.15f, 0.15f)));
            }
        }
    }
}

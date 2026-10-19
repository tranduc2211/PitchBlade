using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
public class GrassSystem : MonoBehaviour
{
    
    #region ComputeSHaders
    public ComputeShader shader;
    int cullingHandler;
    int positionOffsetHandler;
    int groupSizeCulling;
    int coloringHandler;
    int groupSizeColoring;
    #endregion
    public const int cameraGrassResolution = 1152 ;
    public Camera grassCamera;
    public Color wtfColor;
    Texture2D grassTexture;
    public RenderTexture grassRenderTexture;
    public int grassBladeCount;
    float distanceBetween2Blade;
    public const int numberOfGrassBladePerRow = 1152 ;
    public float grassFieldLength = 20;
    public float noiseMultiplier = 1;
    public float noiseRotateMultiplier = 1;
    public Transform grassOne;
    public Transform point1;
    public Transform point2;
    public Transform point3;
    Vector3[] bladePositions;
    Vector3[] vertices;
    Vector4[] colors;
    float[] brightnesses;
    float[] colorLerps;
    int[] culling;
    ComputeBuffer _verticesBuffer;
    ComputeBuffer _cullingBuffer;
    ComputeBuffer _colorBuffer;
    ComputeBuffer _colorLerpBuffer;
    public Material material;
    static readonly int verticesBuffer = Shader.PropertyToID("verticesBuffer");
    static readonly int cullingBuffer = Shader.PropertyToID("cullingBuffer");
    static readonly int colorBuffer = Shader.PropertyToID("colorBuffer");
    // Start is called before the first frame update
    private void Awake()
    {
        GlobalReference.grassSystem = this;
    }
    void Start()
    {
        doneInit = false;
    }
    private bool doneInit = false;
    public async UniTask Init()
    {
        
        await UniTask.Delay(1000);
        grassBladeCount = numberOfGrassBladePerRow * numberOfGrassBladePerRow;
        grassTexture = new Texture2D(cameraGrassResolution, cameraGrassResolution, TextureFormat.RGB24, false);
        InitBuffers();
        await UniTask.DelayFrame(1);
        InitCullingCompute();
        await UniTask.DelayFrame(1);
        InitColoringCompute();
        await UniTask.DelayFrame(1);
       
        await UniTask.Delay(1000);
        InitColorLerpValueToGPU();
        await UniTask.DelayFrame(1);
        InitGrassBladePositionsOnGPU();
        InitVertPositionOffsetCompute();
        await UniTask.DelayFrame(1);

        Vector3 grassPos;
        if (GlobalReference.cPlayer) grassPos = GlobalReference.cPlayer.transform.position;
        else grassPos = Camera.main.transform.position;
        grassPos.y = 0;
        await RefreshGrassPosPrivate(grassPos);


        doneInit = true;
    }
    void InitCullingCompute()
    {
        cullingHandler = shader.FindKernel("CSCulling");
       
        shader.SetFloat("halfFov", 60);
        _cullingBuffer = new ComputeBuffer(grassBladeCount * 3, 1 * 4);
        culling = new int[grassBladeCount * 3];
        colorLerps = new float[grassBladeCount];
        for (int i = 0; i < culling.Length; i++)
        {
            culling[i] =1;//1 is visible
        }
        _cullingBuffer.SetData(culling, 0, 0, culling.Length);
        groupSizeCulling = numberOfGrassBladePerRow / 16;
        shader.SetInt("groupSize", groupSizeCulling);
        shader.SetFloat("distanceBetween2Grass", distanceBetween2Blade);
        shader.SetFloat("fieldLengthHalf", grassFieldLength/2);
        shader.SetBuffer(cullingHandler,"cullingBuffer", _cullingBuffer);
       
        material.SetBuffer(cullingBuffer, _cullingBuffer);
    }
    void InitColoringCompute()
    {
        coloringHandler = shader.FindKernel("CSColoring");
        groupSizeColoring = numberOfGrassBladePerRow / 16;

        shader.SetInt("groupSizeColoring", groupSizeColoring);
        shader.SetInt("numberOfGrassPerRowDividedByCameraResolution", numberOfGrassBladePerRow/cameraGrassResolution);
        
        shader.SetTexture(coloringHandler, "grassRenderTexture", grassRenderTexture);
        shader.SetBuffer(coloringHandler, "colorBuffer", _colorBuffer);
        shader.SetBuffer(coloringHandler, "colorLerpBuffer", _colorLerpBuffer);
    }
    int groupSizePositionOffset;
    void InitVertPositionOffsetCompute()
    {
        positionOffsetHandler = shader.FindKernel("CSVerticeUpdate");
        groupSizePositionOffset = numberOfGrassBladePerRow / 16;
        shader.SetBuffer(positionOffsetHandler, "_verticesBuffer", _verticesBuffer);
        shader.SetInt("groupSizePositionOffset", groupSizePositionOffset);
    }
    void DispatchCullingCompute()
    {
        Vector3 grassCamFlatPos = grassCamera.transform.position;
        grassCamFlatPos.y = 0;
        Transform playerEyeCam;
        if (GlobalReference.cPlayer)
             playerEyeCam = GlobalReference.cPlayer.playerEyeCam.transform;
        else
        {
            playerEyeCam = Camera.main.transform;
        }
        shader.SetVector("cameraPos", (playerEyeCam.position ));
        shader.SetVector("cameraForward", playerEyeCam.forward );
        shader.SetVector("cameraForwardLeft30", Quaternion.Euler(0,-30,0)* playerEyeCam.forward );
        shader.SetVector("cameraForwardRight30", Quaternion.Euler(0,  30, 0) * playerEyeCam.forward );

        shader.Dispatch(cullingHandler, groupSizeCulling, groupSizeCulling, 1);
    }
    void DispatchColoringCompute( )
    {
       
        shader.Dispatch(coloringHandler, groupSizeColoring, groupSizeColoring, 1);
    }

    Vector3 previouslyDispatchedRootPos = Vector3.zero;
    void DispatchVertPosOffsetCompute(Vector3 newRootPos)
    {
        return;
        newRootPos.y = 0;
        shader.SetVector("newRootPos", newRootPos);
        shader.SetVector("currentRootPos", previouslyDispatchedRootPos);
        shader.Dispatch(positionOffsetHandler, groupSizePositionOffset, groupSizePositionOffset, 1);
        previouslyDispatchedRootPos = newRootPos;
    }
    void InitBuffers()
    {
        distanceBetween2Blade = grassFieldLength / numberOfGrassBladePerRow;


        vertices = new Vector3[3 * grassBladeCount];
        _verticesBuffer = new ComputeBuffer(3 * grassBladeCount, 3 * 4);

        bladePositions = new Vector3[grassBladeCount];
       

        colors = new Vector4[grassBladeCount * 3];
        _colorBuffer = new ComputeBuffer(grassBladeCount * 3, 4 * 4);

        _colorBuffer.SetData(colors);
        _colorLerpBuffer = new ComputeBuffer(grassBladeCount , 1 * 4);

        material.SetBuffer(verticesBuffer, _verticesBuffer);
 
        material.SetBuffer(colorBuffer, _colorBuffer);

    }
    
    public Vector3 GetGrassRootPosition(int grassBladeIndex)
    {
        int indexWidth = Mathf.FloorToInt(grassBladeIndex * 1.0f / numberOfGrassBladePerRow);
        int indexHeight = grassBladeIndex % numberOfGrassBladePerRow;
        Vector3 pos;
        pos.x = indexWidth * distanceBetween2Blade - grassFieldLength / 2;
        pos.z = indexHeight * distanceBetween2Blade - grassFieldLength / 2;
        pos.y = 0;
        return pos;
    }

    public bool isRefreshingGrass = false;
    private Vector3 lastPositionFlat = Vector3.zero;
    public async UniTask RefreshGrassPosition(Vector3 position)
    {
        if (doneInit == false) return;
        await RefreshGrassPosPrivate( position);
    }
    private async UniTask RefreshGrassPosPrivate(Vector3 position)
    {
        if (isRefreshingGrass) return;

        position.y = 0;
        if (lastPositionFlat != Vector3.zero)
        {
            if (Vector3.Magnitude(lastPositionFlat - position) < 1) return;//too close, dont rebuild grass
        }
        lastPositionFlat = position;
        isRefreshingGrass = true;
        await UniTask.DelayFrame(1);
        position.y = 3;
        grassCamera.transform.position = position;
        grassCamera.orthographicSize = grassFieldLength / 2;
        Vector3 grassCamFlatPos = grassCamera.transform.position;
        grassCamFlatPos.y = 0;
        shader.SetVector("rootPos", grassCamFlatPos);
        material.SetVector("_RootPos", grassCamFlatPos);
        SnapshotGrassAtNewPositionWithComputeShader();
        await UniTask.DelayFrame(1);
       
        DispatchColoringCompute();
        await UniTask.DelayFrame(1);
        DispatchVertPosOffsetCompute(grassCamFlatPos);
        await UniTask.DelayFrame(1);
        isRefreshingGrass = false;
        
    }
    Rect rectRenderTexture = new Rect(0, 0, cameraGrassResolution, cameraGrassResolution);
    void SnapshotGrassAtNewPosition()
    {
        grassCamera.Render();
        RenderTexture currentActiveRenderTexture = RenderTexture.active;
        RenderTexture.active = grassRenderTexture;
        grassTexture.ReadPixels(rectRenderTexture, 0, 0);
        grassTexture.Apply();
        RenderTexture.active = currentActiveRenderTexture;
    }

    void SnapshotGrassAtNewPositionWithComputeShader()
    {
        grassCamera.Render();
    }
    void InitGrassBladePositionsOnGPU()
    {
        for (int i = 0; i < grassBladeCount; i++)
        {
            Vector3 rootPosOfBlade = GetGrassRootPosition(i);
            bladePositions[i] = rootPosOfBlade;

            int indexWidth = Mathf.FloorToInt(i * 1.0f / numberOfGrassBladePerRow);
            int indexHeight = i % numberOfGrassBladePerRow;
            int indexOnPixelWidth = Mathf.FloorToInt(indexWidth * 1.0f / numberOfGrassBladePerRow * cameraGrassResolution);
            int indexOnPixelHeight = Mathf.FloorToInt(indexHeight * 1.0f / numberOfGrassBladePerRow * cameraGrassResolution);


              float noise  = Mathf.PerlinNoise(rootPosOfBlade.z, rootPosOfBlade.x) ;
            grassOne.transform.eulerAngles = Vector3.up * 360 * noise * noiseRotateMultiplier;

            vertices[i * 3] = point1.position + rootPosOfBlade;
            vertices[i * 3 + 1] = point2.position + rootPosOfBlade;

            float noiseForwardMin = -0.4f;
            float noiseForwardMax = 0.4f;
            float noiseForward = Mathf.Lerp(noiseForwardMin, noiseForwardMax, noise - 0.5f) * noiseMultiplier;
            

            
            float sqrDistanceToCenter = Vector3.SqrMagnitude(rootPosOfBlade);
            float lerpValue = sqrDistanceToCenter /2 / (grassFieldLength * 3f / 6 * grassFieldLength * 3f / 6) ;//*5 so near grass will still 1
            lerpValue = Mathf.Clamp01(lerpValue);



            vertices[i * 3 + 2] = point3.position + rootPosOfBlade + grassOne.forward * noiseForward + grassOne.right * (noise - 0.5f) * noiseMultiplier;

            float lengthValueMul = 1 - lerpValue;
            lengthValueMul = Mathf.Clamp01(lengthValueMul);

            Vector3 dirFromRootToVer3 = vertices[i * 3 + 2] - rootPosOfBlade;
            vertices[i * 3 + 2]-= dirFromRootToVer3*(1-lengthValueMul);//scale back the grass

            vertices[i * 3 + 2].y = Mathf.Max(vertices[i * 3 + 2].y, 0.02f);
        }
        _verticesBuffer.SetData(vertices, 0, 0, vertices.Length);

    }
    void InitColorLerpValueToGPU()
    {
        

        for (int i = 0; i < grassBladeCount; i++)
        {
            Vector3 rootPosOfBlade = GetGrassRootPosition(i);
            bladePositions[i] = rootPosOfBlade;
            
          
            float sqrDistanceToCenter = Vector3.SqrMagnitude(rootPosOfBlade );
            float lerpValue = sqrDistanceToCenter / (grassFieldLength*2.8f/6 * grassFieldLength* 2.8f / 6);
            lerpValue = Mathf.Clamp01(lerpValue);

            colorLerps[i] = lerpValue;
            
            float brightnessValue = Mathf.Lerp(0.65f, 1, lerpValue);
            float brightnessValue2 = Mathf.Lerp(1.1f, 1, lerpValue);
        

 
        }

       
     
        _colorLerpBuffer.SetData(colorLerps, 0, 0, colorLerps.Length);

    }

    private void OnDestroy()
    {
        if (_verticesBuffer != null)
            _verticesBuffer.Release();

       if (_cullingBuffer != null)
             _cullingBuffer.Release();
        if (_colorBuffer != null)
            _colorBuffer.Release();
        if (_colorLerpBuffer != null)
            _colorLerpBuffer.Release();
    }
    Bounds bound;
    private void OnRenderObject()
    {

#if UNITY_EDITOR
        if (isRefreshingGrass==false&& (QuestQualityManager.instance==null  ||QuestQualityManager.instance.isTrailerCinematic))
        {
            material.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3 * grassBladeCount, 1);
        }
        
#endif
    }
    // Update is called once per frame
    void Update()
    {
        if (QuestQualityManager.instance != null)
        {
            if (QuestQualityManager.use3dGrass == 0) return;
        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.C))
        {
               RefreshGrassPosition(Camera.main.transform.position);
        }
#endif
        if (GlobalReference.cPlayer)
            bound.center = GlobalReference.cPlayer.playerHead.transform.position;
        else bound.center = Camera.main.transform.position;
        bound.size = Vector3.one * 50;
        //set pass 0 already set the buffers on GPU
        if (doneInit)
        {
            DispatchCullingCompute();

        }

#if !UNITY_EDITOR
        if (doneInit && isRefreshingGrass == false)
        {
            if (QuestQualityManager.instance==null||QuestQualityManager.instance.isTrailerCinematic==false)
            {
                Camera camera;
                if (GlobalReference.cPlayer) camera = GlobalReference.cPlayer.playerEyeCam;
                else camera = Camera.main;

                Graphics.DrawProcedural(material, bound, MeshTopology.Triangles, 3 * grassBladeCount, 1, camera);

            }
        }
    
      
#endif
    }

    int hehe;

}

using Cysharp.Threading.Tasks;
using UnityEngine;

// Starts GrassSystem the way Goal Poacher's GameManager does during loading,
// then rebuilds the grass patch around the camera whenever it moves far enough.
public class GrassDemoBootstrap : MonoBehaviour
{
    public GrassSystem grassSystem;
    public float refreshDistance = 3f;

    public bool IsReady { get; private set; }

    Vector3 lastRefreshPos;

    async void Start()
    {
        await grassSystem.Init();
        lastRefreshPos = Camera.main.transform.position;
        IsReady = true;
    }

    void Update()
    {
        if (!IsReady || grassSystem.isRefreshingGrass) return;

        Vector3 camPos = Camera.main.transform.position;
        Vector3 delta = camPos - lastRefreshPos;
        delta.y = 0;
        if (delta.magnitude < refreshDistance) return;

        lastRefreshPos = camPos;
        grassSystem.RefreshGrassPosition(camPos).Forget();
    }

    void OnGUI()
    {
        const int w = 330;
        GUI.Box(new Rect(10, 10, w, 92), GUIContent.none);
        GUI.Label(new Rect(20, 16, w - 20, 22),
            IsReady ? $"3D grass: {grassSystem.grassBladeCount:N0} blades, 1 draw call" : "Initializing grass...");
        GUI.Label(new Rect(20, 38, w - 20, 22), "Hold RMB + mouse: look    WASD: move");
        GUI.Label(new Rect(20, 58, w - 20, 22), "Q / E: down / up    Shift: fast");
        GUI.Label(new Rect(20, 78, w - 20, 22), $"Grass patch follows the camera every {refreshDistance:0} m");
    }
}

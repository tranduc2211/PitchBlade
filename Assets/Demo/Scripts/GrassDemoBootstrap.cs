// This file is generated using AI. Please ignore it.
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Starts GrassSystem the way Goal Poacher's GameManager does during loading, then loops the camera
// through fixed viewpoints. As in the game, every switch happens behind a fade to black while the grass
// patch is rebuilt in front of the new camera position.
public class GrassDemoBootstrap : MonoBehaviour
{
    [Serializable]
    public struct Viewpoint
    {
        public string name;
        public Vector3 position;
        public Vector3 lookAt;
    }

    public GrassSystem grassSystem;
    public Viewpoint[] viewpoints;
    // The patch is centered this far in front of the camera, so the ground being looked at is inside it.
    public float patchLookAhead = 4f;
    public float fadeSeconds = 0.25f;
    public float secondsPerView = 5f;
    public bool autoPlay = true;
    public bool showHud = true;

    public bool IsReady { get; private set; }
    public int Current { get; private set; }

    float fade = 1; // 1 = black
    bool switching;
    float viewTimer;

    async void Start()
    {
        SetCamera(viewpoints[0]);
        await grassSystem.Init();
        await SwitchTo(0);
        IsReady = true;
    }

    void Update()
    {
        if (!IsReady || switching) return;

        int next = Current;
        viewTimer += Time.deltaTime;
        if (autoPlay && viewTimer >= secondsPerView) next = (Current + 1) % viewpoints.Length;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space)) next = (Current + 1) % viewpoints.Length;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) next = (Current + viewpoints.Length - 1) % viewpoints.Length;
        for (int i = 0; i < viewpoints.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) next = i;

        if (Input.GetKeyDown(KeyCode.P)) autoPlay = !autoPlay;
        if (next != Current) SwitchTo(next).Forget();
    }

    public async UniTask SwitchTo(int index)
    {
        Current = index;
        await Show(viewpoints[index]);
    }

    public async UniTask Show(Viewpoint viewpoint)
    {
        switching = true;
        await Fade(1);

        SetCamera(viewpoint);
        Transform cam = Camera.main.transform;
        Vector3 center = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * patchLookAhead;
        center.y = 0;
        await grassSystem.RefreshGrassPosition(center);
        await UniTask.DelayFrame(2);

        await Fade(0);
        viewTimer = 0;
        switching = false;
    }

    static void SetCamera(Viewpoint v)
    {
        Camera.main.transform.SetPositionAndRotation(v.position, Quaternion.LookRotation(v.lookAt - v.position));
    }

    async UniTask Fade(float target)
    {
        while (!Mathf.Approximately(fade, target))
        {
            fade = Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / fadeSeconds);
            await UniTask.Yield();
        }
    }

    void OnGUI()
    {
        if (fade > 0)
        {
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (!showHud) return;

        const int w = 340;
        GUI.Box(new Rect(10, 10, w, 92), GUIContent.none);
        GUI.Label(new Rect(20, 16, w - 20, 22),
            IsReady ? $"3D grass: {grassSystem.grassBladeCount:N0} blades, 1 draw call" : "Initializing grass...");
        GUI.Label(new Rect(20, 38, w - 20, 22), $"View {Current + 1} / {viewpoints.Length}: {viewpoints[Current].name}");
        GUI.Label(new Rect(20, 58, w - 20, 22), $"Left / Right or 1-{viewpoints.Length}: switch view");
        GUI.Label(new Rect(20, 78, w - 20, 22), $"P: auto play {(autoPlay ? "on" : "off")}");
    }
}

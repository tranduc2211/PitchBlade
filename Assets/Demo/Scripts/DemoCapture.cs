using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Renders the README screenshots from a player build:
//   StadiumGrassDemo.exe -capture <output folder>
// Does nothing when the argument is absent.
public class DemoCapture : MonoBehaviour
{
    [Serializable]
    public struct Shot
    {
        public string name;
        public Vector3 position;
        public Vector3 lookAt;
    }

    public GrassDemoBootstrap bootstrap;
    public Shot[] shots;

    async void Start()
    {
        string folder = GetArgument("-capture");
        if (folder == null) return;

        Directory.CreateDirectory(folder);
        Transform cam = Camera.main.transform;
        if (cam.TryGetComponent(out DemoFlyCamera fly)) fly.enabled = false;

        await UniTask.WaitUntil(() => bootstrap.IsReady);
        foreach (Shot shot in shots)
        {
            cam.SetPositionAndRotation(shot.position, Quaternion.LookRotation(shot.lookAt - shot.position));
            await UniTask.DelayFrame(3);
            await UniTask.WaitUntil(() => !bootstrap.grassSystem.isRefreshingGrass);
            await UniTask.Delay(800);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, shot.name + ".png"));
            await UniTask.Delay(500);
        }
        Application.Quit();
    }

    static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}

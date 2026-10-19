// This file is generated using AI. Please ignore it.
using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Renders the README screenshots from a player build, one per shot:
//   StadiumGrassDemo.exe -capture <output folder>
// Does nothing when the argument is absent.
public class DemoCapture : MonoBehaviour
{
    public GrassDemoBootstrap bootstrap;
    public GrassDemoBootstrap.Viewpoint[] shots;

    async void Start()
    {
        string folder = GetArgument("-capture");
        if (folder == null) return;

        Directory.CreateDirectory(folder);
        bootstrap.showHud = false;
        bootstrap.autoPlay = false;

        await UniTask.WaitUntil(() => bootstrap.IsReady);
        foreach (GrassDemoBootstrap.Viewpoint shot in shots)
        {
            await bootstrap.Show(shot);
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

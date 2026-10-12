using System.IO;
using MechaRts.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechaRts.EditorTools
{
    public static class PrototypeBatch
    {
        public static void ConfigureAndCheck()
        {
            PlayerSettings.productName = "Mecha RTS Prototype";
            PlayerSettings.companyName = "MechaRts";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorSettings.serializationMode = SerializationMode.ForceText;

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Battle.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Battle.unity", true)
            };

            string error = BattleSelfCheck.Run();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "sim-check.txt"));
            File.WriteAllText(path, string.IsNullOrEmpty(error) ? "SIM_OK" : "SIM_FAIL " + error);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("SIM_FAIL " + error);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("SIM_OK");
            EditorApplication.Exit(0);
        }
    }
}

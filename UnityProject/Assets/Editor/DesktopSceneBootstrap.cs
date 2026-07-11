using System.IO;
using Siegebox.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.EditorTools
{
    /// <summary>
    /// One-click bootstrap for the desktop scene: creates the default runtime theme,
    /// the PanelSettings asset and Assets/Scenes/Main.unity with a wired KernelBridge.
    /// Safe to re-run: existing theme and PanelSettings are reused; an existing scene aborts.
    /// </summary>
    public static class DesktopSceneBootstrap
    {
        private const string ThemePath = "Assets/Unity/UI/UnityDefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = "Assets/Unity/UI/PanelSettings.asset";
        private const string DesktopTemplatePath = "Assets/Unity/UI/Desktop.uxml";
        private const string WindowTemplatePath = "Assets/Unity/UI/Window.uxml";
        private const string TerminalTemplatePath = "Assets/Unity/UI/Terminal.uxml";
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Siegebox/Create Desktop Scene")]
        public static void CreateDesktopScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning($"{ScenePath} already exists — delete or rename it first, then re-run.");
                return;
            }

            var desktopTemplate = LoadTemplate(DesktopTemplatePath);
            var windowTemplate = LoadTemplate(WindowTemplatePath);
            var terminalTemplate = LoadTemplate(TerminalTemplatePath);
            if (desktopTemplate == null || windowTemplate == null || terminalTemplate == null)
            {
                return;
            }

            var panelSettings = LoadOrCreatePanelSettings();
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var desktop = new GameObject("Desktop");
            var uiDocument = desktop.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;

            var bridge = desktop.AddComponent<KernelBridge>();
            var serializedBridge = new SerializedObject(bridge);
            serializedBridge.FindProperty("uiDocument").objectReferenceValue = uiDocument;
            serializedBridge.FindProperty("desktopTemplate").objectReferenceValue = desktopTemplate;
            serializedBridge.FindProperty("windowTemplate").objectReferenceValue = windowTemplate;
            serializedBridge.FindProperty("terminalTemplate").objectReferenceValue = terminalTemplate;
            serializedBridge.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Desktop scene created at {ScenePath} — press Play.");
        }

        private static VisualTreeAsset LoadTemplate(string path)
        {
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (template == null)
            {
                Debug.LogError($"{path} not found — the desktop layouts must exist before bootstrapping.");
            }

            return template;
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var theme = LoadOrCreateDefaultTheme();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = theme;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }

        private static ThemeStyleSheet LoadOrCreateDefaultTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (existing != null)
            {
                return existing;
            }

            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        }
    }
}

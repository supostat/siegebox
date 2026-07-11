using System.IO;
using Siegebox.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.EditorTools
{
    /// <summary>
    /// One-click bootstrap for the terminal scene: creates the default runtime theme,
    /// the PanelSettings asset and Assets/Scenes/Main.unity with a wired KernelBridge.
    /// Safe to re-run: existing theme and PanelSettings are reused; an existing scene aborts.
    /// </summary>
    public static class TerminalSceneBootstrap
    {
        private const string ThemePath = "Assets/Unity/UI/UnityDefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = "Assets/Unity/UI/PanelSettings.asset";
        private const string TerminalTemplatePath = "Assets/Unity/UI/Terminal.uxml";
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Siegebox/Create Terminal Scene")]
        public static void CreateTerminalScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning($"{ScenePath} already exists — delete or rename it first, then re-run.");
                return;
            }

            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TerminalTemplatePath);
            if (template == null)
            {
                Debug.LogError($"{TerminalTemplatePath} not found — the terminal layout must exist before bootstrapping.");
                return;
            }

            var panelSettings = LoadOrCreatePanelSettings();
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var terminal = new GameObject("Terminal");
            var uiDocument = terminal.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;

            var bridge = terminal.AddComponent<KernelBridge>();
            var serializedBridge = new SerializedObject(bridge);
            serializedBridge.FindProperty("uiDocument").objectReferenceValue = uiDocument;
            serializedBridge.FindProperty("terminalTemplate").objectReferenceValue = template;
            serializedBridge.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Terminal scene created at {ScenePath} — press Play.");
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[ScriptedImporter(1, "rs")]
public class RascalScriptImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var textAsset = new TextAsset(File.ReadAllText(ctx.assetPath))
        {
            name = Path.GetFileName(ctx.assetPath),
        };
        ctx.AddObjectToAsset("Rascal Script", textAsset);
        ctx.SetMainObject(textAsset);
    }
}

public class RunawayRascalsMapExportWindow : EditorWindow
{
    private SceneAsset sceneAsset;
    private string title = "New Runaway Rascals Map";
    private string version = "1.0.0";
    private string bundleFileName = "rr-map";
    private int monsterSpawnCount = 3;
    private bool allowAnyUgcMonster = true;
    private List<long> allowedMonsterModIds = new List<long>();
    private TextAsset rascalScript;

    [MenuItem("Tools/Runaway Rascals/Map Export Menu")]
    public static void Open()
    {
        GetWindow<RunawayRascalsMapExportWindow>("Map Export");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Map Source", EditorStyles.boldLabel);
        sceneAsset = (SceneAsset)EditorGUILayout.ObjectField("Unity Scene", sceneAsset, typeof(SceneAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("mod.io Metadata", EditorStyles.boldLabel);
        title = EditorGUILayout.TextField("Title", title);
        version = EditorGUILayout.TextField("Version", version);
        bundleFileName = EditorGUILayout.TextField("AssetBundle File", bundleFileName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);
        monsterSpawnCount = EditorGUILayout.IntField("Monster Spawn Count", Mathf.Max(0, monsterSpawnCount));
        allowAnyUgcMonster = EditorGUILayout.Toggle("Allow Any UGC Monster", allowAnyUgcMonster);

        using (new EditorGUI.DisabledScope(allowAnyUgcMonster))
        {
            DrawMonsterIdList();
        }

        rascalScript = (TextAsset)EditorGUILayout.ObjectField("Rascal Script (.rs)", rascalScript, typeof(TextAsset), false);

        EditorGUILayout.Space();
        if (GUILayout.Button("Export Map Mod"))
        {
            Export();
        }
    }

    private void DrawMonsterIdList()
    {
        int count = Mathf.Max(0, EditorGUILayout.IntField("Allowed Monster Count", allowedMonsterModIds.Count));
        while (allowedMonsterModIds.Count < count) allowedMonsterModIds.Add(0);
        while (allowedMonsterModIds.Count > count) allowedMonsterModIds.RemoveAt(allowedMonsterModIds.Count - 1);

        EditorGUI.indentLevel++;
        for (int i = 0; i < allowedMonsterModIds.Count; i++)
        {
            allowedMonsterModIds[i] = EditorGUILayout.LongField($"Monster Mod ID {i + 1}", allowedMonsterModIds[i]);
        }
        EditorGUI.indentLevel--;
    }

    private void Export()
    {
        if (sceneAsset == null)
        {
            EditorUtility.DisplayDialog("Map Export", "Assign a Unity scene before exporting.", "OK");
            return;
        }

        if (!RunawayRascalsUgcExportUtility.ValidateBundleFileName(bundleFileName))
        {
            EditorUtility.DisplayDialog("Map Export", "AssetBundle File must be a plain filename with no extension or slashes.", "OK");
            return;
        }

        if (rascalScript != null)
        {
            string scriptPath = AssetDatabase.GetAssetPath(rascalScript);
            if (!scriptPath.EndsWith(".rs"))
            {
                EditorUtility.DisplayDialog("Map Export", "The Rascal Script asset must use the .rs extension.", "OK");
                return;
            }
        }

        string outputFolder = EditorUtility.SaveFolderPanel("Export Map Mod Folder", Directory.GetParent(Application.dataPath).FullName, "RunawayRascalsMapMod");
        if (string.IsNullOrEmpty(outputFolder))
        {
            return;
        }

        try
        {
            string prefabPath = RunawayRascalsUgcExportUtility.CreateMapPrefabFromScene(
                sceneAsset,
                title,
                new MapSpawnSettings
                {
                    monsterSpawnCount = monsterSpawnCount,
                    allowAnyUgcMonster = allowAnyUgcMonster,
                    allowedMonsterModIds = allowAnyUgcMonster
                        ? new List<long>()
                        : allowedMonsterModIds.Where(id => id > 0).Distinct().ToList(),
                },
                rascalScript
            );

            RunawayRascalsUgcExportUtility.BuildBundleAndManifest(
                outputFolder,
                bundleFileName,
                prefabPath,
                new RRUgcManifest
                {
                    contentType = "map",
                    title = title,
                    version = version,
                    assetBundleFile = bundleFileName,
                    primaryAsset = prefabPath,
                }
            );

            EditorUtility.DisplayDialog("Map Export", $"Map mod exported to:\n{outputFolder}", "OK");
        }
        catch (System.OperationCanceledException)
        {
        }
    }
}

public class RunawayRascalsMonsterExportWindow : EditorWindow
{
    private GameObject visualPrefab;
    private string title = "New Runaway Rascals Monster";
    private string version = "1.0.0";
    private string bundleFileName = "rr-monster";

    [MenuItem("Tools/Runaway Rascals/Monster Export Menu")]
    public static void Open()
    {
        GetWindow<RunawayRascalsMonsterExportWindow>("Monster Export");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Monster Source", EditorStyles.boldLabel);
        visualPrefab = (GameObject)EditorGUILayout.ObjectField("Visual Prefab", visualPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("mod.io Metadata", EditorStyles.boldLabel);
        title = EditorGUILayout.TextField("Title", title);
        version = EditorGUILayout.TextField("Version", version);
        bundleFileName = EditorGUILayout.TextField("AssetBundle File", bundleFileName);

        EditorGUILayout.Space();
        if (GUILayout.Button("Export Monster Mod"))
        {
            Export();
        }
    }

    private void Export()
    {
        if (visualPrefab == null)
        {
            EditorUtility.DisplayDialog("Monster Export", "Assign a visual prefab before exporting.", "OK");
            return;
        }

        if (!RunawayRascalsUgcExportUtility.ValidateBundleFileName(bundleFileName))
        {
            EditorUtility.DisplayDialog("Monster Export", "AssetBundle File must be a plain filename with no extension or slashes.", "OK");
            return;
        }

        string outputFolder = EditorUtility.SaveFolderPanel("Export Monster Mod Folder", Directory.GetParent(Application.dataPath).FullName, "RunawayRascalsMonsterMod");
        if (string.IsNullOrEmpty(outputFolder))
        {
            return;
        }

        string prefabPath = RunawayRascalsUgcExportUtility.CreateMonsterVisualPrefab(visualPrefab, title);

        RunawayRascalsUgcExportUtility.BuildBundleAndManifest(
            outputFolder,
            bundleFileName,
            prefabPath,
            new RRUgcManifest
            {
                contentType = "monster",
                title = title,
                version = version,
                assetBundleFile = bundleFileName,
                primaryAsset = prefabPath,
            }
        );

        EditorUtility.DisplayDialog("Monster Export", $"Monster mod exported to:\n{outputFolder}", "OK");
    }
}

internal static class RunawayRascalsUgcExportUtility
{
    private const string GeneratedFolder = "Assets/RunawayRascalsUGC/Generated";

    public static bool ValidateBundleFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !fileName.Contains("/")
            && !fileName.Contains("\\")
            && string.IsNullOrEmpty(Path.GetExtension(fileName));
    }

    public static string CreateMapPrefabFromScene(SceneAsset sceneAsset, string title, MapSpawnSettings settings, TextAsset rascalScript)
    {
        EnsureGeneratedFolder();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new System.OperationCanceledException("Map export cancelled because open scenes were not saved.");
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        Scene sourceScene = default;

        try
        {
            sourceScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject exportRoot = new GameObject($"RR_Map_{SanitizeName(title)}");
            SceneManager.MoveGameObjectToScene(exportRoot, sourceScene);

            GameObject[] roots = sourceScene.GetRootGameObjects()
                .Where(root => root != exportRoot)
                .ToArray();

            foreach (GameObject root in roots)
            {
                GameObject clone = Object.Instantiate(root);
                clone.name = root.name;
                SceneManager.MoveGameObjectToScene(clone, sourceScene);
                clone.transform.SetParent(exportRoot.transform, true);
            }

            RRMapContentDefinition content = exportRoot.AddComponent<RRMapContentDefinition>();
            content.spawnSettings = settings;
            content.rascalScriptReference = new RascalScriptReference { textAsset = rascalScript };

            MapRuntimeAnchor anchor = exportRoot.GetComponentInChildren<MapRuntimeAnchor>(true);
            if (anchor == null)
            {
                anchor = exportRoot.AddComponent<MapRuntimeAnchor>();
            }
            content.runtimeAnchor = anchor;

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{exportRoot.name}.prefab");
            PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath);
            Object.DestroyImmediate(exportRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefabPath;
        }
        finally
        {
            if (sourceScene.IsValid())
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    public static string CreateMonsterVisualPrefab(GameObject visualPrefab, string title)
    {
        EnsureGeneratedFolder();

        GameObject exportRoot = new GameObject($"RR_Monster_{SanitizeName(title)}");
        exportRoot.AddComponent<RRMonsterContentDefinition>();

        GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        if (visualInstance == null)
        {
            visualInstance = Object.Instantiate(visualPrefab);
        }
        visualInstance.name = visualPrefab.name;
        visualInstance.transform.SetParent(exportRoot.transform, false);

        string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{exportRoot.name}.prefab");
        PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath);
        Object.DestroyImmediate(exportRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefabPath;
    }

    public static void BuildBundleAndManifest(string outputFolder, string bundleFileName, string assetPath, RRUgcManifest manifest)
    {
        Directory.CreateDirectory(outputFolder);

        var build = new AssetBundleBuild
        {
            assetBundleName = bundleFileName,
            assetNames = new[] { assetPath },
        };

        BuildPipeline.BuildAssetBundles(
            outputFolder,
            new[] { build },
            BuildAssetBundleOptions.None,
            EditorUserBuildSettings.activeBuildTarget
        );

        File.WriteAllText(Path.Combine(outputFolder, RRUgcManifest.FileName), JsonUtility.ToJson(manifest, true));
        AssetDatabase.RemoveUnusedAssetBundleNames();
        AssetDatabase.Refresh();
    }

    private static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/RunawayRascalsUGC"))
        {
            AssetDatabase.CreateFolder("Assets", "RunawayRascalsUGC");
        }

        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            AssetDatabase.CreateFolder("Assets/RunawayRascalsUGC", "Generated");
        }
    }

    private static string SanitizeName(string raw)
    {
        string value = string.IsNullOrWhiteSpace(raw) ? "Untitled" : raw.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value.Replace(' ', '_');
    }
}

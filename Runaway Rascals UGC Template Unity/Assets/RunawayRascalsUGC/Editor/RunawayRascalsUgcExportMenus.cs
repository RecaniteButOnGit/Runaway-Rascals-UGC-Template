using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    private SceneAsset lastSceneAsset;
    private string modTitle = string.Empty;
    private string modVersion = "1.0.0";
    private string bundleFileName = string.Empty;
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
        SyncMetadataFromSceneSelection();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Export Zip", GetSuggestedArchiveFileName(sceneAsset != null ? sceneAsset.name : "Map"));

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

    private void SyncMetadataFromSceneSelection()
    {
        if (sceneAsset == null || sceneAsset == lastSceneAsset)
        {
            return;
        }

        modTitle = sceneAsset.name;
        bundleFileName = RunawayRascalsUgcExportUtility.MakeBundleFileName(sceneAsset.name, "map");
        lastSceneAsset = sceneAsset;
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

        if (string.IsNullOrWhiteSpace(modTitle))
        {
            EditorUtility.DisplayDialog("Map Export", "Enter a mod title before exporting.", "OK");
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

        string archivePath = EditorUtility.SaveFilePanel(
            "Export Map Mod Zip",
            Directory.GetParent(Application.dataPath).FullName,
            GetSuggestedArchiveBaseName(sceneAsset.name),
            "zip"
        );
        if (string.IsNullOrEmpty(archivePath))
        {
            return;
        }

        try
        {
            RunawayRascalsMapExportArtifact exportArtifact = RunawayRascalsUgcExportUtility.CreateMapPrefabFromScene(
                sceneAsset,
                modTitle,
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

            RunawayRascalsUgcExportUtility.BuildBundleAndArchive(
                archivePath,
                bundleFileName,
                exportArtifact.prefabPath,
                new RRUgcManifest
                {
                    contentType = "map",
                    title = modTitle,
                    version = modVersion,
                    assetBundleFile = bundleFileName,
                    primaryAsset = exportArtifact.prefabPath,
                    spawnSettings = new MapSpawnSettings
                    {
                        monsterSpawnCount = exportArtifact.spawnSettings.monsterSpawnCount,
                        allowAnyUgcMonster = exportArtifact.spawnSettings.allowAnyUgcMonster,
                        allowedMonsterModIds = new List<long>(exportArtifact.spawnSettings.allowedMonsterModIds),
                    },
                    rascalScriptAsset = exportArtifact.rascalScriptAssetPath,
                    playerSpawnPath = exportArtifact.playerSpawnPath,
                    monsterSpawnPaths = new List<string>(exportArtifact.monsterSpawnPaths),
                }
            );

            EditorUtility.DisplayDialog("Map Export", $"Map mod zip exported to:\n{archivePath}", "OK");
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private static string GetSuggestedArchiveBaseName(string sourceName)
    {
        return RunawayRascalsUgcExportUtility.MakeArchiveFileName(sourceName);
    }

    private static string GetSuggestedArchiveFileName(string sourceName)
    {
        return $"{GetSuggestedArchiveBaseName(sourceName)}.zip";
    }
}

public class RunawayRascalsMonsterExportWindow : EditorWindow
{
    private GameObject visualPrefab;
    private GameObject lastVisualPrefab;
    private string modTitle = string.Empty;
    private string modVersion = "1.0.0";
    private string bundleFileName = string.Empty;

    [MenuItem("Tools/Runaway Rascals/Monster Export Menu")]
    public static void Open()
    {
        GetWindow<RunawayRascalsMonsterExportWindow>("Monster Export");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Monster Source", EditorStyles.boldLabel);
        visualPrefab = (GameObject)EditorGUILayout.ObjectField("Visual Prefab", visualPrefab, typeof(GameObject), false);
        SyncMetadataFromPrefabSelection();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Export Zip", GetSuggestedArchiveFileName(visualPrefab != null ? visualPrefab.name : "Monster"));

        EditorGUILayout.Space();
        if (GUILayout.Button("Export Monster Mod"))
        {
            Export();
        }
    }

    private void SyncMetadataFromPrefabSelection()
    {
        if (visualPrefab == null || visualPrefab == lastVisualPrefab)
        {
            return;
        }

        modTitle = visualPrefab.name;
        bundleFileName = RunawayRascalsUgcExportUtility.MakeBundleFileName(visualPrefab.name, "monster");
        lastVisualPrefab = visualPrefab;
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

        if (string.IsNullOrWhiteSpace(modTitle))
        {
            EditorUtility.DisplayDialog("Monster Export", "Enter a mod title before exporting.", "OK");
            return;
        }

        string archivePath = EditorUtility.SaveFilePanel(
            "Export Monster Mod Zip",
            Directory.GetParent(Application.dataPath).FullName,
            GetSuggestedArchiveBaseName(visualPrefab.name),
            "zip"
        );
        if (string.IsNullOrEmpty(archivePath))
        {
            return;
        }

        string prefabPath = RunawayRascalsUgcExportUtility.CreateMonsterVisualPrefab(visualPrefab, modTitle);

        RunawayRascalsUgcExportUtility.BuildBundleAndArchive(
            archivePath,
            bundleFileName,
            prefabPath,
            new RRUgcManifest
            {
                contentType = "monster",
                title = modTitle,
                version = modVersion,
                assetBundleFile = bundleFileName,
                primaryAsset = prefabPath,
            }
        );

        EditorUtility.DisplayDialog("Monster Export", $"Monster mod zip exported to:\n{archivePath}", "OK");
    }

    private static string GetSuggestedArchiveBaseName(string sourceName)
    {
        return RunawayRascalsUgcExportUtility.MakeArchiveFileName(sourceName);
    }

    private static string GetSuggestedArchiveFileName(string sourceName)
    {
        return $"{GetSuggestedArchiveBaseName(sourceName)}.zip";
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

    public static string MakeBundleFileName(string sourceName, string suffix)
    {
        string baseName = MakeFileSystemSafeName(sourceName).Replace(' ', '_').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "ugc";
        }

        return $"{baseName}-{suffix}";
    }

    public static string MakeArchiveFileName(string sourceName)
    {
        string fileName = MakeFileSystemSafeName(sourceName);
        return string.IsNullOrWhiteSpace(fileName) ? "RunawayRascalsUGC" : fileName;
    }

    public static RunawayRascalsMapExportArtifact CreateMapPrefabFromScene(SceneAsset sceneAsset, string title, MapSpawnSettings settings, TextAsset rascalScript)
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
                GameObject clone = UnityEngine.Object.Instantiate(root);
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
            string playerSpawnPath = GetRelativeTransformPath(exportRoot.transform, anchor.playerSpawnPoint);
            List<string> monsterSpawnPaths = GetRelativeTransformPaths(exportRoot.transform, anchor.monsterSpawnPoints);
            PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath);
            UnityEngine.Object.DestroyImmediate(exportRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new RunawayRascalsMapExportArtifact
            {
                prefabPath = prefabPath,
                spawnSettings = new MapSpawnSettings
                {
                    monsterSpawnCount = settings.monsterSpawnCount,
                    allowAnyUgcMonster = settings.allowAnyUgcMonster,
                    allowedMonsterModIds = settings.allowedMonsterModIds != null
                        ? settings.allowedMonsterModIds.Where(id => id > 0).Distinct().ToList()
                        : new List<long>(),
                },
                rascalScriptAssetPath = rascalScript != null ? AssetDatabase.GetAssetPath(rascalScript) : null,
                playerSpawnPath = playerSpawnPath,
                monsterSpawnPaths = monsterSpawnPaths,
            };
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
            visualInstance = UnityEngine.Object.Instantiate(visualPrefab);
        }
        visualInstance.name = visualPrefab.name;
        visualInstance.transform.SetParent(exportRoot.transform, false);

        string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{exportRoot.name}.prefab");
        PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath);
        UnityEngine.Object.DestroyImmediate(exportRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefabPath;
    }

    public static void BuildBundleAndArchive(string archivePath, string bundleFileName, string assetPath, RRUgcManifest manifest)
    {
        string tempOutputFolder = Path.Combine(Path.GetTempPath(), "RunawayRascalsUgcExport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempOutputFolder);

        try
        {
            var build = new AssetBundleBuild
            {
                assetBundleName = bundleFileName,
                assetNames = new[] { assetPath },
            };

            BuildPipeline.BuildAssetBundles(
                tempOutputFolder,
                new[] { build },
                BuildAssetBundleOptions.None,
                EditorUserBuildSettings.activeBuildTarget
            );

            string manifestPath = Path.Combine(tempOutputFolder, RRUgcManifest.FileName);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

            string bundlePath = Path.Combine(tempOutputFolder, bundleFileName);
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException($"Built AssetBundle was not found at '{bundlePath}'.");
            }

            string normalizedArchivePath = archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? archivePath
                : $"{archivePath}.zip";

            Directory.CreateDirectory(Path.GetDirectoryName(normalizedArchivePath));
            if (File.Exists(normalizedArchivePath))
            {
                File.Delete(normalizedArchivePath);
            }

            using (ZipArchive archive = ZipFile.Open(normalizedArchivePath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(bundlePath, bundleFileName, System.IO.Compression.CompressionLevel.Optimal);
                archive.CreateEntryFromFile(manifestPath, RRUgcManifest.FileName, System.IO.Compression.CompressionLevel.Optimal);
            }
        }
        finally
        {
            if (Directory.Exists(tempOutputFolder))
            {
                Directory.Delete(tempOutputFolder, true);
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
        }
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
        return MakeFileSystemSafeName(raw).Replace(' ', '_');
    }

    private static string MakeFileSystemSafeName(string raw)
    {
        string value = string.IsNullOrWhiteSpace(raw) ? "Untitled" : raw.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value;
    }

    private static string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null)
        {
            return null;
        }

        if (target == root)
        {
            return string.Empty;
        }

        var pathSegments = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            pathSegments.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return null;
        }

        pathSegments.Reverse();
        return string.Join("/", pathSegments);
    }

    private static List<string> GetRelativeTransformPaths(Transform root, IEnumerable<Transform> targets)
    {
        if (root == null || targets == null)
        {
            return new List<string>();
        }

        return targets
            .Where(target => target != null)
            .Select(target => GetRelativeTransformPath(root, target))
            .Where(path => path != null)
            .Distinct()
            .ToList();
    }
}

internal class RunawayRascalsMapExportArtifact
{
    public string prefabPath;
    public MapSpawnSettings spawnSettings;
    public string rascalScriptAssetPath;
    public string playerSpawnPath;
    public List<string> monsterSpawnPaths = new List<string>();
}

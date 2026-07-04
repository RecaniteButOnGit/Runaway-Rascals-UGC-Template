using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MapSpawnSettings
{
    [Min(0)]
    public int monsterSpawnCount = 3;
    public bool allowAnyUgcMonster = true;
    public List<long> allowedMonsterModIds = new List<long>();
}

[Serializable]
public class RascalScriptReference
{
    public TextAsset textAsset;
}

public class MapRuntimeAnchor : MonoBehaviour
{
    [Header("Spawns")]
    public Transform playerSpawnPoint;
    public List<Transform> monsterSpawnPoints = new List<Transform>();
}

public class RRMapContentDefinition : MonoBehaviour
{
    public MapSpawnSettings spawnSettings = new MapSpawnSettings();
    public RascalScriptReference rascalScriptReference = new RascalScriptReference();
    public MapRuntimeAnchor runtimeAnchor;
}

public class RRMonsterContentDefinition : MonoBehaviour
{
}

[Serializable]
public class RRUgcManifest
{
    public const string FileName = "rr-ugc-manifest.json";

    public string contentType;
    public string title;
    public string version;
    public string assetBundleFile;
    public string primaryAsset;
    public MapSpawnSettings spawnSettings;
    public string rascalScriptAsset;
    public string playerSpawnPath;
    public List<string> monsterSpawnPaths = new List<string>();
}

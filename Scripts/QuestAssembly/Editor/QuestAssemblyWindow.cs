using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 快速零件收集任务编辑器工具。
/// 选中场景中的零件 → 点按钮 → 自动生成任务。
/// 支持多任务：可创建独立 QuestManager（带后缀），也可统一管理。
/// </summary>
public class QuestAssemblyWindow : EditorWindow
{
    private string questName = "组装电台";
    private string prerequisiteQuestName = "";
    private string[] existingQuestNames = new string[0];
    private int prereqIndex = 0;
    private Vector2 scrollPos;

    // 创建模式
    private enum CreationMode { Unified = 0, Separate = 1 }
    private CreationMode creationMode = CreationMode.Unified;

    [MenuItem("Tools/Quest Assembly Tool")]
    public static void ShowWindow()
    {
        var win = GetWindow<QuestAssemblyWindow>("Quest Assembly Tool");
        win.minSize = new Vector2(380, 400);
        win.Show();
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("📦 零件收集任务工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ── 任务名称 ──
        questName = EditorGUILayout.TextField("任务名称", questName);

        // ── 前置任务 ──
        RefreshQuestNames();
        GUILayout.Label("前置任务（可选）", EditorStyles.boldLabel);
        if (existingQuestNames.Length > 0)
        {
            var options = new string[existingQuestNames.Length + 1];
            options[0] = "(无前置任务)";
            for (int i = 0; i < existingQuestNames.Length; i++)
                options[i + 1] = existingQuestNames[i];

            int newIdx = EditorGUILayout.Popup("前置任务", prereqIndex, options);
            if (newIdx != prereqIndex)
            {
                prereqIndex = newIdx;
                prerequisiteQuestName = (newIdx == 0) ? "" : existingQuestNames[newIdx - 1];
            }

            if (!string.IsNullOrEmpty(prerequisiteQuestName))
                EditorGUILayout.HelpBox(
                    $"必须先完成「{prerequisiteQuestName}」，此任务的零件才可收集。",
                    MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("场景尚无其他任务，此任务无前置条件。", MessageType.Info);
        }

        EditorGUILayout.Space();

        // ── 创建模式 ──
        GUILayout.Label("创建模式", EditorStyles.boldLabel);
        creationMode = (CreationMode)EditorGUILayout.EnumPopup("管理模式", creationMode);
        if (creationMode == CreationMode.Unified)
        {
            EditorGUILayout.HelpBox(
                "添加到统一的 QuestManager（推荐）\n所有任务共享一个管理器，方便整体查看。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "创建独立的 QuestManager_{任务名}\n每个任务有自己的管理器，适合独立管理。",
                MessageType.Info);
        }

        EditorGUILayout.Space();

        // ── 选中物体状态与分组预览 ──
        GUILayout.Label("步骤 1: 在 Hierarchy 中选中零件物体", EditorStyles.boldLabel);
        int selectedCount = Selection.gameObjects.Length;
        EditorGUILayout.LabelField("当前选中物体数", selectedCount.ToString());

        if (selectedCount > 0)
        {
            // 分组预览
            var groups = GroupSelectedByType();
            int typeCount = groups.Count;
            EditorGUILayout.LabelField("实际零件类型数", typeCount.ToString());

            if (typeCount < selectedCount)
            {
                EditorGUILayout.HelpBox(
                    $"检测到同种物品多处摆放：{selectedCount} 个物体分为 {typeCount} 种类型，\n" +
                    "每种类型仅需收集 1 次即计入任务进度。",
                    MessageType.Info);
            }

            foreach (var group in groups)
            {
                int instanceCount = group.gameObjects.Count;
                string label = instanceCount > 1
                    ? $"  · {group.groupName} (×{instanceCount}) — 收集 1 次即计为完成"
                    : $"  · {group.groupName}";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"已选中 {selectedCount} 个物体（{typeCount} 种类型），点击下方按钮生成任务。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "请在 Hierarchy 视图中选中多个零件物体，然后点击按钮。",
                MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ── 核心按钮 ──
        GUI.enabled = selectedCount > 0;
        if (GUILayout.Button("🚀 从选中物体创建任务", GUILayout.Height(40)))
        {
            CreateQuestFromSelected();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();

        // ── 场景中已有任务列表 ──
        GUILayout.Label("步骤 2: 场景中的现有任务", EditorStyles.boldLabel);

        if (GUILayout.Button("🔄 刷新任务列表并同步零件"))
        {
            RefreshAndSyncQuests();
        }

        // 显示现有任务
        var allQuestManagers = FindObjectsOfType<QuestManager>();
        if (allQuestManagers.Length == 0)
        {
            EditorGUILayout.HelpBox("场景中尚无 QuestManager。", MessageType.Warning);
        }
        else
        {
            foreach (var qm in allQuestManagers)
            {
                string label = $"  [{qm.name}] — {qm.quests.Count} 个任务";
                foreach (var q in qm.quests)
                    label += $"\n    · {q.questName} ({q.requiredParts.Count} 个零件)";
                EditorGUILayout.HelpBox(label, MessageType.None);

                if (GUILayout.Button($"🔍 定位 {qm.name}"))
                {
                    Selection.activeGameObject = qm.gameObject;
                    EditorGUIUtility.PingObject(qm.gameObject);
                }
            }
        }

        EditorGUILayout.Space();

        // ── 持久化 ──
        GUILayout.Label("步骤 3: 保存/加载（可选）", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 保存为配置"))
        {
            SaveQuestConfig();
        }
        if (GUILayout.Button("📂 从配置生成"))
        {
            LoadQuestConfig();
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("——— 使用说明 ———", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.HelpBox(
            "1. 在 Hierarchy 中选中多个零件物体\n" +
            "2. 输入任务名称（如 \"组装电台\"）\n" +
            "3. 选择管理模式（统一/独立）\n" +
            "4. 点击「从选中物体创建任务」\n" +
            "5. 运行游戏，角色碰触零件即收集\n" +
            "6. 不同零件可属于不同任务，互不干扰",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    /// <summary>选中物体的分组信息</summary>
    private class PartGroup
    {
        public string groupName;            // 去重后的名称，如 "Battery"
        public string stemName;             // 用于 partId 的干净名称
        public List<GameObject> gameObjects = new List<GameObject>();
    }

    /// <summary>按去掉后缀的名称对选中物体分组</summary>
    private List<PartGroup> GroupSelectedByType()
    {
        var groups = new List<PartGroup>();
        foreach (var go in Selection.gameObjects)
        {
            if (go == null) continue;
            if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null) continue;

            // 去掉 Unity 自动添加的 " (1)"、"(2)" 后缀
            string stem = System.Text.RegularExpressions.Regex.Replace(go.name, @"\s*\(\d+\)\s*$", "").Trim();

            // 检查是否已存在同名分组
            var existing = groups.Find(g => g.stemName == stem);
            if (existing != null)
            {
                existing.gameObjects.Add(go);
            }
            else
            {
                groups.Add(new PartGroup
                {
                    groupName = stem,
                    stemName = stem.Replace(" ", "_").Replace("(", "").Replace(")", ""),
                    gameObjects = new List<GameObject> { go }
                });
            }
        }
        return groups;
    }

    // ================================================================
    //  核心逻辑
    // ================================================================

    void CreateQuestFromSelected()
    {
        var groups = GroupSelectedByType();
        if (groups.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有有效的零件物体被选中。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(questName))
        {
            EditorUtility.DisplayDialog("提示", "请输入任务名称。", "确定");
            return;
        }

        int totalObjectCount = 0;
        int typeCount = groups.Count;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // 1. 遍历每个分组，为组内物体挂 CollectiblePart，共享同一个 partId
        foreach (var group in groups)
        {
            string sharedPartId = $"{questName}_{group.stemName}";

            foreach (var go in group.gameObjects)
            {
                var part = go.GetComponent<CollectiblePart>();
                if (part == null)
                    part = Undo.AddComponent<CollectiblePart>(go);

                part.partId = sharedPartId;
                part.partName = group.groupName;
                part.questName = questName;

                Undo.RecordObject(part, "Set part fields");
                totalObjectCount++;
            }
        }

        // 2. 创建/查找 QuestManager
        if (creationMode == CreationMode.Unified)
        {
            // —— 统一模式：找或创建唯一的 QuestManager ——
            var qm = FindObjectOfType<QuestManager>();
            if (qm == null)
            {
                var qmGO = new GameObject("QuestManager");
                qm = qmGO.AddComponent<QuestManager>();
                Undo.RegisterCreatedObjectUndo(qmGO, "Create QuestManager");
            }
            Undo.RecordObject(qm, "Add quest to manager");

            // 注册或更新任务
            var quest = qm.GetOrCreateQuest(questName);
            quest.prerequisiteQuestName = prerequisiteQuestName;
            quest.prerequisiteQuestName = prerequisiteQuestName;
            var partsInScene = FindObjectsOfType<CollectiblePart>()
                .Where(p => p.questName == questName).ToList();

            foreach (var p in partsInScene)
            {
                if (!quest.requiredParts.Exists(r => r.partId == p.partId))
                {
                    quest.requiredParts.Add(new QuestManager.PartRequirement
                    {
                        partId = p.partId,
                        partName = p.partName,
                        collected = false
                    });
                }
            }

            EditorUtility.SetDirty(qm);
        }
        else
        {
            // —— 独立模式：创建 QuestManager_{任务名} ——
            string qmName = $"QuestManager_{questName}";
            var existing = GameObject.Find(qmName);
            QuestManager qm;
            if (existing != null)
            {
                qm = existing.GetComponent<QuestManager>();
                if (qm == null)
                {
                    qm = existing.AddComponent<QuestManager>();
                }
            }
            else
            {
                var qmGO = new GameObject(qmName);
                qm = qmGO.AddComponent<QuestManager>();
                Undo.RegisterCreatedObjectUndo(qmGO, "Create QuestManager");
            }
            Undo.RecordObject(qm, "Add quest to manager");

            var quest = qm.GetOrCreateQuest(questName);
            quest.prerequisiteQuestName = prerequisiteQuestName;
            var partsInScene = FindObjectsOfType<CollectiblePart>()
                .Where(p => p.questName == questName).ToList();

            foreach (var p in partsInScene)
            {
                if (!quest.requiredParts.Exists(r => r.partId == p.partId))
                {
                    quest.requiredParts.Add(new QuestManager.PartRequirement
                    {
                        partId = p.partId,
                        partName = p.partName,
                        collected = false
                    });
                }
            }

            EditorUtility.SetDirty(qm);
        }

        // 3. 确保 QuestUIController 存在
        var uiCtrl = FindObjectOfType<QuestUIController>();
        if (uiCtrl == null)
        {
            var uiGO = new GameObject("QuestUIController");
            uiCtrl = uiGO.AddComponent<QuestUIController>();
            Undo.RegisterCreatedObjectUndo(uiGO, "Create QuestUIController");
        }

        // 4. 确保 Player Tag
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var fps = FindObjectOfType<FPSController>();
            if (fps != null)
            {
                fps.gameObject.tag = "Player";
                Undo.RecordObject(fps.gameObject, "Set Player tag");
            }
            else
            {
                var cc = FindObjectOfType<CharacterController>();
                if (cc != null)
                {
                    cc.gameObject.tag = "Player";
                    Undo.RecordObject(cc.gameObject, "Set Player tag");
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        string modeLabel = creationMode == CreationMode.Unified ? "统一管理器" : "独立管理器";
        Debug.Log($"[QuestAssembly] ✅ 任务创建完成: {questName} ({typeCount} 种零件类型, 共 {totalObjectCount} 个实例) [{modeLabel}]");
        EditorUtility.DisplayDialog(
            "✅ 任务创建完成",
            $"任务「{questName}」已创建！\n" +
            $"零件类型: {typeCount} 种\n" +
            $"物体总数: {totalObjectCount} 个\n" +
            "(同种零件多处摆放时仅需收集 1 次)\n" +
            $"管理模式: {modeLabel}\n\n" +
            "点击 Play 即可测试。",
            "确定");

        Repaint();
    }

    // ================================================================
    //  刷新 & 同步
    // ================================================================

    void RefreshAndSyncQuests()
    {
        var allQm = FindObjectsOfType<QuestManager>();
        if (allQm.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "场景中没有 QuestManager。", "确定");
            return;
        }

        int synced = 0;
        foreach (var qm in allQm)
        {
            Undo.RecordObject(qm, "Sync quests");

            var partsInScene = FindObjectsOfType<CollectiblePart>();
            foreach (var part in partsInScene)
            {
                var quest = qm.GetQuest(part.questName);
                if (quest == null)
                {
                    quest = new QuestManager.QuestData
                    {
                        questName = part.questName,
                        collectedIds = new HashSet<string>()
                    };
                    qm.quests.Add(quest);
                }
                if (!quest.requiredParts.Exists(r => r.partId == part.partId))
                {
                    quest.requiredParts.Add(new QuestManager.PartRequirement
                    {
                        partId = part.partId,
                        partName = part.partName,
                        collected = false
                    });
                    synced++;
                }
            }
            EditorUtility.SetDirty(qm);
        }

        RefreshQuestNames();
        EditorUtility.DisplayDialog("同步完成", $"已同步 {synced} 个新零件到现有任务。", "确定");
        Repaint();
    }

    /// <summary>从场景中的 QuestManager 刷新已有任务名称列表</summary>
    void RefreshQuestNames()
    {
        var qm = FindObjectOfType<QuestManager>();
        if (qm != null)
        {
            existingQuestNames = qm.GetAllQuestNames();
        }
        else
        {
            existingQuestNames = new string[0];
        }
    }

    // ================================================================
    //  持久化
    // ================================================================

    void SaveQuestConfig()
    {
        var partsInScene = FindObjectsOfType<CollectiblePart>()
            .Where(p => p.questName == questName)
            .ToList();

        if (partsInScene.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "场景中未找到匹配的零件，请先创建任务。", "确定");
            return;
        }

        var config = ScriptableObject.CreateInstance<QuestAssemblyConfig>();
        config.questName = questName;
        config.parts = partsInScene.Select(p => new QuestAssemblyConfig.PartDef
        {
            partId = p.partId,
            partName = p.partName,
            partPrefab = PrefabUtility.GetCorrespondingObjectFromSource(p.gameObject)
        }).ToArray();

        string path = EditorUtility.SaveFilePanelInProject(
            "保存任务配置",
            $"{questName}Config",
            "asset",
            "保存任务配置到项目");

        if (!string.IsNullOrEmpty(path))
        {
            string dir = Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", dir));
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"[QuestAssembly] 配置已保存: {path}");
        }
    }

    void LoadQuestConfig()
    {
        string path = EditorUtility.OpenFilePanel("加载任务配置", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        string relPath = "Assets" + path.Substring(Application.dataPath.Length);
        var config = AssetDatabase.LoadAssetAtPath<QuestAssemblyConfig>(relPath);

        if (config == null)
        {
            EditorUtility.DisplayDialog("错误", "所选文件不是有效的 QuestAssemblyConfig。", "确定");
            return;
        }

        int created = 0;
        foreach (var partDef in config.parts)
        {
            if (partDef.partPrefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(partDef.partPrefab);
                if (go != null)
                {
                    go.name = partDef.partName;

                    var part = go.GetComponent<CollectiblePart>();
                    if (part == null) part = go.AddComponent<CollectiblePart>();
                    part.partId = partDef.partId;
                    part.partName = partDef.partName;
                    part.questName = config.questName;

                    go.transform.position = new Vector3(
                        Random.Range(-8f, 8f),
                        0.5f,
                        Random.Range(-8f, 8f));

                    Undo.RegisterCreatedObjectUndo(go, $"Create from config: {partDef.partName}");
                    created++;
                }
            }
        }

        questName = config.questName;

        if (created > 0)
        {
            Debug.Log($"[QuestAssembly] 从配置生成了 {created} 个零件，请选中它们并点击「从选中物体创建任务」。");
            EditorUtility.DisplayDialog("生成完成",
                $"已生成 {created} 个零件。\n\n请选中它们，选择管理模式，点击「从选中物体创建任务」完成绑定。", "确定");
        }
    }
}

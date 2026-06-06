using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一任务管理器 — 场景中只有一个，管理 N 个不同的收集任务。
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("任务列表")]
    public List<QuestData> quests = new List<QuestData>();

    [System.Serializable]
    public class PartRequirement
    {
        public string partId;
        public string partName;
        public bool collected;
    }

    [System.Serializable]
    public class QuestData
    {
        public string questName;                     // 任务唯一名称，如 "组装电台"
        public string prerequisiteQuestName;         // 前置任务名，留空表示无前置
        public List<PartRequirement> requiredParts = new List<PartRequirement>();

        // 运行时
        public HashSet<string> collectedIds = new HashSet<string>();

        public int TotalRequired => requiredParts.Count;
        public int CollectedCount => collectedIds.Count;
        public bool IsComplete => collectedIds.Count >= requiredParts.Count && TotalRequired > 0;
    }

    // 运行时 UI 引用
    private QuestUIController uiController;

    // ================================================================
    //  单例
    // ================================================================
    public static QuestManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[QuestManager] 检测到重复实例，销毁当前对象。");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        uiController = FindObjectOfType<QuestUIController>();
        if (uiController == null)
            Debug.LogWarning("[QuestManager] 场景中未找到 QuestUIController，UI 将不可用。");

        // 重建每个任务的 collectedIds 哈希集（序列化不保留 HashSet）
        foreach (var quest in quests)
        {
            quest.collectedIds = new HashSet<string>();
            foreach (var part in quest.requiredParts)
            {
                if (part.collected)
                    quest.collectedIds.Add(part.partId);
            }
        }

        // 自动从场景中的 CollectiblePart 补全未注册的零件
        var partsInScene = FindObjectsOfType<CollectiblePart>();
        foreach (var part in partsInScene)
        {
            var quest = GetQuest(part.questName);
            if (quest == null)
            {
                // 场景有零件但 QuestManager 没该任务 → 自动创建
                quest = new QuestData { questName = part.questName };
                quest.collectedIds = new HashSet<string>();
                quests.Add(quest);
            }
            if (!quest.requiredParts.Exists(r => r.partId == part.partId))
            {
                quest.requiredParts.Add(new PartRequirement
                {
                    partId = part.partId,
                    partName = part.partName,
                    collected = false
                });
            }
        }

        if (uiController != null)
            uiController.RefreshAll(this);
    }

    // ================================================================
    //  公开 API
    // ================================================================

    /// <summary>查找任务，未找到时自动创建</summary>
    public QuestData GetOrCreateQuest(string questName)
    {
        var q = GetQuest(questName);
        if (q == null)
        {
            q = new QuestData { questName = questName, collectedIds = new HashSet<string>() };
            quests.Add(q);
        }
        return q;
    }

    /// <summary>按名称查找任务</summary>
    public QuestData GetQuest(string questName)
    {
        return quests.Find(q => q.questName == questName);
    }

    /// <summary>由 CollectiblePart 调用。返回 true 表示计入任务进度，false 表示零件已收集过不计入但仍拾取。</summary>
    public bool CollectPart(CollectiblePart part)
    {
        var quest = GetQuest(part.questName);
        if (quest == null)
        {
            Debug.LogWarning($"[QuestManager] 任务 '{part.questName}' 不存在，跳过收集。");
            return false;
        }

        // 已收集过此类型 → 零件可以拾取但不算任务进度
        if (quest.collectedIds.Contains(part.partId)) return false;

        quest.collectedIds.Add(part.partId);

        // 更新 requiredParts 状态
        var req = quest.requiredParts.Find(r => r.partId == part.partId);
        if (req != null)
            req.collected = true;
        else
            quest.requiredParts.Add(new PartRequirement
            {
                partId = part.partId,
                partName = part.partName,
                collected = true
            });

        Debug.Log($"[QuestManager] [{quest.questName}] 收集: {part.partName} ({quest.CollectedCount}/{quest.TotalRequired})");

        // UI 反馈
        if (uiController != null)
        {
            uiController.ShowCollectFeedback(quest.questName, part.partName);

            if (quest.IsComplete)
                uiController.ShowQuestComplete(quest.questName);

            uiController.RefreshAll(this);
        }

        return true;
    }

    /// <summary>查询某个 partId 是否已被某任务收集</summary>
    public bool IsPartCollected(string partId)
    {
        foreach (var quest in quests)
        {
            if (quest.collectedIds.Contains(partId))
                return true;
        }
        return false;
    }

    /// <summary>供编辑器工具调用：注册一组零件到指定任务</summary>
    public void RegisterParts(string questName, List<PartRequirement> parts)
    {
        var quest = GetOrCreateQuest(questName);
        foreach (var p in parts)
        {
            if (!quest.requiredParts.Exists(r => r.partId == p.partId))
                quest.requiredParts.Add(p);
        }
    }

    /// <summary>供编辑器工具校验</summary>
    public bool HasPartId(string questName, string partId)
    {
        var q = GetQuest(questName);
        return q != null && q.requiredParts.Exists(r => r.partId == partId);
    }

    /// <summary>判断任务是否解锁（前置任务已完成或无前置）</summary>
    public bool IsQuestUnlocked(string questName)
    {
        var quest = GetQuest(questName);
        if (quest == null) return false;
        return IsQuestUnlocked(quest);
    }

    public bool IsQuestUnlocked(QuestData quest)
    {
        if (string.IsNullOrEmpty(quest.prerequisiteQuestName))
            return true;
        var prereq = GetQuest(quest.prerequisiteQuestName);
        return prereq != null && prereq.IsComplete;
    }

    /// <summary>获取所有任务名称列表</summary>
    public string[] GetAllQuestNames()
    {
        var names = new string[quests.Count];
        for (int i = 0; i < quests.Count; i++)
            names[i] = quests[i].questName;
        return names;
    }
}

using UnityEngine;

/// <summary>
/// 挂载到场景中每个可收集零件上。
/// 玩家（CharacterController / Collider）碰触触发器即收集。
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class CollectiblePart : MonoBehaviour
{
    [Header("零件配置")]
    public string partId;          // 唯一标识，如 "antenna"
    public string partName;        // 显示名，如 "天线"
    public string questName;       // 所属任务名，用于匹配 QuestManager 中的任务条目

    [Header("反馈")]
    public GameObject collectEffectPrefab;  // 拾取特效（可选）
    public AudioClip collectSound;          // 拾取音效（可选）

    private SphereCollider triggerCollider;
    private bool isCollected = false;

    void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = 0.8f;

        // 如果没有名称则自动取 GameObject 名
        if (string.IsNullOrEmpty(partName))
            partName = gameObject.name;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        // 判断是否是玩家
        if (!other.CompareTag("Player") && other.GetComponent<CharacterController>() == null)
            return;

        // 检查前置任务是否完成
        if (QuestManager.Instance != null && !QuestManager.Instance.IsQuestUnlocked(questName))
        {
            // 前置任务未完成 — 提示锁定
            string prereq = QuestManager.Instance.GetQuest(questName)?.prerequisiteQuestName;
            string msg = string.IsNullOrEmpty(prereq)
                ? "🔒 此任务暂未解锁！"
                : $"🔒 需要先完成「{prereq}」！";
            Debug.Log($"[CollectiblePart] {msg}");

            var ui = FindObjectOfType<QuestUIController>();
            if (ui != null)
                ui.ShowLockedFeedback(questName, prereq);

            return;  // 不收集，零件保留在原地
        }

        isCollected = true;

        // 通知统一 QuestManager 实例 — 返回 true 计入进度，false 已收集过
        bool counted = false;
        if (QuestManager.Instance != null)
        {
            counted = QuestManager.Instance.CollectPart(this);
        }
        else
        {
            Debug.LogWarning("[CollectiblePart] 场景中未找到 QuestManager！");
        }

        // 拾取反馈（总是播放）
        if (collectEffectPrefab != null)
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);

        // UI 反馈：已计入 vs 仅拾取
        var ui = FindObjectOfType<QuestUIController>();
        if (ui != null)
        {
            if (counted)
                ui.ShowCollectFeedback(questName, partName);
            else
                ui.ShowPickupOnlyFeedback(questName, partName);
        }

        // 零件始终消失（玩家已拾取，只是不计入任务）
        gameObject.SetActive(false);
    }
}

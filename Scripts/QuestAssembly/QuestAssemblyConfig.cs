using UnityEngine;

/// <summary>
/// ScriptableObject 配置 — 保存任务定义，供地编复用和版本管理。
/// 右键 Create → Quest Assembly → Quest Config 创建。
/// </summary>
[CreateAssetMenu(menuName = "Quest Assembly/Quest Config", fileName = "NewQuestConfig")]
public class QuestAssemblyConfig : ScriptableObject
{
    [Header("任务信息")]
    public string questName = "新任务";

    [Header("所需零件定义")]
    public PartDef[] parts;

    [System.Serializable]
    public class PartDef
    {
        public string partId;          // 唯一标识
        public string partName;        // 显示名称
        public GameObject partPrefab;  // 可选：用于从配置生成场景零件
    }
}

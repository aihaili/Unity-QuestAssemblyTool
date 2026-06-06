using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 屏幕 UI，支持同时显示多个任务的收集进度。
/// 自动创建 Canvas 和文本组件，无需手动绑定。
/// </summary>
public class QuestUIController : MonoBehaviour
{
    [Header("UI 引用（自动创建）")]
    public Text progressMultiText;  // 左上多任务列表
    public Text feedbackText;       // 中央单条反馈

    [Header("设置")]
    public float feedbackDuration = 1.5f;
    public Color feedbackColor = Color.green;
    public Color completeColor = Color.yellow;

    private Canvas canvas;
    private float feedbackTimer = 0f;
    private bool showingComplete = false;

    void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            SetupCanvasAndUI();
    }

    void SetupCanvasAndUI()
    {
        // 创建 Canvas
        var canvasGO = new GameObject("QuestCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        // ── 多任务进度文本（左上，支持多行）──
        var progGO = new GameObject("ProgressMultiText");
        progGO.transform.SetParent(canvasGO.transform, false);
        progressMultiText = progGO.AddComponent<Text>();
        progressMultiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progressMultiText.fontSize = 24;
        progressMultiText.color = Color.white;
        progressMultiText.alignment = TextAnchor.UpperLeft;
        progressMultiText.text = "";

        var progRt = progGO.GetComponent<RectTransform>();
        progRt.anchorMin = new Vector2(0, 1);
        progRt.anchorMax = new Vector2(0, 1);
        progRt.pivot = new Vector2(0, 1);
        progRt.anchoredPosition = new Vector2(20, -20);
        progRt.sizeDelta = new Vector2(600, 400);

        // ── 单条反馈文本（屏幕中央）──
        var fbGO = new GameObject("FeedbackText");
        fbGO.transform.SetParent(canvasGO.transform, false);
        feedbackText = fbGO.AddComponent<Text>();
        feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        feedbackText.fontSize = 36;
        feedbackText.color = feedbackColor;
        feedbackText.alignment = TextAnchor.MiddleCenter;
        feedbackText.text = "";

        var fbRt = fbGO.GetComponent<RectTransform>();
        fbRt.anchorMin = new Vector2(0.5f, 0.5f);
        fbRt.anchorMax = new Vector2(0.5f, 0.5f);
        fbRt.pivot = new Vector2(0.5f, 0.5f);
        fbRt.anchoredPosition = Vector2.zero;
        fbRt.sizeDelta = new Vector2(600, 80);
    }

    void Update()
    {
        if (feedbackTimer > 0)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0 && !showingComplete)
                feedbackText.text = "";
        }
    }

    // ================================================================
    //  多任务刷新
    // ================================================================

    /// <summary>刷新全部任务的进度列表</summary>
    public void RefreshAll(QuestManager qm)
    {
        if (progressMultiText == null || qm == null) return;

        var sb = new StringBuilder();
        foreach (var quest in qm.quests)
        {
            if (quest.TotalRequired == 0) continue;

            bool unlocked = qm.IsQuestUnlocked(quest);
            bool complete = quest.IsComplete;

            string bar = BuildProgressBar(quest.CollectedCount, quest.TotalRequired);
            string status;

            if (complete)
                status = " ✅";
            else if (!unlocked)
            {
                string prereq = string.IsNullOrEmpty(quest.prerequisiteQuestName) ? "?" : quest.prerequisiteQuestName;
                status = $" 🔒 需要先完成[{prereq}]";
            }
            else
                status = "";

            sb.AppendLine($"[{quest.questName}] {bar} {quest.CollectedCount}/{quest.TotalRequired}{status}");
        }
        progressMultiText.text = sb.ToString();
    }

    private string BuildProgressBar(int current, int total)
    {
        int barLen = 10;
        int filled = Mathf.RoundToInt((float)current / total * barLen);
        var sb = new StringBuilder();
        sb.Append('█', filled);
        sb.Append('░', barLen - filled);
        return sb.ToString();
    }

    // ================================================================
    //  单条反馈
    // ================================================================

    /// <summary>零件收集反馈</summary>
    public void ShowCollectFeedback(string questName, string partName)
    {
        if (feedbackText != null)
        {
            feedbackText.text = $"✅ [{questName}] {partName} 已收集！";
            feedbackText.color = feedbackColor;
            feedbackText.fontSize = 36;
            feedbackTimer = feedbackDuration;
            showingComplete = false;
        }
    }

    /// <summary>单个任务完成</summary>
    public void ShowQuestComplete(string questName)
    {
        if (feedbackText != null)
        {
            feedbackText.text = $"🎉 [{questName}] 组装完成！🎉";
            feedbackText.color = completeColor;
            feedbackText.fontSize = 48;
            feedbackTimer = 4f;
            showingComplete = true;
        }
    }

    /// <summary>拾取零件但不计入任务进度（同类型已收集过）</summary>
    public void ShowPickupOnlyFeedback(string questName, string partName)
    {
        if (feedbackText != null)
        {
            feedbackText.text = $"📦 [{questName}] {partName} 已拾取（多余零件）";
            feedbackText.color = Color.gray;
            feedbackText.fontSize = 30;
            feedbackTimer = 1.2f;
            showingComplete = false;
        }
    }

    /// <summary>前置任务未完成时的锁定提示</summary>
    public void ShowLockedFeedback(string questName, string prereqName)
    {
        if (feedbackText != null)
        {
            string msg = string.IsNullOrEmpty(prereqName)
                ? $"🔒 [{questName}] 尚未解锁！"
                : $"🔒 [{questName}] 需要先完成「{prereqName}」！";
            feedbackText.text = msg;
            feedbackText.color = Color.gray;
            feedbackText.fontSize = 32;
            feedbackTimer = 2f;
            showingComplete = false;
        }
    }
}

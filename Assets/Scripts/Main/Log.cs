using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MyBox;
using Photon.Pun;

public class Log : PhotonCompatible
{

#region Variables

    public static Log instance;

    [Foldout("Log", true)]
    Scrollbar scroll;
    [SerializeField] RectTransform RT;
    GridLayoutGroup gridGroup;
    [SerializeField] LogText textBoxClone;
    Vector2 startingSize;
    Vector2 startingPosition;

    [Foldout("Undos", true)]
    public List<LogText> undosInLog = new();
    public NextStep undoToThis = null;
    [SerializeField] Button undoButton;
    bool currentUndoState = false;

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();

        instance = this;
        gridGroup = RT.GetComponent<GridLayoutGroup>();
        scroll = this.transform.GetChild(1).GetComponent<Scrollbar>();

        startingSize = RT.sizeDelta;
        startingPosition = RT.transform.localPosition;

        undoButton = GameObject.Find("Undo Button").GetComponent<Button>();
        undoButton.onClick.AddListener(() => DisplayUndoBar(!currentUndoState));
    }

    #endregion

#region Add To Log

    public static string Article(string followingWord)
    {
        if (followingWord.StartsWith('A')
            || followingWord.StartsWith('E')
            || followingWord.StartsWith('I')
            || followingWord.StartsWith('O')
            || followingWord.StartsWith('U'))
        {
            return $"an {followingWord}";
        }
        else
        {
            return $"a {followingWord}";
        }
    }

    [PunRPC]
    public void AddText(string logText, int indent = 0)
    {
        if (indent < 0)
            return;

        //Debug.Log($"new log: {logText}");
        LogText newText = Instantiate(textBoxClone, RT.transform);
        newText.name = $"Log {RT.transform.childCount}";
        ChangeScrolling();

        newText.textBox.text = "";
        for (int i = 0; i < indent; i++)
            newText.textBox.text += "    ";

        newText.textBox.text += string.IsNullOrEmpty(logText) ? "" : char.ToUpper(logText[0]) + logText[1..];
        newText.textBox.text = KeywordTooltip.instance.EditText(newText.textBox.text);

        if (undoToThis != null)
        {
            if (undoToThis.action != null)
            {
                Debug.Log($"new undo point in log: {undoToThis.actionName}");
                newText.step = undoToThis;
                undosInLog.Insert(0, newText);
            }
            undoToThis = null;
        }
    }

    void ChangeScrolling()
    {
        int goPast = Mathf.FloorToInt((startingSize.y / gridGroup.cellSize.y) - 1);
        //Debug.Log($"{RT.transform.childCount} vs {goPast}");
        if (RT.transform.childCount > goPast)
        {
            RT.sizeDelta = new Vector2(startingSize.x, startingSize.y + ((RT.transform.childCount - goPast) * gridGroup.cellSize.y));
            if (scroll.value <= 0.2f)
            {
                RT.transform.localPosition = new Vector3(RT.transform.localPosition.x, RT.transform.localPosition.y + gridGroup.cellSize.y / 2, 0);
                scroll.value = 0;
            }
        }
        else
        {
            RT.sizeDelta = startingSize;
            RT.transform.localPosition = startingPosition;
            scroll.value = 0;
        }
    }

    private void Update()
    {
        undosInLog.RemoveAll(item => item == null);
        undoButton.gameObject.SetActive(undosInLog.Count > 0);

        if (Application.isEditor && Input.GetKeyDown(KeyCode.Space))
            AddText($"test {RT.transform.childCount}");
    }

    void OnEnable()
    {
        Application.logMessageReceived += DebugMessages;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= DebugMessages;
    }

    void DebugMessages(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error)
        {
            AddText($"");
            AddText($"the game crashed :(");
        }
    }

    #endregion

#region Undos

    public void DisplayUndoBar(bool flash)
    {
        currentUndoState = flash;
        for (int i = 0; i < undosInLog.Count; i++)
        {
            LogText next = undosInLog[i];
            next.button.onClick.RemoveAllListeners();
            next.button.interactable = flash;
            next.undoBar.gameObject.SetActive(false);

            if (flash)
            {
                next.undoBar.gameObject.SetActive(flash);
                NextStep toThis = next.step;
                next.button.onClick.AddListener(() =>
                InvokeUndo(toThis, next.transform.GetSiblingIndex()));
            }
        }
    }

    void InvokeUndo(NextStep toThisPoint, int deleteLines)
    {
        for (int i = RT.transform.childCount; i > deleteLines; i--)
            Destroy(RT.transform.GetChild(i - 1).gameObject);
        ChangeScrolling();

        Player player = Manager.instance.FindThisPlayer();
        //Debug.Log($"{player.historyStack.IndexOf(toThisPoint)} - {toThisPoint.action}");
        undoToThis = null;
        DisplayUndoBar(false);
        player.UndoAmount(toThisPoint);
    }

    #endregion

}

using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using Photon.Pun;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using System;

[Serializable]
public class CardData
{
    public string cardName;
    public string textBox;
    public string playInstructions;
    public bool useSheets;
    public int cardAmount;
    public int coinAmount;
    public int batteryAmount;
    public int crownAmount;
    public int miscAmount;
    public string artCredit;
}

[Serializable]
public class RobotData: CardData
{
    public int coinCost;
    public int startingBattery;
    public int scoringCrowns;
}

public class CarryVariables : MonoBehaviour
{

#region Setup

    public static CarryVariables instance;
    [Foldout("Prefabs", true)]
    public Player playerPrefab;
    public GameObject playerCardPrefab;
    public GameObject otherCardPrefab;
    public Popup textPopup;
    public Popup cardPopup;
    public SliderChoice sliderPopup;
    public Button playerButtonPrefab;

    [Foldout("Card data", true)]
    public List<CardData> actionFiles { get; private set; }
    public List<RobotData> robotCardFiles { get; private set; }
    public List<CardData> eventCardFiles { get; private set; }

    string sheetURL = "1ded6BsFZUQjSxAKS9LrqqGTuEx9qk6914dgrfJQwir4";
    string apiKey = "AIzaSyCl_GqHd1-WROqf7i2YddE3zH6vSv3sNTA";
    string baseUrl = "https://sheets.googleapis.com/v4/spreadsheets/";

    [Foldout("Right click", true)]
    [SerializeField] Transform rightClickBackground;
    [SerializeField] Image rightClickCard;
    [SerializeField] TMP_Text rightClickText;

    [Foldout("UI", true)]
    [SerializeField] Transform permanentCanvas;
    [SerializeField] Image blackBackground;
    public Sprite faceDownSprite;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Application.targetFrameRate = 60;
            StartCoroutine(GetScripts());
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public IEnumerator TransitionImage(float time)
    {
        blackBackground.gameObject.SetActive(true);
        blackBackground.SetAlpha(1);
        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;
            blackBackground.SetAlpha(1 - (elapsedTime / time));
            yield return null;
        }

        blackBackground.gameObject.SetActive(false);
        blackBackground.SetAlpha(0);
    }
    #endregion

#region Right click

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            blackBackground.gameObject.SetActive(false);
            rightClickBackground.gameObject.SetActive(false);
        }
    }

    public void RightClickDisplay(CanvasGroup group, string text = null)
    {
        CanvasGroup newGroup = Instantiate(group);
        blackBackground.gameObject.SetActive(true);
        blackBackground.SetAlpha(0.85f);

        rightClickBackground.gameObject.SetActive(true);
        try { Destroy(rightClickCard.transform.GetChild(0).gameObject); } catch { }

        if (text == null)
        {
            rightClickText.transform.parent.gameObject.SetActive(false);
            rightClickText.text = "";
        }
        else
        {
            rightClickText.transform.parent.gameObject.SetActive(true);
            rightClickText.text = text;
        }

        newGroup.transform.SetParent(rightClickCard.transform);
        newGroup.transform.localScale = Vector3.one;
        newGroup.transform.localPosition = Vector3.zero;
        if (group.alpha > 0)
            group.alpha = 1;
        foreach (Transform child in newGroup.transform)
            child.gameObject.AddComponent<KeywordLinkHover>();
    }

    #endregion

#region Download

    IEnumerator Download(string range)
    {
        if (Application.isEditor)
        {
            string url = $"{baseUrl}{sheetURL}/values/{range}?key={apiKey}";
            using UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Download failed: {www.error}");
            }
            else
            {
                string filePath = $"Assets/Resources/{range}.txt";
                File.WriteAllText($"{filePath}", www.downloadHandler.text);

                string[] allLines = File.ReadAllLines($"{filePath}");
                List<string> modifiedLines = allLines.ToList();
                modifiedLines.RemoveRange(1, 3);
                File.WriteAllLines($"{filePath}", modifiedLines.ToArray());
                Debug.Log($"downloaded {range}");
            }
        }
    }

    IEnumerator GetScripts()
    {
        CoroutineGroup group = new(this);
        group.StartCoroutine(Download("Actions"));
        group.StartCoroutine(Download("Player Cards"));
        group.StartCoroutine(Download("Events"));
        while (group.AnyProcessing)
            yield return null;

        robotCardFiles = GetDataFiles<RobotData>(ReadFile("Player Cards"));
        actionFiles = GetDataFiles<CardData>(ReadFile("Actions"));
        eventCardFiles = GetDataFiles<CardData>(ReadFile("Events"));

        string[][] ReadFile(string range)
        {
            TextAsset data = Resources.Load($"{range}") as TextAsset;

            string editData = data.text;
            editData = editData.Replace("],", "").Replace("{", "").Replace("}", "");

            string[] numLines = editData.Split("[");
            string[][] list = new string[numLines.Length][];

            for (int i = 0; i < numLines.Length; i++)
            {
                list[i] = numLines[i].Split("\",");
            }
            return list;
        }
    }

    List<T> GetDataFiles<T>(string[][] data) where T : new()
    {
        Dictionary<string, int> columnIndex = new();
        List<T> toReturn = new();

        for (int i = 0; i < data[1].Length; i++)
        {
            string nextLine = data[1][i].Trim().Replace("\"", "");
            if (!columnIndex.ContainsKey(nextLine))
                columnIndex.Add(nextLine, i);
        }

        for (int i = 2; i < data.Length; i++)
        {
            for (int j = 0; j < data[i].Length; j++)
                data[i][j] = data[i][j].Trim().Replace("\"", "").Replace("\\", "").Replace("]", "");

            if (data[i][0].IsNullOrEmpty())
                continue;

            T nextData = new();
            toReturn.Add(nextData);

            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (columnIndex.TryGetValue(field.Name, out int index))
                {
                    string sheetValue = data[i][index];

                    if (field.FieldType == typeof(int))
                    {
                        field.SetValue(nextData, StringToInt(sheetValue));
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        field.SetValue(nextData, StringToBool(sheetValue));
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        field.SetValue(nextData, sheetValue);
                    }
                }
            }
        }

        return toReturn;
    }

    int StringToInt(string line)
    {
        line = line.Trim();
        try
        {
            return (line.Equals("")) ? -1 : int.Parse(line);
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    bool StringToBool(string line)
    {
        line = line.Trim();
        return line == "TRUE";
    }

#endregion

}

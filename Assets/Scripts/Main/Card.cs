using UnityEngine;
using UnityEngine.UI;
using MyBox;
using Photon.Pun;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class Card : PhotonCompatible
{

#region Variables

    public Button button { get; private set; }
    public Image border { get; private set; }
    public CanvasGroup cg { get; private set; }

    protected Image background;
    protected Image artBox;
    protected TMP_Text cardName;
    protected TMP_Text cardDescription;

    protected List<string> activationSteps = new();
    protected int stepCounter;
    protected int sideCounter;

    #endregion

#region Setup

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();

        border = this.transform.Find("Border").GetComponent<Image>();
        button = GetComponent<Button>();
        cg = this.transform.Find("Canvas Group").GetComponent<CanvasGroup>();
        this.transform.localScale = Vector3.Lerp(Vector3.one, Manager.instance.canvas.transform.localScale, 0.5f);

        background = cg.transform.Find("Background").GetComponent<Image>();
        cardDescription = cg.transform.Find("Card Description").GetComponent<TMP_Text>();
        try
        {
            cardName = cg.transform.Find("Card Name").GetComponent<TMP_Text>();
            cardName.text = this.name;
            artBox = cg.transform.Find("Art Box").GetComponent<Image>();
            artBox.sprite = Resources.Load<Sprite>($"Card Art/{this.name}");
        }
        catch { }
    }

    internal virtual void AssignInfo(int fileNumber)
    {
    }

    protected void GetInstructions(CardData dataFile)
    {
        if (dataFile.useSheets)
        {
            activationSteps = SpliceString(dataFile.playInstructions);
            foreach (string next in activationSteps)
            {
                if (FindMethod(next) == null)
                    Debug.LogError($"{this.name} - {next} is wrong");
            }
        }

        List<string> SpliceString(string text)
        {
            if (text.IsNullOrEmpty())
            {
                return new();
            }
            else
            {
                string divide = text.Replace(" ", "").Trim();
                return divide.Split('/').ToList();
            }
        }
    }

    #endregion

#region Animations

    public IEnumerator MoveCard(Vector3 newPos, float waitTime, Vector3 newScale)
    {
        float elapsedTime = 0;
        Vector2 originalPos = this.transform.localPosition;
        Vector2 originalScale = this.transform.localScale;

        while (elapsedTime < waitTime)
        {
            this.transform.localPosition = Vector3.Lerp(originalPos, newPos, elapsedTime / waitTime);
            this.transform.localScale = Vector3.Lerp(originalScale, newScale, elapsedTime / waitTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        this.transform.localPosition = newPos;
    }

    public IEnumerator RevealCard(float totalTime)
    {
        if (this.cg.alpha == 1)
            yield break;

        transform.localEulerAngles = new Vector3(0, 0, 0);
        float elapsedTime = 0f;

        Vector3 originalRot = this.transform.localEulerAngles;
        Vector3 newRot = new(0, 90, 0);

        while (elapsedTime < totalTime)
        {
            this.transform.localEulerAngles = Vector3.Lerp(originalRot, newRot, elapsedTime / totalTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cg.alpha = 1;
        elapsedTime = 0f;

        while (elapsedTime < totalTime)
        {
            this.transform.localEulerAngles = Vector3.Lerp(newRot, originalRot, elapsedTime / totalTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        this.transform.localEulerAngles = originalRot;
    }

    private void FixedUpdate()
    {
        try { this.border.SetAlpha(Manager.instance.opacity); } catch { }
    }

    #endregion

}

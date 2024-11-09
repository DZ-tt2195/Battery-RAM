using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyBox;
using System.Linq;

public class Popup : MonoBehaviour
{

#region Setup

    [SerializeField] TMP_Text textbox;
    RectTransform textWidth;
    RectTransform imageWidth;

    [SerializeField] Button textButton;
    [SerializeField] Button cardButton;
    public List<Button> buttonsInCollector { get; private set; }
    Player decidingPlayer;
    public bool beDestroyed { get; private set; }

    void Awake()
    {
        textWidth = textbox.GetComponent<RectTransform>();
        imageWidth = this.transform.GetComponent<RectTransform>();
        buttonsInCollector = new();
    }

    internal void StatsSetup(Player player, string header, bool beDestroyed, Vector2 position)
    {
        decidingPlayer = player;
        if (header == "")
        {
            textbox.gameObject.SetActive(false);
            this.transform.GetChild(1).transform.localPosition = Vector3.zero;
            imageWidth.sizeDelta = new Vector2(imageWidth.sizeDelta.x, imageWidth.sizeDelta.y / 2);
        }
        else
        {
            this.textbox.text = KeywordTooltip.instance.EditText(header);
        }

        this.transform.SetParent(Manager.instance.canvas.transform);
        this.transform.localPosition = position;
        this.transform.localScale = new Vector3(1, 1, 1);
        this.beDestroyed = beDestroyed;
    }

    #endregion

#region Add Button

    internal void AddTextButton(string text)
    {
        Button nextButton = Instantiate(textButton, this.transform.GetChild(1));
        nextButton.transform.GetChild(0).GetComponent<TMP_Text>().text = (text);

        nextButton.interactable = true;
        nextButton.name = text;
        int buttonNumber = buttonsInCollector.Count;
        nextButton.onClick.AddListener(() => decidingPlayer.DecisionMade(buttonNumber));
        buttonsInCollector.Add(nextButton);

        for (int i = 0; i < buttonsInCollector.Count; i++)
        {
            Transform nextTransform = buttonsInCollector[i].transform;
            nextTransform.transform.localPosition = new Vector2((buttonsInCollector.Count - 1) * -150 + (300 * i), 0);
        }
        Resize();
    }

    internal int AddCardButton(Card card, float alpha)
    {
        Button nextButton = Instantiate(cardButton, this.transform.GetChild(1));
        nextButton.GetComponent<RightClickMe>().AssignInfo(card.cg, card.GetFile().artCredit);

        nextButton.name = card.name;
        nextButton.interactable = true;
        int buttonNumber = buttonsInCollector.Count;
        nextButton.onClick.AddListener(() => decidingPlayer.DecisionMade(buttonNumber, card));
        buttonsInCollector.Add(nextButton);

        CanvasGroup group = Instantiate(card.cg);
        group.transform.SetParent(nextButton.transform);
        group.transform.localScale = Vector3.one;
        group.transform.localPosition = Vector3.zero;

        for (int i = 0; i < buttonsInCollector.Count; i++)
        {
            Transform nextTransform = buttonsInCollector[i].transform;
            nextTransform.transform.localPosition = new Vector2((buttonsInCollector.Count - 1) * -150 + (300 * i), 0);
        }

        Resize();
        return buttonNumber;
    }

    #endregion

#region Helpers

    void Resize()
    {
        imageWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, imageWidth.sizeDelta.y);
        textWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, textWidth.sizeDelta.y);
    }

    internal void DisableButton(int number)
    {
        if (number < buttonsInCollector.Count)
        {
            buttonsInCollector[number].onClick.RemoveAllListeners();
            buttonsInCollector[number].interactable = false;
        }
    }

    internal void RemoveButton(int number)
    {
        if (number < buttonsInCollector.Count)
        {
            Button button = buttonsInCollector[number];
            buttonsInCollector.RemoveAt(number);
            Destroy(button.gameObject);
        }
        Resize();
    }

    internal void WaitForChoice()
    {
        if (buttonsInCollector.Count == 0)
            decidingPlayer.PopStack();
    }

    #endregion

}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    bool clicked = false;
    Popup popup;
    Button button;
    TMP_Text textBox;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => clicked = !clicked);
        textBox = button.transform.GetChild(0).GetComponent<TMP_Text>();

        popup = Instantiate(CarryVariables.instance.cardPopup);
        popup.gameObject.SetActive(false);
    }

    internal void Setup(Player player, string header, Vector3 position)
    {
        popup.gameObject.SetActive(true);
        popup.StatsSetup(player, header, false, position);
        popup.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (popup.buttonsInCollector.Count > 0)
            popup.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!clicked)
            popup.gameObject.SetActive(false);
    }

    internal (CanvasGroup card, int index) FindCard(Card card)
    {
        for (int i = 0; i < popup.buttonsInCollector.Count; i++)
        {
            Button nextButton = popup.buttonsInCollector[i];
            if (nextButton.name == card.name)
            {
                return (nextButton.GetComponent<CanvasGroup>(), i);
            }
        }
        return (null, -1);
    }

    internal void RemoveCard(Card card, string text)
    {
        (CanvasGroup target, int index) = FindCard(card);
        RemoveCard(index, text);
    }

    internal void RemoveCard(int index, string text)
    {
        if (index >= 0)
        {
            popup.RemoveButton(index);
            textBox.text = $"{text} ({popup.buttonsInCollector.Count})";
        }
    }

    internal void AddCard(Card card, float alpha, string text)
    {
        int number = popup.AddCardButton(card, alpha, false);
        popup.DisableButton(number);
        textBox.text = $"{text} ({popup.buttonsInCollector.Count})";
    }
}

using System.Collections.Generic;
using UnityEngine;
using MyBox;
using TMPro;
using UnityEngine.UI;
using System;

public class CardGallery : MonoBehaviour
{

#region Setup

    [SerializeField] TMP_Text searchResults;
    [SerializeField] RectTransform storeCards;
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] TMP_Dropdown crownDropdown;
    [SerializeField] TMP_Dropdown typeDropdown;
    [SerializeField] Scrollbar cardScroll;
    List<Card> allCards = new();

    private void Start()
    {
        searchInput.onValueChanged.AddListener(ChangeSearch);
        crownDropdown.onValueChanged.AddListener(ChangeDropdown);
        typeDropdown.onValueChanged.AddListener(ChangeDropdown);

        for (int i = 0; i<CarryVariables.instance.robotCardFiles.Count; i++)
        {
            GameObject nextObject = Instantiate(CarryVariables.instance.playerCardPrefab);
            PlayerCard card = nextObject.AddComponent<PlayerCard>();
            card.AssignInfo(i);
            allCards.Add(card);
        }
        for (int i = 0; i < CarryVariables.instance.eventCardFiles.Count; i++)
        {
            GameObject nextObject = Instantiate(CarryVariables.instance.otherCardPrefab);
            EventCard card = nextObject.AddComponent<EventCard>();
            card.AssignInfo(i);
            allCards.Add(card);
        }

        SearchCards();
    }

    #endregion

#region Card Search

    bool CompareStrings(string searchBox, string comparison)
    {
        if (searchBox.IsNullOrEmpty())
            return true;
        return (comparison.IndexOf(searchBox, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    void ChangeSearch(string text)
    {
        SearchCards();
    }

    void ChangeDropdown(int n)
    {
        SearchCards();
    }

    void SearchCards()
    {
        int searchCrown;
        if (typeDropdown.options[typeDropdown.value].text == "Event")
        {
            searchCrown = -1;
            crownDropdown.gameObject.SetActive(false);
        }
        else
        {
            crownDropdown.gameObject.SetActive(true);
            try { searchCrown = int.Parse(crownDropdown.options[crownDropdown.value].text); }
            catch { searchCrown = -1; }
        }

        foreach (Card card in allCards)
        {
            bool stringMatch = (CompareStrings(searchInput.text, card.GetFile().textBox) || CompareStrings(searchInput.text, card.name));
            bool crownMatch = false;
            bool typeMatch = false;

            if (typeDropdown.options[typeDropdown.value].text == "Event")
            {
                crownMatch = true;
                typeMatch = card is EventCard;
            }
            else if (typeDropdown.options[typeDropdown.value].text == "Robot")
            {
                if ((card is PlayerCard))
                {
                    RobotData data = (RobotData)card.GetFile();
                    crownMatch = (searchCrown == -1) || data.scoringCrowns == searchCrown;
                    typeMatch = true;
                }
            }

            if (stringMatch && crownMatch && typeMatch)
            {
                card.transform.SetParent(storeCards);
                card.transform.SetAsLastSibling();
            }
            else
            {
                card.transform.SetParent(null);
            }
        }

        storeCards.transform.localPosition = new Vector3(0, -1050, 0);
        storeCards.sizeDelta = new Vector3(2560, Math.Max(800, 400 * (Mathf.Ceil(storeCards.childCount / 8f))));
        searchResults.text = $"Found {storeCards.childCount} Cards";
    }

    #endregion

}

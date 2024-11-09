using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using MyBox;
using System.Reflection;
using System;
using System.Linq.Expressions;

public enum Resource { Coin, Crown }
public enum StepType { Share, UndoPoint, Revert, None }
[Serializable]
public class NextStep
{
    public StepType stepType { get; private set; }
    public PhotonCompatible source { get; private set; }
    public Expression<Action> action { get; private set; }

    internal NextStep(PhotonCompatible source, StepType stepType, Expression<Action> action)
    {
        this.source = source;
        this.action = action;
        ChangeType(stepType, stepType == StepType.UndoPoint);
    }

    internal void ChangeType(StepType stepType, bool changeUndo)
    {
        this.stepType = stepType;
        if (changeUndo)
            Log.instance.undoToThis = (stepType == StepType.UndoPoint) ? this : null;
    }
}

public class Player : PhotonCompatible
{

#region Variables

    [Foldout("Player info", true)]
    [ReadOnly] public int playerPosition;
    public Photon.Realtime.Player realTimePlayer { get; private set; }
    public Dictionary<Resource, int> resourceDictionary { get; private set; }

    [Foldout("Cards", true)]
    public List<PlayerCard> cardsInHand = new();
    public List<PlayerCard> cardsInPlay = new();
    public List<EventCard> myEvents { get; private set; }

    [Foldout("UI", true)]
    Button myButton;
    Button resignButton;
    Transform keepHand;
    Transform keepPlay;
    [SerializeField] HoverPopup hoverEvent;
    [SerializeField] TMP_Text resourceText;

    [Foldout("Undo", true)]
    [ReadOnly][SerializeField] int currentStep = -1;
    [ReadOnly] public List<NextStep> historyStack = new();
    public Dictionary<string, MethodInfo> dictionary = new();

    [Foldout("Choices", true)]
    public int choice { get; private set; }
    public Card chosenCard { get; private set; }
    Stack<List<Action>> decisionReact = new();
    List<TriggeredAbility> allAbilities = new();

    #endregion

#region Setup

    protected override void Awake()
    {
        base.Awake();
        if (PhotonNetwork.IsConnected && pv.AmOwner)
            pv.Owner.NickName = PlayerPrefs.GetString("Online Username");
        this.bottomType = this.GetType();

        resignButton = GameObject.Find("Resign Button").GetComponent<Button>();
        keepHand = transform.Find("Keep Hand");
        keepHand = transform.Find("Keep Play");
    }

    private void Start()
    {
        this.transform.SetParent(Manager.instance.storePlayers);
        if (PhotonNetwork.IsConnected)
            this.name = pv.Owner.NickName;
        hoverEvent.Setup(this, $"{this.name}'s Events", new(0, 250));
    }

    internal void AssignInfo(int position)
    {
        this.playerPosition = position;
        Manager.instance.storePlayers.transform.localScale = Manager.instance.canvas.transform.localScale;
        this.transform.localPosition = Vector3.zero;
        if (PhotonNetwork.IsConnected)
            realTimePlayer = PhotonNetwork.PlayerList[pv.OwnerActorNr - 1];

        myButton = Instantiate(CarryVariables.instance.playerButtonPrefab);
        myButton.transform.SetParent(Manager.instance.canvas.transform);
        myButton.transform.localScale = Manager.instance.canvas.transform.localScale;
        myButton.transform.localPosition = new(-1100, 425 - (100 * playerPosition));
        myButton.transform.GetChild(0).GetComponent<TMP_Text>().text = this.name;
        myButton.onClick.AddListener(MoveScreen);
        myButton.gameObject.SetActive(PhotonNetwork.IsConnected && PhotonNetwork.CurrentRoom.MaxPlayers >= 2);

        resourceDictionary = new()
        {
            { Resource.Coin, 0 },
            { Resource.Crown, 0 },
        };

        if (InControl())
        {
            resignButton.onClick.AddListener(() => Manager.instance.DisplayEnding(this.playerPosition));

            DoFunction(() => DrawPlayerCards(4, 0), RpcTarget.MasterClient);

            if (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom.MaxPlayers <= 2)
                DoFunction(() => DrawEventCards(3), RpcTarget.MasterClient);
            else
                DoFunction(() => DrawEventCards(2), RpcTarget.MasterClient);

            Invoke(nameof(MoveScreen), 0.25f);
        }
    }

    #endregion

#region Events

    [PunRPC]
    void DrawEventCards(int number)
    {
        if (Manager.instance.eventDeck.childCount < number)
        {
            Manager.instance.eventDiscard.Shuffle();
            while (Manager.instance.eventDiscard.childCount > 0)
            {
                Transform nextChild = Manager.instance.eventDiscard.GetChild(0);
                nextChild.SetParent(Manager.instance.eventDeck);
                nextChild.SetAsLastSibling();
            }
        }

        for (int i = 0; i < number; i++)
        {
            Card card = Manager.instance.eventDeck.GetChild(i).GetComponent<Card>();
            DoFunction(() => ReceiveEvent(card.pv.ViewID));
        }
    }

    [PunRPC]
    void ReceiveEvent(int PV)
    {
        EventCard card = PhotonView.Find(PV).GetComponent<EventCard>();
        myEvents.Add(card);
        hoverEvent.AddCard(card, InControl() ? 0.75f : 0, "View Revealed\nEvents");
    }

    [PunRPC]
    void RevealEvent(bool undo, int PV)
    {
        EventCard card = PhotonView.Find(PV).GetComponent<EventCard>();
        (CanvasGroup group, int index) = hoverEvent.FindCard(card);
        if (group != null && group.alpha < 1)
        {
            if (undo)
            {
                hoverEvent.RemoveCard(index, "View Revealed\nEvents");
                hoverEvent.AddCard(card, InControl() ? 0.75f : 0, "View Revealed\nEvents");
            }
            else
            {
                hoverEvent.RemoveCard(index, "View Revealed\nEvents");
                hoverEvent.AddCard(card, 1, "View Revealed\nEvents");
            }
        }
    }

    #endregion

#region Resources

    void UpdateResourceText()
    {
        resourceText.text = KeywordTooltip.instance.EditText($"{this.name}: " +
            $"{resourceDictionary[Resource.Coin]} Coin, " +
            $"{resourceDictionary[Resource.Crown]} Food, " +
            $" | " +
            $"Total Crown: {CalculateScore()}");
    }

    public int CalculateScore()
    {
        int answer = resourceDictionary[Resource.Crown];
        foreach (PlayerCard card in cardsInPlay)
            answer += card.dataFile.scoringCrowns;
        return answer;
    }

    public void ResourceRPC(Resource resource, int number, int logged, string source = "")
    {
        int actualAmount = number;
        if (resource != Resource.Crown)
        {
            if (resourceDictionary[resource] + number < 0)
                actualAmount = -1 * resourceDictionary[resource];
        }
        if (actualAmount != 0)
            RememberStep(this, StepType.Share, () => ChangeResource(false, (int)resource, actualAmount, source, logged));
    }

    [PunRPC]
    void ChangeResource(bool undo, int resource, int amount, string source, int logged)
    {
        if (undo)
        {
            resourceDictionary[(Resource)resource] -= amount;
        }
        else
        {
            string parathentical = source == "" ? "" : $"({source})";
            resourceDictionary[(Resource)resource] += amount;

            if (amount >= 0)
                Log.instance.AddText($"{this.name} gets +{Mathf.Abs(amount)} {(Resource)resource} {parathentical}.", logged);
            else
                Log.instance.AddText($"{this.name} loses {Mathf.Abs(amount)} {(Resource)resource} {parathentical}.", logged);
        }
        UpdateResourceText();
    }

    #endregion

#region Cards In Hand

    [PunRPC]
    internal void DrawPlayerCards(int cardsToDraw, int logged)
    {
        if (cardsToDraw > Manager.instance.playerDeck.childCount)
        {
            Manager.instance.playerDiscard.Shuffle();
            while (Manager.instance.playerDiscard.childCount > 0)
            {
                Transform nextChild = Manager.instance.playerDiscard.GetChild(0);
                nextChild.SetParent(Manager.instance.playerDeck);
                nextChild.SetAsLastSibling();
            }
        }

        for (int i = 0; i < cardsToDraw; i++)
        {
            Card card = Manager.instance.playerDeck.GetChild(i).GetComponent<Card>();
            DoFunction(() => SendPlayerCardToAsker(card.pv.ViewID, logged), this.realTimePlayer);
        }
    }

    [PunRPC]
    void SendPlayerCardToAsker(int PV, int logged)
    {
        RememberStep(this, StepType.Share, () => AddToHand(false, PV, logged));
    }

    [PunRPC]
    void AddToHand(bool undo, int PV, int logged)
    {
        if (undo)
        {
            DoFunction(() => ReturnToDeck(PV), RpcTarget.MasterClient);
        }
        else
        {
            PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();
            cardsInHand.Add(card);

            if (InControl())
                Log.instance.AddText($"{this.name} draws {card.name}.", logged);
            else
                Log.instance.AddText($"{this.name} draws 1 Card.", logged);

            card.transform.localPosition = new Vector2(0, -1100);
            card.cg.alpha = 0;
        }
        SortHand();
    }

    [PunRPC]
    void ReturnToDeck(int PV)
    {
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();
        cardsInHand.Remove(card);
        card.transform.SetParent(Manager.instance.playerDeck);
        card.transform.SetAsFirstSibling();
        card.transform.localPosition = new(0, 10000);
    }

    public void SortHand()
    {
        float start = -1100;
        float end = 475;
        float gap = 225;

        float midPoint = (start + end) / 2;
        int maxFit = (int)((Mathf.Abs(start) + Mathf.Abs(end)) / gap);

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            Card nextCard = cardsInHand[i];

            nextCard.transform.SetParent(keepHand);
            nextCard.transform.SetSiblingIndex(i);

            float offByOne = cardsInHand.Count - 1;
            float startingX = (cardsInHand.Count <= maxFit) ? midPoint - (gap * (offByOne / 2f)) : (start);
            float difference = (cardsInHand.Count <= maxFit) ? gap : gap * (maxFit / offByOne);

            Vector2 newPosition = new(startingX + difference * i, -540);
            StartCoroutine(nextCard.MoveCard(newPosition, 0.25f, Vector3.one));
            if (InControl())
                StartCoroutine(nextCard.RevealCard(0.25f));
        }
    }

    [PunRPC]
    public void DiscardFromHand(bool undo, int PV, int logged)
    {
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();

        if (undo)
        {
            cardsInHand.Add(card);
            card.transform.SetParent(this.keepHand);
            card.transform.localPosition = new Vector2(0, -1100);
            card.cg.alpha = 0;
        }
        else
        {
            cardsInHand.Remove(card);
            card.transform.SetParent(Manager.instance.playerDiscard);
            Log.instance.AddText($"{this.name} discards {card.name}.", logged);
            StartCoroutine(card.MoveCard(new(0, -10000), 0.25f, Vector3.one));
        }
        SortHand();
    }

    #endregion

#region Decisions

    #region Choose

    public void ChooseButton(List<string> possibleChoices, Vector3 position, string changeInstructions, Action action)
    {
        Popup popup = Instantiate(CarryVariables.instance.textPopup);
        popup.StatsSetup(this, "Choices", true, position);

        for (int i = 0; i < possibleChoices.Count; i++)
            popup.AddTextButton(possibleChoices[i]);

        AddToStack(() => Destroy(popup.gameObject), action != null);
        AddToStack(action, false);
        if (action != null) Manager.instance.Instructions(changeInstructions);
        popup.WaitForChoice();
    }

    public void ChooseCardFromPopup(List<Card> listOfCards, Vector3 position, string changeInstructions, Action action, List<float> alphas = null)
    {
        Popup popup = Instantiate(CarryVariables.instance.cardPopup);
        popup.StatsSetup(this, changeInstructions, true, position);

        AddToStack(() => Destroy(popup.gameObject), action != null);
        AddToStack(action, false);
        if (action != null) Manager.instance.Instructions(changeInstructions);

        for (int i = 0; i < listOfCards.Count; i++)
        {
            try
            {
                popup.AddCardButton(listOfCards[i], alphas[i]);
            }
            catch
            {
                popup.AddCardButton(listOfCards[i], 1);
            }
        }
        popup.WaitForChoice();
    }

    public void ChooseCardOnScreen(List<Card> listOfCards, string changeInstructions, Action action)
    {
        IEnumerator haveCardsEnabled = KeepCardsOn();
        AddToStack(Disable, action != null);
        AddToStack(action, false);

        if (action != null)
            Manager.instance.Instructions(changeInstructions);

        if (listOfCards.Count == 0 && action != null)
            PopStack();
        else if (listOfCards.Count == 1 && action != null)
            DecisionMade(0, listOfCards[0]);
        else
            StartCoroutine(haveCardsEnabled);

        IEnumerator KeepCardsOn()
        {
            float elapsedTime = 0f;
            while (elapsedTime < 0.3f)
            {
                for (int j = 0; j < listOfCards.Count; j++)
                {
                    Card nextCard = listOfCards[j];
                    int buttonNumber = j;

                    nextCard.button.onClick.RemoveAllListeners();
                    nextCard.button.interactable = true;
                    nextCard.button.onClick.AddListener(() => DecisionMade(buttonNumber, nextCard));
                    nextCard.border.gameObject.SetActive(true);
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        void Disable()
        {
            StopCoroutine(haveCardsEnabled);

            foreach (Card nextCard in listOfCards)
            {
                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = false;
                nextCard.border.gameObject.SetActive(false);
            }
        }
    }

    public void ChooseSlider(int min, int max, string changeInstructions, Vector3 position, Action action)
    {
        SliderChoice slider = Instantiate(CarryVariables.instance.sliderPopup);
        slider.StatsSetup(this, "Choose a number.", min, max, position);

        AddToStack(() => Destroy(slider.gameObject), true);
        AddToStack(action, false);
        if (action != null) Manager.instance.Instructions(changeInstructions);
    }

    #endregion

    #region Resolve Decision

    public void PopStack()
    {
        List<Action> toDo = decisionReact.Pop();
        for (int i = 0; i < toDo.Count; i++)
        {
            Action next = toDo[i];
            RememberStep(this, StepType.Revert, () => ResolveEffects(false, next, i == 0));
        }
    }

    public void AddToStack(Action action, bool newTrigger)
    {
        RememberStep(this, StepType.Revert, () => DecisionReact(false, action, newTrigger));
    }

    void DecisionReact(bool undo, Action action, bool newTrigger)
    {
        if (undo)
        {
            if (decisionReact.Count > 0)
            {
                if (newTrigger)
                    decisionReact.Pop();
                else
                    decisionReact.Peek().Remove(action);
            }
        }
        else
        {
            if (decisionReact.Count == 0 || newTrigger)
                decisionReact.Push(new());
            decisionReact.Peek().Add(action);
        }
    }

    void ResolveEffects(bool undo, Action newAction, bool newList)
    {
        if (undo)
            DecisionReact(false, newAction, newList);
        else
            newAction?.Invoke();
    }

    public void DecisionMade(int value)
    {
        choice = value;
        chosenCard = null;
        PopStack();
    }

    public void DecisionMade(int value, Card card)
    {
        choice = value;
        chosenCard = card;
        PopStack();
    }

    #endregion

    #endregion

#region Steps

    public void RememberStep(PhotonCompatible source, StepType type, Expression<Action> action)
    {
        NextStep newStep = new(source, type, action);
        historyStack.Add(newStep);
        currentStep++;
        //Debug.Log($"step {currentStep}: {action}");
        newStep.action.Compile().Invoke();
    }

    [PunRPC]
    internal void ShareSteps()
    {
        if (InControl() && PhotonNetwork.IsConnected && PhotonNetwork.CurrentRoom.MaxPlayers >= 2)
        {
            foreach (NextStep step in historyStack)
            {
                if (step.stepType == StepType.Share)
                {
                    (string instruction, object[] parameters) = step.source.TranslateFunction(step.action);
                    try
                    {
                        DoFunction(() => StepForOthers(step.source.pv.ViewID, instruction, parameters), RpcTarget.Others);
                    }
                    catch { }
                }
            }
        }
        if (InControl())
        {
            DoFunction(() => DeleteHistory());
        }
    }

    [PunRPC]
    void StepForOthers(int PV, string instruction, object[] parameters)
    {
        PhotonCompatible source = PhotonView.Find(PV).GetComponent<PhotonCompatible>();
        source.StringParameters(instruction, parameters);
    }

    [PunRPC]
    void DeleteHistory()
    {
        currentStep = -1;
        historyStack.Clear();
        Log.instance.undosInLog.Clear();
    }

    internal void UndoAmount(NextStep toThisPoint)
    {
        StartCoroutine(CarryVariables.instance.TransitionImage(1f));
        Debug.Log($"undo to {toThisPoint.action}");

        Popup[] allPopups = FindObjectsByType<Popup>(FindObjectsSortMode.None);
        foreach (Popup popup in allPopups)
        {
            if (popup.beDestroyed)
                Destroy(popup.gameObject);
        }

        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
        foreach (Card card in allCards)
        {
            card.button.interactable = false;
            card.button.onClick.RemoveAllListeners();
            card.border.gameObject.SetActive(false);
        }

        while (currentStep >= 0)
        {
            NextStep next = historyStack[currentStep];

            if (next.stepType == StepType.Share || next.stepType == StepType.Revert)
            {
                (string instruction, object[] parameters) = next.source.TranslateFunction(next.action);

                object[] newParameters = new object[parameters.Length];
                newParameters[0] = true;
                for (int i = 1; i < parameters.Length; i++)
                    newParameters[i] = parameters[i];

                next.source.StringParameters(instruction, newParameters);
            }

            if (next == toThisPoint || currentStep == 0)
            {
                this.SortHand();
                historyStack.RemoveAt(currentStep);
                currentStep--;
                RememberStep(this, StepType.UndoPoint, toThisPoint.action);
                break;
            }
            else
            {
                historyStack.RemoveAt(currentStep);
                currentStep--;
            }
        }
    }

    #endregion

#region Abilities

    public int NumberFromAbilities(string condition, object[] array, int logged)
    {
        int number = 0;
        foreach (TriggeredAbility ability in ReturnAbilities(condition, array))
            number += (int)ability.NumberAbility(logged, array);
        return number;
    }

    public bool BoolFromAbilities(bool targetBool, string condition, object[] array, int logged)
    {
        foreach (TriggeredAbility ability in ReturnAbilities(condition, array))
        {
            if ((bool)ability.BoolAbility(logged, array) == targetBool)
                return true;
        }
        return false;
    }

    public void ResolveAbilities(string condition, object[] array, int logged)
    {
        foreach (TriggeredAbility ability in ReturnAbilities(condition, array))
            ability.ResolveAbility(logged, array);
    }

    List<TriggeredAbility> ReturnAbilities(string condition, object[] array)
    {
        List<TriggeredAbility> validAbilities = new();
        this.allAbilities.RemoveAll(ability => ability == null);
        foreach (TriggeredAbility ability in this.allAbilities)
        {
            if (ability.CheckAbility(condition, array))
                validAbilities.Add(ability);
        }
        return validAbilities;
    }

    public void AddAbility(bool undo, TriggeredAbility ability)
    {
        if (undo)
            allAbilities.Remove(ability);
        else
            allAbilities.Add(ability);
    }

    public void DropAbility(bool undo, TriggeredAbility ability)
    {
        if (undo)
            allAbilities.Add(ability);
        else
            allAbilities.Remove(ability);
    }

    #endregion

#region Helpers

    public bool InControl()
    {
        if (PhotonNetwork.IsConnected)
            return this.pv.AmOwner;
        else
            return true;
    }

    void MoveScreen()
    {
        foreach (Transform transform in Manager.instance.storePlayers)
            transform.localPosition = new(0, -10000);
        this.transform.localPosition = Vector3.zero;
    }

    [PunRPC]
    public void ChangeButtonColor(bool done)
    {
        if (myButton != null)
        {
            myButton.image.color = (done) ? Color.yellow : Color.white;
        }
    }

    #endregion

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using MyBox;
using System;
using System.Linq.Expressions;

public enum Resource { Coin, Crown }
public enum StepType { Share, UndoPoint, Revert, None }
[Serializable]
public class NextStep
{
    public StepType stepType { get; private set; }
    [TextArea(5,5)] public string actionName { get; private set; }
    public PhotonCompatible source { get; private set; }
    public Expression<Action> action { get; private set; }
    public bool completed = false;

    internal NextStep(PhotonCompatible source, StepType stepType, Expression<Action> action)
    {
        this.source = source;
        this.action = action;
        this.actionName = $"{action.ToString().Replace("() => ", "")}";
        ChangeType(stepType);
    }

    internal void ChangeType(StepType stepType)
    {
        if (this.stepType == StepType.UndoPoint && stepType != StepType.UndoPoint)
        {
            Log.instance.undoToThis = null;
            //Debug.Log("undopoint canceled");
        }

        this.stepType = stepType;
        completed = stepType != StepType.UndoPoint;
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
    [SerializeField] List<NextStep> historyStack = new();
    int currentDecisionInStack = -1;
    bool makingDecision = false;

    [Foldout("Choices", true)]
    public int choice { get; private set; }
    public Card chosenCard { get; private set; }
    [SerializeField] List<Action> inReaction = new();
    [SerializeField] List<TriggeredAbility> allAbilities = new();

    #endregion

#region Setup

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();

        myEvents = new();
        resignButton = GameObject.Find("Resign Button").GetComponent<Button>();
        keepHand = transform.Find("Keep Hand");
        keepPlay = transform.Find("Keep Play");
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected && pv.AmOwner)
            DoFunction(() => SendName(PlayerPrefs.GetString("Online Username")), RpcTarget.AllBuffered);
    }

    [PunRPC]
    void SendName(string username)
    {
        pv.Owner.NickName = username;
        hoverEvent.Setup(this, $"{username}'s Events", new(0, 250));
        this.name = username;
        this.transform.SetParent(Manager.instance.storePlayers);
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
        myButton.transform.localScale = Vector3.one;
        myButton.transform.localPosition = new(-1100, 425 - (100 * playerPosition));
        myButton.transform.GetChild(0).GetComponent<TMP_Text>().text = this.name;
        myButton.onClick.AddListener(MoveScreen);
        myButton.gameObject.SetActive(PhotonNetwork.IsConnected && PhotonNetwork.CurrentRoom.MaxPlayers >= 2);

        resourceDictionary = new()
        {
            { Resource.Coin, (Application.isEditor && (!PhotonNetwork.IsConnected ||  PhotonNetwork.CurrentRoom.MaxPlayers == 1)) ? 20 : 4 },
            { Resource.Crown, 0 },
        };
        UpdateResourceText();

        if (InControl())
        {
            resignButton.onClick.AddListener(() => Manager.instance.DoFunction(() => Manager.instance.DisplayEnding(this.playerPosition)));
            DoFunction(() => DrawPlayerCards(4, 0), RpcTarget.MasterClient);

            int eventsToDraw = (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom.MaxPlayers <= 2) ? 3 : 2;
            DoFunction(() => DrawEventCards(eventsToDraw, 0), RpcTarget.MasterClient);

            Invoke(nameof(MoveScreen), 0.25f);
            RememberStep(this, StepType.UndoPoint, () => EndTurn());
            PopStack();
        }
    }

    #endregion

#region Resources

    void UpdateResourceText()
    {
        resourceText.text = KeywordTooltip.instance.EditText($"{this.name}: " +
            $"{resourceDictionary[Resource.Coin]} Coin, " +
            $"{resourceDictionary[Resource.Crown]} Crown" +
            $" | " +
            $"Total Crown: {CalculateScore()}");
    }

    public int CalculateScore()
    {
        int answer = resourceDictionary[Resource.Crown];
        foreach (PlayerCard card in cardsInPlay)
            answer += (card.GetFile() as PlayerCardData).scoringCrowns;
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
            string parathentical = source == "" ? "" : $" ({source})";
            resourceDictionary[(Resource)resource] += amount;

            if (amount >= 0)
                Log.instance.AddText($"{this.name} gets +{Mathf.Abs(amount)} {(Resource)resource}{parathentical}.", logged);
            else
                Log.instance.AddText($"{this.name} loses {Mathf.Abs(amount)} {(Resource)resource}{parathentical}.", logged);
        }
        UpdateResourceText();
    }

#endregion

#region Events

    [PunRPC]
    public void DrawEventCards(int number, int logged)
    {
        for (int i = 0; i < number; i++)
        {
            Card card = Manager.instance.eventDeck.GetChild(0).GetComponent<Card>();
            card.transform.SetParent(null);
            DoFunction(() => SendEventCardToAsker(card.pv.ViewID, logged), this.realTimePlayer);
        }
    }

    [PunRPC]
    void SendEventCardToAsker(int PV, int logged)
    {
        RememberStep(this, StepType.Share, () => ReceiveEvent(false, PV, logged));
    }

    [PunRPC]
    void ReturnEventToDeck(int PV)
    {
        EventCard card = PhotonView.Find(PV).GetComponent<EventCard>();
        ChangePopup(false, card);

        card.transform.SetParent(Manager.instance.eventDeck);
        card.transform.SetAsFirstSibling();
        card.transform.localPosition = new(0, 10000);
    }

    void ChangePopup(bool add, EventCard card)
    {
        if (add)
        {
            myEvents.Add(card);
            card.transform.SetParent(null);
            hoverEvent.AddCard(card, InControl() ? 0.75f : 0, "View Revealed\nEvents");
        }
        else
        {
            myEvents.Remove(card);
            hoverEvent.RemoveCard(card, "View Revealed\nEvents");
        }
    }

    [PunRPC]
    void ReceiveEvent(bool undo, int PV, int logged)
    {
        if (undo)
        {
            DoFunction(() => ReturnEventToDeck(PV), RpcTarget.MasterClient);
        }
        else
        {
            EventCard card = PhotonView.Find(PV).GetComponent<EventCard>();
            ChangePopup(true, card);

            if (InControl())
                Log.instance.AddText($"{this.name} draws {card.name}.", logged);
            else
                Log.instance.AddText($"{this.name} draws an Event.", logged);
        }
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

    [PunRPC]
    public void DiscardEvent(bool undo, int PV, int logged)
    {
        EventCard card = PhotonView.Find(PV).GetComponent<EventCard>();

        if (undo)
        {
            ChangePopup(true, card);
        }
        else
        {
            ChangePopup(false, card);
            card.transform.SetParent(Manager.instance.eventDiscard);
            Log.instance.AddText($"{this.name} discards {card.name}.", logged);
        }
    }

    #endregion

#region Cards In Hand

    [PunRPC]
    internal void DrawPlayerCards(int number, int logged)
    {
        for (int i = 0; i<number; i++)
        {
            Card card = Manager.instance.playerDeck.GetChild(0).GetComponent<Card>();
            card.transform.SetParent(null);
            DoFunction(() => SendPlayerCardToAsker(card.pv.ViewID, logged), this.realTimePlayer);
        }
    }

    [PunRPC]
    internal void SendPlayerCardToAsker(int PV, int logged)
    {
        RememberStep(this, StepType.Share, () => AddToHand(false, PV, logged));
    }

    [PunRPC]
    void AddToHand(bool undo, int PV, int logged)
    {
        if (undo)
        {
            DoFunction(() => ReturnPlayerCardToDeck(PV), RpcTarget.MasterClient);
            SortHand();
        }
        else
        {
            PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();
            PutInHand(card);

            if (InControl())
                Log.instance.AddText($"{this.name} draws {card.name}.", logged);
            else
                Log.instance.AddText($"{this.name} draws 1 Card.", logged);
        }
    }

    void PutInHand(PlayerCard card)
    {
        cardsInHand.Add(card);
        card.transform.localPosition = new Vector2(0, -1100);
        card.cg.alpha = 0;
        SortHand();
    }

    public void SortHand()
    {
        float start = -1100;
        float end = 475;
        float gap = 225;

        float midPoint = (start + end) / 2;
        int maxFit = (int)((Mathf.Abs(start) + Mathf.Abs(end)) / gap);
        cardsInHand = cardsInHand.OrderBy(card => (card.GetFile() as PlayerCardData).coinCost).ToList();

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
    void ReturnPlayerCardToDeck(int PV)
    {
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();
        cardsInHand.Remove(card);
        card.transform.SetParent(Manager.instance.playerDeck);
        card.transform.SetAsFirstSibling();
        StartCoroutine(card.MoveCard(new(0, -10000), 0.25f, Vector3.one));
    }

    public void DiscardPlayerCard(PlayerCard card, int logged)
    {
        RememberStep(this, StepType.Share, () => DiscardFromHand(false, card.pv.ViewID, logged));
    }

    [PunRPC]
    void DiscardFromHand(bool undo, int PV, int logged)
    {
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();

        if (undo)
        {
            PutInHand(card);
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

#region Cards In Play

    public void PlayCard(PlayerCard card, bool pay, int logged)
    {
        if (card == null)
            return;

        PlayerCardData data = card.GetFile() as PlayerCardData;

        if (pay)
        {
            int coinCost = data.coinCost + this.NumberFromAbilities(nameof(ChangeCoinCost), ChangeCoinCost.CheckParameters(card), logged);
            if (coinCost > 0)
                this.ResourceRPC(Resource.Coin, -1 * coinCost, logged);
        }
        RememberStep(this, StepType.Share, () => AddToPlay(false, card.pv.ViewID, logged));
        card.BatteryRPC(this, data.startingBattery, logged);
        ResolveAbilities(nameof(PlayedCard), PlayedCard.CheckParameters(card), logged);
    }

    [PunRPC]
    void AddToPlay(bool undo, int PV, int logged)
    {
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();
        if (undo)
        {
            card.cg.alpha = 0;
            cardsInHand.Add(card);
            cardsInPlay.Remove(card);
        }
        else
        {
            cardsInHand.Remove(card);
            cardsInPlay.Add(card);

            Log.instance.AddText($"{this.name} plays {card.name}.", logged);
            card.cg.alpha = 1;
        }
        SortHand();
        SortPlay();
    }

    public void SortPlay()
    {
        float start = -1100;
        float end = 475;
        float gap = 225;

        float midPoint = (start + end) / 2;
        int maxFit = (int)((Mathf.Abs(start) + Mathf.Abs(end)) / gap);

        for (int i = 0; i < cardsInPlay.Count; i++)
        {
            Card nextCard = cardsInPlay[i];

            nextCard.transform.SetParent(keepPlay);
            nextCard.transform.SetSiblingIndex(i);

            float offByOne = cardsInPlay.Count - 1;
            float startingX = (cardsInPlay.Count <= maxFit) ? midPoint - (gap * (offByOne / 2f)) : (start);
            float difference = (cardsInPlay.Count <= maxFit) ? gap : gap * (maxFit / offByOne);

            Vector2 newPosition = new(startingX + difference * i, 0);
            StartCoroutine(nextCard.MoveCard(newPosition, 0.25f, Vector3.one));
            StartCoroutine(nextCard.RevealCard(0.25f));
        }
    }

    #endregion

#region Leader

    [PunRPC]
    public void StartChooseEvent()
    {
        PreserveTextRPC("", 0);
        DoFunction(() => ChangeButtonColor(false));
        MoveScreen();
        ResetHistory();

        Manager.instance.DoFunction(() => Manager.instance.Instructions($"Waiting on {this.name}..."), RpcTarget.Others);
        RememberStep(this, StepType.UndoPoint, () => EndTurn());
        RememberStep(this, StepType.UndoPoint, () => ChooseEvent());
        PopStack();
    }

    void ChooseEvent()
    {
        ChooseCardFromPopup(myEvents.OfType<Card>().ToList(), Vector3.zero, "Choose an Event for all players.", PlayEvent);

        void PlayEvent()
        {
            EventCard chosenEvent = myEvents[choice];
            PreserveTextRPC($"{this.name} reveals {chosenEvent.name}.", 0);

            Manager.instance.DoFunction(() => Manager.instance.CreateEventPopup(chosenEvent.pv.ViewID), RpcTarget.All);
            RememberStep(this, StepType.Share, () => RevealEvent(false, chosenEvent.pv.ViewID));
        }
    }

    public void EndTurn()
    {
        if (Log.instance.undosInLog.Count >= 1)
            ChooseButton(new() { "End Turn" }, Vector3.zero, "Last chance to undo anything.", Done);
        else
            Done();

        void Done()
        {
            AutoNewDecision();
            DoFunction(() => ChangeButtonColor(true));
            makingDecision = false;
            Manager.instance.Instructions("Waiting on other players...");
            Log.instance.DisplayUndoBar(false);
            Log.instance.undosInLog.Clear();
            Manager.instance.DoFunction(() => Manager.instance.CompletedTurn(), RpcTarget.MasterClient);
        }
    }

    #endregion

#region Main Turn

    List<PlayerCard> resolvedCards;

    [PunRPC]
    public void StartMainTurn()
    {
        ResetHistory();
        DoFunction(() => ChangeButtonColor(false));
        MoveScreen();

        PreserveTextRPC($"", 0);
        PreserveTextRPC($"{this.name}'s Turn", 0);

        RememberStep(this, StepType.UndoPoint, () => ChooseToResolve());
        RememberStep(this, StepType.UndoPoint, () => ChooseAction());
        PopStack();
    }

    void ChooseAction()
    {
        ChooseCardFromPopup(Manager.instance.listOfActions.OfType<Card>().ToList(), Vector3.zero, "Choose an action.", Resolve);

        void Resolve()
        {
            ActionCard action = (ActionCard)chosenCard;
            PreserveTextRPC($"{this.name} chooses to {action.name}.", 0);
            resolvedCards = new();
            action.ActivateThis(this, 1);
        }
    }

    void ChooseToResolve()
    {
        List<Card> canResolve = new();
        foreach (PlayerCard card in cardsInPlay)
        {
            if (!resolvedCards.Contains(card) && card.batteryHere > 0)
                canResolve.Add(card);
        }
        if (canResolve.Count <= 1)
        {
            AutoNewDecision();
        }

        if (canResolve.Count == 0)
        { 
            FinishedMainTurn();
        }
        else
        {
            ChooseCardOnScreen(canResolve, "Choose a card to resolve.", Resolve);

            void Resolve()
            {
                PlayerCard toResolve = (PlayerCard)chosenCard;
                RememberStep(this, StepType.Revert, () => ResolveCard(false, toResolve));
            }
        }
    }

    void ResolveCard(bool undo, PlayerCard card)
    {
        if (undo)
        {
            resolvedCards.Remove(card);
        }
        else
        {
            resolvedCards.Add(card);
            RememberStep(this, StepType.UndoPoint, () => ChooseToResolve());

            PreserveTextRPC($"{this.name} resolves {card.name}.", 0);
            card.BatteryRPC(this, -1, 0);

            if (BoolFromAbilities(false, nameof(CanResolveCard), CanResolveCard.CheckParameters(card), 1))
                PopStack();
            else
                card.ActivateThis(this, 1);
        }
    }

    void FinishedMainTurn()
    {
        PreserveTextRPC($"{this.name} ends their turn.", 0);
        ResolveAbilities(nameof(EndMyTurn), EndMyTurn.CheckParameters(), 1);
        RememberStep(this, StepType.UndoPoint, () => EndTurn());
        PopStack();
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

        inReaction.Add(() => Destroy(popup.gameObject));
        if (action != null)
        {
            inReaction.Add(action);
            Manager.instance.Instructions(changeInstructions);
        }
    }

    public void ChooseCardFromPopup(List<Card> listOfCards, Vector3 position, string changeInstructions, Action action, List<float> alphas = null)
    {
        Popup popup = Instantiate(CarryVariables.instance.cardPopup);
        popup.StatsSetup(this, changeInstructions, true, position);

        inReaction.Add(() => Destroy(popup.gameObject));
        if (action != null)
        {
            inReaction.Add(action);
            Manager.instance.Instructions(changeInstructions);
        }
        for (int i = 0; i < listOfCards.Count; i++)
        {
            popup.AddCardButton(listOfCards[i], 1, true);
        }
    }

    public void ChooseCardOnScreen(List<Card> listOfCards, string changeInstructions, Action action)
    {
        IEnumerator haveCardsEnabled = KeepCardsOn();
        inReaction.Add(Disable);

        if (action != null)
        {
            inReaction.Add(action);
            Manager.instance.Instructions(changeInstructions);
        }

        if (listOfCards.Count == 0 && action != null)
            DecisionMade(-1);
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

        inReaction.Add(() => Destroy(slider.gameObject));
        if (action != null)
        {
            inReaction.Add(action);
            Manager.instance.Instructions(changeInstructions);
        }
    }

    #endregion

    #region Resolve Decision

    public void PopStack()
    {
        if (makingDecision)
        {
            Debug.LogError("ignoring pivot");
            return;
        }

        if (currentDecisionInStack >= 0)
        {
            int number = currentDecisionInStack;
            RememberStep(this, StepType.Revert, () => DecisionComplete(false, number));
        }

        List<Action> newActions = new();
        for (int i = 0; i < inReaction.Count; i++)
            newActions.Add(inReaction[i]);

        inReaction.Clear();
        foreach (Action action in newActions)
            action();

        for (int i = historyStack.Count - 1; i >= 0; i--)
        {
            NextStep step = historyStack[i];
            if (step.stepType == StepType.UndoPoint && !step.completed)
            {
                makingDecision = true;
                currentDecisionInStack = i;
                Log.instance.undoToThis = step;
                step.action.Compile().Invoke();
                return;
            }
        }
        Debug.LogError("failed to find pivot");
    }

    void DecisionComplete(bool undo, int stepNumber)
    {
        NextStep step = historyStack[stepNumber];
        if (undo)
        {
            step.completed = false;
            Debug.Log($"turned off: {step.actionName}");
        }
        else
        {
            step.completed = true;
            Debug.Log($"turned on: {step.actionName}");
        }
    }

    public void DecisionMade(int value)
    {
        choice = value;
        chosenCard = null;
        makingDecision = false;
        PopStack();
    }

    public void DecisionMade(int value, Card card)
    {
        choice = value;
        chosenCard = card;
        makingDecision = false;
        PopStack();
    }

    public void AutoNewDecision()
    {
        makingDecision = false;
        Log.instance.undoToThis = null;
    }

    #endregion

    #endregion

#region Steps

    public void RememberStep(PhotonCompatible source, StepType type, Expression<Action> action)
    {
        NextStep newStep = new(source, type, action);
        historyStack.Add(newStep);

        //Debug.Log($"step {currentStep}: {action}");
        if (type != StepType.UndoPoint)
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
            DoFunction(() => ResetHistory());
        }
    }

    [PunRPC]
    void StepForOthers(int PV, string instruction, object[] parameters)
    {
        PhotonCompatible source = PhotonView.Find(PV).GetComponent<PhotonCompatible>();
        source.StringParameters(instruction, parameters);
    }

    [PunRPC]
    void ResetHistory()
    {
        historyStack.Clear();
        Log.instance.undosInLog.Clear();
        Log.instance.DisplayUndoBar(false);
    }

    internal void UndoAmount(NextStep toThisPoint)
    {
        StartCoroutine(CarryVariables.instance.TransitionImage(1f));
        inReaction.Clear();

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

        for (int i = historyStack.Count-1; i>=0; i--)
        {
            NextStep next = historyStack[i];
            Debug.Log($"undo step {i}: {next.actionName}");

            if (next.stepType == StepType.Share || next.stepType == StepType.Revert)
            {
                (string instruction, object[] parameters) = next.source.TranslateFunction(next.action);

                object[] newParameters = new object[parameters.Length];
                newParameters[0] = true;
                for (int j = 1; j < parameters.Length; j++)
                    newParameters[j] = parameters[j];

                next.source.StringParameters(instruction, newParameters);
            }

            if (next == toThisPoint || i == 0)
            {
                Debug.Log($"continue at {toThisPoint.action}");
                this.SortHand();
                this.SortPlay();

                makingDecision = false;
                currentDecisionInStack = -1;

                PopStack();
                break;
            }
            else
            {
                historyStack.RemoveAt(i);
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

        for (int i = allAbilities.Count - 1; i >= 0; i--)
        {
            TriggeredAbility ability = allAbilities[i];
            if (ability.CheckAbility(condition, array))
                validAbilities.Add(ability);
            if (condition == nameof(EndMyTurn) && ability.justThisTurn)
                AbilityExpired(ability);
        }
        return validAbilities;
    }

    public void NewAbility(TriggeredAbility ability)
    {
        this.RememberStep(this, StepType.Revert, () => AbilityAdded(false, ability));
    }

    void AbilityAdded(bool undo, TriggeredAbility ability)
    {
        if (undo)
            allAbilities.Remove(ability);
        else
            allAbilities.Add(ability);
    }

    public void AbilityExpired(TriggeredAbility ability)
    {
        this.RememberStep(this, StepType.Revert, () => AbilityDropped(false, ability));
    }

    void AbilityDropped(bool undo, TriggeredAbility ability)
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
        currentDecisionInStack = -1;
        if (myButton != null)
            myButton.image.color = (done) ? Color.yellow : Color.white;
    }

    public void PreserveTextRPC(string text, int logged)
    {
        RememberStep(this, StepType.Share, () => TextShared(false, text, logged));
    }

    [PunRPC]
    void TextShared(bool undo, string text, int logged)
    {
        if (!undo)
            Log.instance.AddText(text, logged);
    }

    public int TotalBattery()
    {
        int answer = 0;
        foreach (PlayerCard card in cardsInPlay)
            answer += card.batteryHere;
        return answer;
    }

    public List<NextStep> SearchForSteps(string name)
    {
        List<NextStep> hasStepName = new();
        foreach (NextStep step in historyStack)
        {
            if (step.action.Body is MethodCallExpression methodCall)
            {
                if (methodCall.Method.Name == name)
                    hasStepName.Add(step);
            }
        }
        return hasStepName;
    }

    #endregion

}

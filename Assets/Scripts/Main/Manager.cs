using System.Collections;
using System.Collections.Generic;
using MyBox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Diagnostics;

public class Manager : PhotonCompatible
{

#region Variables

    public static Manager instance;

    [Foldout("Players", true)]
    public List<Player> playersInOrder { get; private set; }
    public Transform storePlayers { get; private set; }

    [Foldout("Gameplay", true)]
    List<Action> actionStack = new();
    int currentStep = -1;
    int waitingOnPlayers = 0;
    int turnNumber;
    EventCard nextEvent;
    Popup eventPopup;

    [Foldout("Cards", true)]
    public Transform playerDeck;
    public Transform playerDiscard;
    public Transform eventDeck;
    public Transform eventDiscard;
    public List<ActionCard> listOfActions = new();

    [Foldout("UI and Animation", true)]
    [SerializeField] TMP_Text instructions;
    public float opacity { get; private set; }
    bool decrease = true;
    public Canvas canvas { get; private set; }

    [Foldout("Ending", true)]
    [SerializeField] Transform endScreen;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Button quitGame;
    Stopwatch stopwatch;

    #endregion

#region Setup

    protected override void Awake()
    {
        base.Awake();
        instance = this;
        bottomType = this.GetType();
        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        storePlayers = GameObject.Find("Store Players").transform;
    }

    private void FixedUpdate()
    {
        if (decrease)
            opacity -= 0.05f;
        else
            opacity += 0.05f;
        if (opacity < 0 || opacity > 1)
            decrease = !decrease;
    }

    public GameObject MakeObject(GameObject prefab)
    {
        if (PhotonNetwork.IsConnected)
            return PhotonNetwork.Instantiate(prefab.name, Vector3.zero, new());
        else
            return Instantiate(prefab);
    }

    private void Start()
    {
        MakeObject(CarryVariables.instance.playerPrefab.gameObject);
        eventPopup = Instantiate(CarryVariables.instance.cardPopup);
        eventPopup.StatsSetup(null, "", false, new(1000, 600));
        eventPopup.gameObject.SetActive(false);

        if (PhotonNetwork.IsMasterClient)
        {
            for (int i = 0; i < CarryVariables.instance.playerCardFiles.Count; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    GameObject next = MakeObject(CarryVariables.instance.playerCardPrefab);
                    DoFunction(() => AddPlayerCard(next.GetComponent<PhotonView>().ViewID, i), RpcTarget.AllBuffered);
                }
            }
            for (int i = 0; i < CarryVariables.instance.eventCardFiles.Count; i++)
            {
                GameObject next = MakeObject(CarryVariables.instance.otherCardPrefab);
                DoFunction(() => AddEventCard(next.GetComponent<PhotonView>().ViewID, i), RpcTarget.AllBuffered);
            }
            for (int i = 0; i < CarryVariables.instance.actionFiles.Count; i++)
            {
                GameObject next = MakeObject(CarryVariables.instance.otherCardPrefab);
                DoFunction(() => AddActionCard(next.GetComponent<PhotonView>().ViewID, i), RpcTarget.AllBuffered);
            }
        }

        StartCoroutine(Setup());
    }

    IEnumerator Setup()
    {
        CoroutineGroup group = new(this);
        group.StartCoroutine(WaitForPlayers());

        IEnumerator WaitForPlayers()
        {
            if (PhotonNetwork.IsConnected)
            {
                instructions.text = $"Waiting for more players ({storePlayers.childCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})";
                while (storePlayers.childCount < PhotonNetwork.CurrentRoom.MaxPlayers)
                {
                    instructions.text = $"Waiting for more players ({storePlayers.childCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})";
                    yield return null;
                }
                instructions.text = $"All players are in.";
            }
        }

        while (group.AnyProcessing)
            yield return null;

        if (PhotonNetwork.IsMasterClient)
        {
            AddStep(ReadySetup);
            Continue();
        }
    }

    void ReadySetup()
    {
        playerDeck.Shuffle();
        eventDeck.Shuffle();
        storePlayers.Shuffle();

        for (int i = 0; i < storePlayers.childCount; i++)
            DoFunction(() => AddPlayer(storePlayers.transform.GetChild(i).GetComponent<PhotonView>().ViewID, i));
    }

    [PunRPC]
    void AddPlayer(int PV, int position)
    {
        Player nextPlayer = PhotonView.Find(PV).GetComponent<Player>();
        playersInOrder ??= new();
        playersInOrder.Insert(position, nextPlayer);
        instructions.text = "";
        nextPlayer.AssignInfo(position);
    }

    [PunRPC]
    void AddEventCard(int ID, int fileNumber)
    {
        GameObject nextObject = PhotonView.Find(ID).gameObject;
        CardData data = CarryVariables.instance.eventCardFiles[fileNumber];

        nextObject.name = data.cardName;
        nextObject.transform.SetParent(eventDeck);
        nextObject.transform.localPosition = new(250 * eventDeck.childCount, 10000);

        Type type = Type.GetType(data.cardName.Replace(" ", ""));
        if (type != null)
            nextObject.AddComponent(type);
        else
            nextObject.AddComponent(Type.GetType(nameof(EventCard)));

        Card card = nextObject.GetComponent<Card>();
        card.AssignInfo(fileNumber);
    }

    [PunRPC]
    void AddActionCard(int ID, int fileNumber)
    {
        GameObject nextObject = PhotonView.Find(ID).gameObject;
        CardData data = CarryVariables.instance.actionFiles[fileNumber];
        nextObject.name = data.cardName;

        Type type = Type.GetType(data.cardName.Replace(" ", ""));
        if (type != null)
            nextObject.AddComponent(type);
        else
            nextObject.AddComponent(Type.GetType(nameof(ActionCard)));

        ActionCard card = nextObject.GetComponent<ActionCard>();
        card.AssignInfo(fileNumber);
        listOfActions.Add(card);
    }

    [PunRPC]
    void AddPlayerCard(int ID, int fileNumber)
    {
        GameObject nextObject = PhotonView.Find(ID).gameObject;
        PlayerCardData data = CarryVariables.instance.playerCardFiles[fileNumber];

        nextObject.name = data.cardName;
        nextObject.transform.SetParent(playerDeck);
        nextObject.transform.localPosition = new(250 * playerDeck.childCount, 10000);

        Type type = Type.GetType(data.cardName.Replace(" ", ""));
        if (type != null)
            nextObject.AddComponent(type);
        else
            nextObject.AddComponent(Type.GetType(nameof(PlayerCard)));

        Card card = nextObject.GetComponent<Card>();
        card.AssignInfo(fileNumber);
    }

    #endregion

#region Gameplay Loop

    public void AddStep(Action action, int position = -1)
    {
        if (AmMaster())
        {
            if (position < 0 || currentStep < 0)
                actionStack.Add(action);
            else
                actionStack.Insert(currentStep + position, action);
        }
    }

    [PunRPC]
    internal void Continue()
    {
        if (AmMaster())
        {
            Invoke(nameof(NextAction), 0.5f);
        }
    }

    void NextAction()
    {
        if (turnNumber == 12)
        {
            DoFunction(() => DisplayEnding(-1));
        }
        else if (currentStep < actionStack.Count - 1)
        {
            playerDiscard.Shuffle();
            while (playerDiscard.childCount > 0)
            {
                Transform nextChild = playerDiscard.GetChild(0);
                nextChild.SetParent(playerDeck);
                nextChild.SetAsLastSibling();
            }
            eventDiscard.Shuffle();
            while (eventDiscard.childCount > 0)
            {
                Transform nextChild = eventDiscard.GetChild(0);
                nextChild.SetParent(eventDeck);
                nextChild.SetAsLastSibling();
            }

            currentStep++;

            if (playersInOrder != null)
                waitingOnPlayers = playersInOrder.Count;
            else if (PhotonNetwork.IsConnected)
                waitingOnPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
            else
                waitingOnPlayers = 1;

            //UnityEngine.Debug.Log($"next action: {actionStack[currentStep].Method.Name}");
            actionStack[currentStep]();
        }
        else
        {
            foreach (Player player in playersInOrder)
            {
                AddStep(() => OneEvent(player));
                AddStep(ResolveEvent);
                AddStep(EveryoneTurn);
            }
            NextAction();
        }

        void OneEvent(Player player)
        {
            turnNumber++;
            Log.instance.DoFunction(() => Log.instance.AddText($"", 0));
            Log.instance.DoFunction(() => Log.instance.AddText($"Round {turnNumber} / 12 - {player.name} is in charge", 0));

            waitingOnPlayers = 1;
            DoFunction(() => CreateEventPopup(-1));
            player.DoFunction(() => player.StartChooseEvent(), player.realTimePlayer);
        }

        void ResolveEvent()
        {
            nextEvent.ActivateThis(0);
        }

        void EveryoneTurn()
        {
            foreach (Player player in playersInOrder)
                player.DoFunction(() => player.StartMainTurn(), player.realTimePlayer);
        }
    }

    [PunRPC]
    internal void CreateEventPopup(int PV)
    {
        if (PV < 0)
        {
            eventPopup.gameObject.SetActive(false);
        }
        else
        {
            nextEvent = PhotonView.Find(PV).GetComponent<EventCard>();
            eventPopup.gameObject.SetActive(true);

            eventPopup.RemoveButton(0);
            eventPopup.AddCardButton(nextEvent, 1, false);
            eventPopup.DisableButton(0);
        }
    }

    [PunRPC]
    internal void CompletedTurn()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Continue();
        }
        else
        {
            waitingOnPlayers--;
            if (waitingOnPlayers == 0)
            {
                foreach (Player player in playersInOrder)
                    player.pv.RPC(nameof(player.ShareSteps), player.realTimePlayer);
                Continue();
            }
        }
    }

    #endregion

#region Ending

    [PunRPC]
    public void DisplayEnding(int resignPosition)
    {
        Popup[] allPopups = FindObjectsByType<Popup>(FindObjectsSortMode.None);
        foreach (Popup popup in allPopups)
            Destroy(popup.gameObject);

        List<Player> playerScoresInOrder = playersInOrder.OrderByDescending(player => player.CalculateScore()).ToList();
        int nextPlacement = 1;

        Log.instance.AddText("");
        Log.instance.AddText("The game has ended.");
        Instructions("The game has ended.");
        scoreText.text = "";

        Player resignPlayer = null;
        if (resignPosition >= 0)
        {
            resignPlayer = playersInOrder[resignPosition];
            Log.instance.AddText($"{resignPlayer.name} has resigned.");
        }

        for (int i = 0; i < playerScoresInOrder.Count; i++)
        {
            Player player = playerScoresInOrder[i];
            if (player != resignPlayer)
            {
                EndstatePlayer(player, false);
                if (i == 0 || playerScoresInOrder[i - 1].CalculateScore() != player.CalculateScore())
                    nextPlacement++;
            }
        }

        if (resignPlayer != null)
            EndstatePlayer(resignPlayer, true);
        scoreText.text = KeywordTooltip.instance.EditText(scoreText.text);

        endScreen.gameObject.SetActive(true);
        quitGame.onClick.AddListener(Leave);
    }

    void EndstatePlayer(Player player, bool resigned)
    {
        scoreText.text += $"\n\n{player.name} - {player.CalculateScore()} VP {(resigned ? $"[Resigned]" : "")}\n";
    }

    void Leave()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("1. Lobby");
        }
        else
        {
            SceneManager.LoadScene("0. Loading");
        }
    }
    #endregion

#region Misc

    public Player FindThisPlayer()
    {
        foreach (Player player in playersInOrder)
        {
            if (player.InControl())
                return player;
        }
        return null;
    }

    public bool AmMaster()
    {
        if (PhotonNetwork.IsConnected)
            return PhotonNetwork.IsMasterClient;
        else
            return true;
    }

    [PunRPC]
    internal void Instructions(string text)
    {
        instructions.text = KeywordTooltip.instance.EditText(text);
    }

    #endregion

}

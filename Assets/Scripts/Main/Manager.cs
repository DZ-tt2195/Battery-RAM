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
    List<Action> damageStack = new();
    List<TriggeredAbility> allAbilities = new();

    [Foldout("Cards", true)]
    public Transform playerDeck;
    public Transform playerDiscard;

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

        if (PhotonNetwork.IsMasterClient)
        {
            for (int i = 0; i < CarryVariables.instance.cardFiles.Count; i++)
            {
                GameObject next = MakeObject(CarryVariables.instance.cardPrefab);
                DoFunction(() => AddCard(next.GetComponent<PhotonView>().ViewID, i), RpcTarget.AllBuffered);
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
                    instructions.text = $"Waiting for more players ({storePlayers.childCount} / {PhotonNetwork.CurrentRoom.MaxPlayers})";
                    yield return null;
                }
                instructions.text = $"All players are in.";
            }
        }

        while (group.AnyProcessing)
            yield return null;

        if (PhotonNetwork.IsMasterClient)
        {
            ReadySetup();
        }
    }

    void ReadySetup()
    {
        playerDeck.Shuffle();
        storePlayers.Shuffle();
        for (int i = 0; i < storePlayers.childCount; i++)
        {
            Player player = storePlayers.transform.GetChild(i).GetComponent<Player>();
            DoFunction(() => AddPlayer(player.name, i));
        }
        Continue();
    }

    [PunRPC]
    void AddPlayer(string name, int position)
    {
        Player nextPlayer = GameObject.Find(name).GetComponent<Player>();
        playersInOrder ??= new();
        playersInOrder.Insert(position, nextPlayer);
        instructions.text = "";
        nextPlayer.AssignInfo(position);
    }

    [PunRPC]
    void AddCard(int ID, int fileNumber)
    {
        GameObject nextObject = PhotonView.Find(ID).gameObject;
        CardData data = CarryVariables.instance.cardFiles[fileNumber];
        /*
        nextObject.name = data.name;
        nextObject.transform.SetParent(deck);

        allCards ??= new();
        nextObject.transform.localPosition = new(250 * allCards.Count, 10000);

        Type type = Type.GetType(data.name.Replace(" ", ""));
        if (type != null)
            nextObject.AddComponent(type);
        else if (data.type == CardType.Troop)
            nextObject.AddComponent(Type.GetType(nameof(TroopCard)));
        else if (data.type == CardType.Spell)
            nextObject.AddComponent(Type.GetType(nameof(SpellCard)));
        else
            Debug.LogError($"failed to add type {data.name}/{data.type}");
        */
        Card card = nextObject.GetComponent<Card>();
        //card.AssignInfo(allCards.Count, data);
    }

    #endregion

#region Gameplay Loop

    void AddStep(Action action, int position = -1)
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            if (position < 0 || currentStep < 0)
                actionStack.Add(action);
            else
                actionStack.Insert(currentStep + position, action);
        }
    }

    [PunRPC]
    public void Instructions(string text)
    {
        instructions.text = (text);
    }

    [PunRPC]
    public void Continue()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            Invoke(nameof(NextAction), 0.25f);
        }
    }

    void NextAction()
    {
        if (currentStep < actionStack.Count - 1)
        {
            foreach (Action action in damageStack)
                action();
            damageStack.Clear();

            currentStep++;
            actionStack[currentStep]();
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

        /*
        List<Player> playerScoresInOrder = playersInOrder.OrderByDescending(player => player.CalculateScore()).ToList();
        int nextPlacement = 1;
        scoreText.text += $"Game length: {CalculateTime()}\n";
        */
        Log.instance.AddText("");
        Log.instance.AddText("The game has ended.");
        Instructions("The game has ended.");

        Player resignPlayer = null;
        if (resignPosition >= 0)
        {
            resignPlayer = playersInOrder[resignPosition];
            Log.instance.AddText($"{resignPlayer.name} has resigned.");
        }
        /*
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
        */
        if (resignPlayer != null)
            EndstatePlayer(resignPlayer, true);
        scoreText.text = KeywordTooltip.instance.EditText(scoreText.text);

        endScreen.gameObject.SetActive(true);
        quitGame.onClick.AddListener(Leave);
    }

    void EndstatePlayer(Player player, bool resigned)
    {
        //scoreText.text += $"\n\n{player.name} - {player.CalculateScore()} VP {(resigned ? $"[Resigned on turn {turnNumber}]" : "")}\n";
    }

    string CalculateTime(Stopwatch stopwatch)
    {
        TimeSpan time = stopwatch.Elapsed;
        string part = time.Seconds < 10 ? $"0{time.Seconds}" : $"{time.Seconds}";
        return $"{time.Minutes}:{part}";
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

    #endregion

}

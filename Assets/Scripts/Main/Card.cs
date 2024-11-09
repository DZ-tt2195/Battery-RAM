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

#region Ministeps

    protected void Advance(bool undo, Player player, CardData dataFile, int logged)
    {
        if (dataFile.useSheets)
        {
            if (undo)
            {
                stepCounter--;
            }
            else
            {
                stepCounter++;
                if (stepCounter < activationSteps.Count)
                    StringParameters(activationSteps[stepCounter], new object[3] { player, dataFile, logged });
                else
                    player.PopStack();
            }
        }
    }

    protected void CheckBool(bool answer, Player player, CardData dataFile, int logged)
    {
        if (answer)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        else if (activationSteps.Count != 0)
            player.PopStack();
    }

    #region +/- Resources

    protected void DrawCard(Player player, CardData dataFile, int logged)
    {
        player.DoFunction(() => player.DrawPlayerCards(dataFile.cardAmount, logged), RpcTarget.MasterClient);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void AddCoin(Player player, CardData dataFile, int logged)
    {
        player.ResourceRPC(Resource.Coin, dataFile.coinAmount, logged);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void LoseCoin(Player player, CardData dataFile, int logged)
    {
        player.ResourceRPC(Resource.Coin, -1 * dataFile.coinAmount, logged);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void AddCrown(Player player, CardData dataFile, int logged)
    {
        player.ResourceRPC(Resource.Crown, dataFile.crownAmount, logged);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void LoseCrown(Player player, CardData dataFile, int logged)
    {
        player.ResourceRPC(Resource.Crown, -1* dataFile.crownAmount, logged);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    #endregion

    #region Play

    List<PlayerCard> canPlay;

    protected void PlayCombine(Player player, CardData dataFile, int logged)
    {
        canPlay = player.cardsInHand.Where(card => card.CanPayCost(player)).ToList();

        if (canPlay.Count >= 1)
            player.RememberStep(this, StepType.None, () => ChoosePlay(player, dataFile, logged));
        else
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    void ChoosePlay(Player player, CardData dataFile, int logged)
    {
        player.ChooseCardOnScreen(canPlay.OfType<Card>().ToList(), $"Choose a card to play.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard cardToPlay = (PlayerCard)player.chosenCard;
                player.PlayCard(cardToPlay, true, logged);
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't play anything.", logged);
            }
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        }
    }

    #endregion

    #region Setters

    protected void SetAllStats(int number, CardData dataFile)
    {
        float multiplier = (dataFile.miscAmount > 0) ? dataFile.miscAmount : -1f / dataFile.miscAmount;
        dataFile.cardAmount = (int)Mathf.Floor(number * multiplier);
        dataFile.coinAmount = (int)Mathf.Floor(number * multiplier);
        dataFile.crownAmount = (int)Mathf.Floor(number * multiplier);
        dataFile.batteryAmount = (int)Mathf.Floor(number * multiplier);
    }

    protected void SetToHand(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.cardsInHand.Count, dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void SetToMoney(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.resourceDictionary[Resource.Coin], dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    #endregion

    #region Booleans

    protected void HandOrMore(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.cardsInHand.Count >= dataFile.miscAmount, player, dataFile, logged);
    }

    protected void HandOrLess(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.cardsInHand.Count <= dataFile.miscAmount, player, dataFile, logged);
    }

    protected void MoneyOrMore(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.resourceDictionary[Resource.Coin] >= dataFile.miscAmount, player, dataFile, logged);
    }

    protected void MoneyOrLess(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.resourceDictionary[Resource.Coin] <= dataFile.miscAmount, player, dataFile, logged);
    }

    #endregion

    #region Discarding

    protected void DiscardCard(Player player, CardData dataFile, int logged)
    {
        Action action = () => player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        player.AddToStack(() => action(), true);

        player.RememberStep(this, (player.cardsInHand.Count <= 1) ? StepType.None : StepType.UndoPoint,
            () => ChooseDiscard(player, dataFile, false, 1, logged));
    }

    protected void AskDiscard(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count == 0)
        {
            CheckBool(false, player, dataFile, logged);
            return;
        }

        player.AddToStack(() => FinishedDiscarding(), true);
        player.RememberStep(this, StepType.UndoPoint, () => ChooseDiscard(player, dataFile, true, 1, logged));

        void FinishedDiscarding()
        {
            CheckBool(sideCounter == dataFile.cardAmount, player, dataFile, logged);
        }
    }

    void ChooseDiscard(Player player, CardData dataFile, bool optional, int counter, int logged)
    {
        sideCounter = counter;
        string parathentical = (dataFile.cardAmount == 1) ? "" : $"({counter}/{dataFile.cardAmount})";
        if (optional)
            player.ChooseButton(new() { "Decline" }, new(0, 250), "", null);
        player.ChooseCardOnScreen(player.cardsInHand.OfType<Card>().ToList(), $"Discard a card {parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                sideCounter++;
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                player.RememberStep(player, StepType.UndoPoint, () => player.DiscardFromHand(false, playerCard.pv.ViewID, logged));

                if (counter == dataFile.cardAmount)
                    player.PopStack();
                else
                    player.RememberStep(this, (player.cardsInHand.Count == 1) ? StepType.None : StepType.UndoPoint,
                        () => ChooseDiscard(player, dataFile, false, sideCounter, logged));
            }
            else
            {
                if (optional)
                    player.PreserveTextRPC($"{player.name} doesn't discard to {this.name}.", logged);
                player.PopStack();
            }
        }
    }

    #endregion

    #region Ask Pay

    protected void AskLoseCoin(Player player, CardData dataFile, int logged)
    {
        if (player.resourceDictionary[Resource.Coin] < dataFile.coinAmount)
        {
            CheckBool(false, player, dataFile, logged);
            return;
        }

        Action action = () => LoseCoin(player, dataFile, logged);
        player.RememberStep(this, StepType.UndoPoint, () => ChoosePay(player, () => action(),
            $"Pay {dataFile.coinAmount} Coin to {this.name}?", dataFile, logged));
    }

    void ChoosePay(Player player, Action ifDone, string text, CardData dataFile, int logged)
    {
        player.ChooseButton(new() { "Yes", "No" }, new(0, 250), text, Next);

        void Next()
        {
            if (player.choice == 0)
            {
                ifDone();
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't pay for {this.name}.", logged);
                CheckBool(false, player, dataFile, logged);
            }
        }
    }

    #endregion

    #endregion

}
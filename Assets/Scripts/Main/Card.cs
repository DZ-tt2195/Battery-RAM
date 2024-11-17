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
    protected RightClickMe rcm;

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
        rcm = GetComponent<RightClickMe>();
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
        rcm.AssignInfo(cg, dataFile.artCredit);
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

    public virtual CardData GetFile()
    {
        return null;
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
        else if (!undo)
        {
            player.PopStack();
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

    protected void SetToPlayArea(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.cardsInPlay.Count, dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void SetToCoin(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.resourceDictionary[Resource.Coin], dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void SetToTotalBattery(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.TotalBattery(), dataFile);
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

    protected void TotalBatteryOrMore(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.TotalBattery() >= dataFile.miscAmount, player, dataFile, logged);
    }

    protected void TotalBatteryOrLess(Player player, CardData dataFile, int logged)
    {
        CheckBool(player.TotalBattery() <= dataFile.miscAmount, player, dataFile, logged);
    }

    #endregion

    #region Play

    List<PlayerCard> canPlay;

    protected bool CanPlayCards(Player player)
    {
        canPlay = player.cardsInHand.Where(card => card.CanPayCost(player)).ToList();
        return canPlay.Count >= 1;
    }

    protected void PlayCard(Player player, CardData dataFile, int logged)
    {
        if (CanPlayCards(player))
        {
            player.RememberStep(this, StepType.UndoPoint, () => ChoosePlay(player, dataFile, logged));
        }
        else
        {
            player.PreserveTextRPC($"{player.name} can't play anything.", logged);
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        }
    }

    void ChoosePlay(Player player, CardData dataFile, int logged)
    {
        player.ChooseButton(new() { "Decline" }, new(0, 250), $"Choose a card to play with {this.name}.", Next);
        player.ChooseCardOnScreen(canPlay.OfType<Card>().ToList(), "", null);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard cardToPlay = (PlayerCard)player.chosenCard;
                player.PlayCard(cardToPlay, true, logged);
                PostPlaying(player, cardToPlay, dataFile, logged);
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't play anything with {this.name}.", logged);
                PostPlaying(player, null, dataFile, logged);
            }
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        }
    }

    protected virtual void PostPlaying(Player player, PlayerCard cardToPlay, CardData dataFile, int logged)
    {
    }

    #endregion

    #region Discard

    protected void DiscardCard(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count <= dataFile.cardAmount)
        {
            DiscardAll(player, dataFile, logged);
        }
        else
        {
            player.AddToStack(() => player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged)), true);
            player.RememberStep(this, StepType.UndoPoint, () => ChooseDiscard(player, dataFile, false, 1, logged));
        }
    }

    void DiscardAll(Player player, CardData dataFile, int logged)
    {
        for (int i = 0; i<player.cardsInHand.Count; i++)
            player.DiscardPlayerCard(player.cardsInHand[0], logged);
        PostDiscarding(player, true, dataFile, logged);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void AskDiscard(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count < dataFile.cardAmount)
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
        string parathentical = (dataFile.cardAmount == 1) ? "" : $" ({counter}/{dataFile.cardAmount})";
        if (optional)
            player.ChooseButton(new() { "Decline" }, new(0, 250), "", null);
        player.ChooseCardOnScreen(player.cardsInHand.OfType<Card>().ToList(), $"Discard Card to {this.name}{parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                player.DiscardPlayerCard(playerCard, logged);

                if (counter == dataFile.cardAmount)
                {
                    PostDiscarding(player, true, dataFile, logged);
                    player.PopStack();
                }
                else
                {
                    player.RememberStep(this, (player.cardsInHand.Count == 1) ? StepType.None : StepType.UndoPoint,
                        () => ChooseDiscard(player, dataFile, false, sideCounter+1, logged));
                }
            }
            else
            {
                if (optional)
                    player.PreserveTextRPC($"{player.name} doesn't discard to {this.name}.", logged);
                PostDiscarding(player, false, dataFile, logged);
                player.PopStack();
            }
        }
    }

    protected virtual void PostDiscarding(Player player, bool success, CardData dataFile, int logged)
    {
    }

    #endregion

    #region Add Battery

    protected void AddBattery(Player player, CardData dataFile, int logged)
    {
        if (player.BoolFromAbilities(false, nameof(CanAddBattery), CanAddBattery.CheckParameters(), logged))
        {
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
        }
        else
        {
            player.AddToStack(() => player.RememberStep(this, StepType.Revert,
                () => Advance(false, player, dataFile, logged)), true);
            player.RememberStep(this, (player.cardsInPlay.Count <= 1) ? StepType.None : StepType.UndoPoint,
                () => ChooseAddBattery(player, dataFile, 1, logged));
        }
    }

    void ChooseAddBattery(Player player, CardData dataFile, int counter, int logged)
    {
        sideCounter = counter;
        string parathentical = (dataFile.batteryAmount == 1) ? "" : $" ({counter}/{dataFile.batteryAmount})";
        player.ChooseCardOnScreen(player.cardsInPlay.OfType<Card>().ToList(), $"Add a Battery{parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, 1, logged, this.name);

                if (counter == dataFile.batteryAmount)
                {
                    PostAddBattery(player, dataFile, logged);
                    player.PopStack();
                }
                else
                {
                    player.RememberStep(this, StepType.UndoPoint, () => ChooseAddBattery(player, dataFile, sideCounter+1, logged));
                }
            }
            else
            {
                player.PreserveTextRPC($"{player.name} can't add any Battery.", logged);
                player.PopStack();
            }
        }
    }

    protected virtual void PostAddBattery(Player player, CardData dataFile, int logged)
    {
    }

    #endregion

    #region Lose Battery

    protected void LoseBattery(Player player, CardData dataFile, int logged)
    {
        player.AddToStack(() => player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged)), true);

        player.RememberStep(this, (player.TotalBattery() <= 1) ? StepType.None : StepType.UndoPoint,
            () => ChooseLoseBattery(player, dataFile, false, 1, logged));
    }

    protected void AskLoseBattery(Player player, CardData dataFile, int logged)
    {
        if (player.TotalBattery() < dataFile.batteryAmount)
        {
            CheckBool(false, player, dataFile, logged);
            return;
        }

        player.AddToStack(() => FinishedRemoving(), true);
        player.RememberStep(this, StepType.UndoPoint, () => ChooseLoseBattery(player, dataFile, true, 1, logged));

        void FinishedRemoving()
        {
            CheckBool(sideCounter == dataFile.batteryAmount, player, dataFile, logged);
        }
    }

    protected void ChooseLoseBattery(Player player, CardData dataFile, bool optional, int counter, int logged)
    {
        sideCounter = counter;
        string parathentical = (dataFile.batteryAmount == 1) ? "" : $" ({counter}/{dataFile.batteryAmount})";
        if (optional)
            player.ChooseButton(new() { "Decline" }, new(0, -250), "", null);
        player.ChooseCardOnScreen(player.cardsInPlay.Where(card => card.batteryHere >= 1).OfType<Card>().ToList(), $"Lose a Battery to {this.name}{parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, -1, logged, this.name);

                if (counter == dataFile.batteryAmount)
                {
                    PostLoseBattery(player, true, dataFile, logged);
                    player.PopStack();
                }
                else
                {
                    player.RememberStep(this, (player.TotalBattery() <= 1) ? StepType.None : StepType.UndoPoint,
                        () => ChooseLoseBattery(player, dataFile, false, sideCounter+1, logged));
                }
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't remove any Battery.", logged);
                PostLoseBattery(player, false, dataFile, logged);
                player.PopStack();
            }
        }
    }

    protected virtual void PostLoseBattery(Player player, bool success, CardData dataFile, int logged)
    {
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
        if (dataFile.coinAmount == 0)
        {
            action();
        }
        else
        {
            player.RememberStep(this, StepType.UndoPoint, () => ChoosePay(player, () => action(),
                $"Pay {dataFile.coinAmount} Coin to {this.name}?", dataFile, logged));
        }
    }

    protected void AskLoseCrown(Player player, CardData dataFile, int logged)
    {
        Action action = () => LoseCrown(player, dataFile, logged);

        if (dataFile.crownAmount == 0)
        {
            action();
        }
        else
        {
            player.RememberStep(this, StepType.UndoPoint, () => ChoosePay(player, () => action(),
                $"Lose {dataFile.crownAmount} Crown for {this.name}?", dataFile, logged));
        }
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
                player.PreserveTextRPC($"{player.name} doesn't use {this.name}.", logged);
                CheckBool(false, player, dataFile, logged);
            }
        }
    }

    #endregion

    #endregion

}

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
    bool mayStopEarly;

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

    #region Misc

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
            }
        }
    }

    protected void ChangeSideCount(bool undo, int change)
    {
        if (undo)
            sideCounter-=change;
        else
            sideCounter+=change;
    }

    protected void SetSideCount(bool undo, int newNumber)
    {
        ChangeSideCount(undo, newNumber - sideCounter);
    }

    #endregion

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

    protected void SetToBattery(Player player, CardData dataFile, int logged)
    {
        SetAllStats(player.TotalBattery(), dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    #endregion

    #region Booleans

    protected void HandOrMore(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count >= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void HandOrLess(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count <= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void PlayAreaOrMore(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInPlay.Count >= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void PlayAreaOrLess(Player player, CardData dataFile, int logged)
    {
        if (player.cardsInPlay.Count <= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void CoinOrMore(Player player, CardData dataFile, int logged)
    {
        if (player.resourceDictionary[Resource.Coin] >= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void CoinOrLess(Player player, CardData dataFile, int logged)
    {
        if (player.resourceDictionary[Resource.Coin] <= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void BatteryOrMore(Player player, CardData dataFile, int logged)
    {
        if (player.TotalBattery() >= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    protected void BatteryOrLess(Player player, CardData dataFile, int logged)
    {
        if (player.TotalBattery() <= dataFile.miscAmount)
            player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
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
            player.RememberStep(this, StepType.UndoPoint, () => ChooseDiscard(player, dataFile, false, logged));
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
        mayStopEarly = true;
        if (player.cardsInHand.Count < dataFile.cardAmount)
            return;

        player.RememberStep(this, StepType.Revert, () => SetSideCount(false, 0));
        player.RememberStep(this, StepType.UndoPoint, () => ChooseDiscard(player, dataFile, true, logged));
    }

    void ChooseDiscard(Player player, CardData dataFile, bool optional, int logged)
    {
        player.RememberStep(this, StepType.Revert, () => ChangeSideCount(false, 1));
        List<Card> cardsToChoose = player.cardsInHand.OfType<Card>().ToList();
        string parathentical = (dataFile.cardAmount == 1) ? "" : $" ({sideCounter}/{dataFile.cardAmount})";

        if (optional)
        {
            player.ChooseButton(new() { "Decline" }, new(0, 250), $"Discard to {this.name}{parathentical}.", Next);
            player.ChooseCardOnScreen(cardsToChoose, $"", null);
        }
        else 
        {
            if (cardsToChoose.Count <= 1)
                player.AutoNewDecision();
            player.ChooseCardOnScreen(cardsToChoose, $"Discard to {this.name}{parathentical}.", Next);
        }

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                player.DiscardPlayerCard(playerCard, logged);

                if (sideCounter == dataFile.cardAmount)
                {
                    PostDiscarding(player, true, dataFile, logged);
                    player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
                }
                else
                {
                    player.RememberStep(this, StepType.UndoPoint, () => ChooseDiscard(player, dataFile, false, logged));
                }
            }
            else
            {
                if (optional)
                    player.PreserveTextRPC($"{player.name} doesn't discard to {this.name}.", logged);
                PostDiscarding(player, false, dataFile, logged);

                if (!mayStopEarly)
                    player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
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
            player.RememberStep(this, StepType.Revert, () => SetSideCount(false, 0));
            player.RememberStep(this, StepType.UndoPoint, () => ChooseAddBattery(player, dataFile, logged));
        }
    }

    void ChooseAddBattery(Player player, CardData dataFile, int logged)
    {
        player.RememberStep(this, StepType.Revert, () => ChangeSideCount(false, 1));
        List<Card> cardsToChoose = player.cardsInPlay.OfType<Card>().ToList();

        if (cardsToChoose.Count <= 1)
            player.AutoNewDecision();

        string parathentical = (dataFile.batteryAmount == 1) ? "" : $" ({sideCounter}/{dataFile.batteryAmount})";
        player.ChooseCardOnScreen(cardsToChoose, $"Add a Battery{parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, 1, logged, this.name);

                if (sideCounter == dataFile.batteryAmount)
                {
                    PostAddBattery(player, dataFile, logged);
                    player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
                }
                else
                {
                    player.RememberStep(this, StepType.UndoPoint, () => ChooseAddBattery(player, dataFile, logged));
                }
            }
            else
            {
                player.PreserveTextRPC($"{player.name} can't add any Battery.", logged);
                player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
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
        player.RememberStep(this, StepType.Revert, () => SetSideCount(false, 0));
        player.RememberStep(this, StepType.UndoPoint, () => ChooseLoseBattery(player, dataFile, false, logged));
    }

    protected void AskLoseBattery(Player player, CardData dataFile, int logged)
    {
        mayStopEarly = true;
        if (player.TotalBattery() < dataFile.batteryAmount)
            return;

        player.RememberStep(this, StepType.Revert, () => SetSideCount(false, 0));
        player.RememberStep(this, StepType.UndoPoint, () => ChooseLoseBattery(player, dataFile, true, logged));
    }

    protected void ChooseLoseBattery(Player player, CardData dataFile, bool optional, int logged)
    {
        List<Card> cardsToChoose = player.cardsInPlay.Where(card => card.batteryHere >= 1).OfType<Card>().ToList();
        player.RememberStep(this, StepType.Revert, () => ChangeSideCount(false, 1));
        string parathentical = (dataFile.batteryAmount == 1) ? "" : $" ({sideCounter}/{dataFile.batteryAmount})";

        if (optional)
            player.ChooseButton(new() { "Decline" }, new(0, 250), "", null);
        else if (cardsToChoose.Count <= 1)
            player.AutoNewDecision();

        player.ChooseCardOnScreen(cardsToChoose, $"Lose a Battery to {this.name}{parathentical}.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, -1, logged, this.name);

                if (sideCounter == dataFile.batteryAmount)
                {
                    PostLoseBattery(player, true, dataFile, logged);
                }
                else
                {
                    player.RememberStep(this, StepType.UndoPoint, () => ChooseLoseBattery(player, dataFile, false, logged));
                }
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't remove any Battery.", logged);
                PostLoseBattery(player, false, dataFile, logged);

                if (!mayStopEarly)
                    player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
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
            return;

        Action action = () => AddCoin(player, dataFile, logged);
        if (dataFile.coinAmount == 0)
        {
            action();
        }
        else
        {
            player.RememberStep(this, StepType.UndoPoint, () => ChoosePay(player, action,
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
            player.RememberStep(this, StepType.UndoPoint, () => ChoosePay(player, action,
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
                player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't use {this.name}.", logged);
            }
        }
    }

    #endregion

    #endregion

}

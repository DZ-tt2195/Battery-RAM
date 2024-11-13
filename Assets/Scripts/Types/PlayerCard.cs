using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerCard : Card
{

#region Setup

    PlayerCardData dataFile;
    TMP_Text cardStats;
    TMP_Text batteryText;
    public int batteryHere { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
        cardStats = cg.transform.Find("Card Stats").GetComponent<TMP_Text>();
        batteryText = this.transform.Find("Battery Image").GetComponentInChildren<TMP_Text>();
    }

    internal override void AssignInfo(int fileNumber)
    {
        dataFile = CarryVariables.instance.playerCardFiles[fileNumber];
        background.color = Color.blue;
        GetInstructions(dataFile);
        UpdateBatteryText();

        cardName.text = KeywordTooltip.instance.EditText($"{dataFile.cardName}   {dataFile.coinCost} Coin");
        cardStats.text = KeywordTooltip.instance.EditText($"{dataFile.startingBattery} Battery | {dataFile.scoringCrowns} Crown");
        cardDescription.text = KeywordTooltip.instance.EditText(dataFile.textBox);
    }

    public override CardData GetFile()
    {
        return dataFile;
    }

    public virtual void ActivateThis(Player player, int logged)
    {
        stepCounter = -1;
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    public bool CanPayCost(Player player)
    {
        int coinCost = this.dataFile.coinCost + player.NumberFromAbilities(nameof(ChangeCoinCost), ChangeCoinCost.CheckParameters(this), -1);
        return player.resourceDictionary[Resource.Coin] >= coinCost;
    }

    #endregion

#region Battery

    public void BatteryRPC(Player player, int number, int logged, string source = "")
    {
        if (number > 0 && player.BoolFromAbilities(false, nameof(CanAddBattery), CanAddBattery.CheckParameters(), logged))
        {
            return;
        }
        else
        {
            int actualAmount = number;
            if (batteryHere + number < 0)
                actualAmount = -1 * batteryHere;
            if (actualAmount != 0)
                player.RememberStep(this, StepType.Share, () => this.ChangeBattery(false, player.playerPosition, actualAmount, source, logged));
            UpdateBatteryText();
        }
    }

    [PunRPC]
    void ChangeBattery(bool undo, int playerPosition, int amount, string source, int logged)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        if (undo)
        {
            batteryHere -= amount;
        }
        else
        {
            string parathentical = source == "" ? "" : $" ({source})";
            batteryHere += amount;

            if (amount >= 0)
                Log.instance.AddText($"{player.name} adds {Mathf.Abs(amount)} Battery to {this.name}{parathentical}.", logged);
            else
                Log.instance.AddText($"{player.name} removes {Mathf.Abs(amount)} Battery from {this.name}{parathentical}.", logged);
        }
        UpdateBatteryText();
    }

    void UpdateBatteryText()
    {
        batteryText.text = KeywordTooltip.instance.EditText($"{batteryHere} Battery");
        batteryText.transform.parent.gameObject.SetActive(batteryHere > 0);
    }

    #endregion

#region Ministeps

    protected void SetToBatteryHere(Player player, CardData dataFile, int logged)
    {
        SetAllStats(this.batteryHere, dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    #endregion

}

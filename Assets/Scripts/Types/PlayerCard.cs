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
    public int batteriesHere { get; private set; }

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

        cardName.text = KeywordTooltip.instance.EditText($"{dataFile.name}     {dataFile.coinCost} Coin");
        cardStats.text = KeywordTooltip.instance.EditText($"{dataFile.startingBatteries} Battery | {dataFile.scoringCrowns} Crown");
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

#region Batteries

    public void BatteryRPC(Player player, int number, int logged, string source = "")
    {
        int actualAmount = number;
        if (batteriesHere + number < 0)
            actualAmount = -1 * batteriesHere;
        if (actualAmount != 0)
            player.RememberStep(this, StepType.Share, () => this.ChangeBattery(false, player.playerPosition, actualAmount, source, logged));
        UpdateBatteryText();
    }

    [PunRPC]
    void ChangeBattery(bool undo, int playerPosition, int amount, string source, int logged)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        if (undo)
        {
            batteriesHere -= amount;
        }
        else
        {
            string parathentical = source == "" ? "" : $"({source})";
            batteriesHere += amount;

            if (amount >= 0)
                Log.instance.AddText($"{player.name} adds {Mathf.Abs(amount)} Battery to {this.name} {parathentical}.", logged);
            else
                Log.instance.AddText($"{player.name} removes {Mathf.Abs(amount)} Battery to {this.name} {parathentical}.", logged);
        }
        UpdateBatteryText();
    }

    void UpdateBatteryText()
    {
        batteryText.text = KeywordTooltip.instance.EditText($"{batteriesHere} Battery");
        batteryText.transform.parent.gameObject.SetActive(batteriesHere > 0);
    }

    #endregion

#region Ministeps

    protected void SetToBatteryHere(Player player, CardData dataFile, int logged)
    {
        SetAllStats(this.batteriesHere, dataFile);
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }

    #endregion

}

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class PlayerCard : Card, IPointerClickHandler
{
    public PlayerCardData dataFile { get; protected set; }
    TMP_Text cardStats;
    TMP_Text batteryText;

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
        cardStats = cg.transform.Find("Card Stats").GetComponent<TMP_Text>();
        batteryText = this.transform.Find("Battery Image").GetComponentInChildren<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            CarryVariables.instance.RightClickDisplay(cg, dataFile.artCredit.Replace("/", "\n"));
        }
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
}

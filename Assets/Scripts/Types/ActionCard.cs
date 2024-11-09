using TMPro;
using UnityEngine;

public class ActionCard : Card
{
    public CardData dataFile { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    internal override void AssignInfo(int fileNumber)
    {
        this.dataFile = CarryVariables.instance.actionFiles[fileNumber];
        cardDescription.text = KeywordTooltip.instance.EditText(dataFile.textBox);
        GetInstructions(dataFile);
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
}

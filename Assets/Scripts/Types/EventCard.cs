using Photon.Pun;
using UnityEngine;

public class EventCard : Card
{
    public CardData dataFile { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    internal override void AssignInfo(int fileNumber)
    {
        this.dataFile = CarryVariables.instance.eventCardFiles[fileNumber];
        cardDescription.text = KeywordTooltip.instance.EditText(dataFile.textBox);
        GetInstructions(dataFile);
    }

    public virtual void ActivateThis(int logged)
    {
        foreach (Player player in Manager.instance.playersInOrder)
            DoFunction(() => ResolveEvent(player.playerPosition, logged), player.realTimePlayer);
    }

    [PunRPC]
    protected virtual void ResolveEvent(int playerPosition, int logged)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.DoFunction(() => player.ChangeButtonColor(false));
        player.AddToStack(() => player.RememberStep(player, StepType.UndoPoint, () => player.EndTurn()), true);

        stepCounter = -1;
        player.RememberStep(this, StepType.Revert, () => Advance(false, player, dataFile, logged));
    }
}

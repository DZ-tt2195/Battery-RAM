using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using System.Linq;

public class RobotSibyl : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData dataFile = GetFile();
        player.DoFunction(() => player.DrawEventCards(dataFile.miscAmount, logged), RpcTarget.MasterClient);
        player.RememberStep(this, StepType.UndoPoint, () => DiscardEvent(player, dataFile, 1, logged));
    }

    void DiscardEvent(Player player, CardData dataFile, int counter, int logged)
    {
        sideCounter = counter;
        string parathentical = (dataFile.cardAmount == 1) ? "" : $" ({counter}/{dataFile.cardAmount})";
        player.ChooseCardFromPopup(player.myEvents.OfType<Card>().ToList(), Vector3.zero, $"Discard an Event{parathentical}.", Discard);

        void Discard()
        {
            EventCard card = (EventCard)player.chosenCard;
            player.RememberStep(player, StepType.Share, () => player.DiscardEvent(false, card.pv.ViewID, logged));
            player.Pivot();
        }
    }
}

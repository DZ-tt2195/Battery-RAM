using Photon.Pun;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Explosion : EventCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    [PunRPC]
    protected override void ResolveEvent(int playerPosition, int logged)
    {
        base.ResolveEvent(playerPosition, logged);
        Player player = Manager.instance.playersInOrder[playerPosition];

        if (player.cardsInPlay.Count >= dataFile.miscAmount)
            player.RememberStep(this, StepType.UndoPoint, () => LoseAnotherBattery(player, 0, logged));
        player.PopStack();
    }

    protected void LoseAnotherBattery(Player player, int counter, int logged)
    {
        sideCounter = counter;
        List<Card> cardsToChoose = player.cardsInPlay.OfType<Card>().ToList();
        player.ChooseCardOnScreen(cardsToChoose, $"Discard 1 Card from your play area.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                player.RememberStep(this, StepType.Share, () =>
                    DiscardFromPlay(false, player.playerPosition, playerCard.pv.ViewID, logged));
            }
        }
    }

    [PunRPC]
    void DiscardFromPlay(bool undo, int playerPosition, int PV, int logged)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        PlayerCard card = PhotonView.Find(PV).GetComponent<PlayerCard>();

        if (undo)
        {
            player.cardsInPlay.Add(card);
        }
        else
        {
            player.cardsInPlay.Remove(card);
            Log.instance.AddText($"{this.name} discards {card.name} from play.", logged);
        }
        player.SortPlay();
    }
}
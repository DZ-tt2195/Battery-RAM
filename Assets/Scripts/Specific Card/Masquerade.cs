using UnityEngine;
using Photon.Pun;
using System.Linq;

public class Masquerade : EventCard
{
    int[] cardIDs = null;

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

        if (Manager.instance.AmMaster() && Manager.instance.playersInOrder.Count >= 2)
        {
            cardIDs = new int[Manager.instance.playersInOrder.Count];
            Manager.instance.AddStep(SelectCards, 1);
            Manager.instance.AddStep(DiscardCards, 2);
            Manager.instance.AddStep(ReceiveCards, 3);
        }

        player.DoFunction(() => player.DrawPlayerCards(1, logged), RpcTarget.MasterClient);
        player.PopStack();
        if (Manager.instance.playersInOrder.Count == 1)
            Log.instance.AddText("There aren't any other players.", 1);
    }

    void SelectCards()
    {
        foreach (Player player in Manager.instance.playersInOrder)
            DoFunction(() => PickOtherPlayersCardRPC(player.playerPosition), player.realTimePlayer);
    }

    [PunRPC]
    void PickOtherPlayersCardRPC(int playerPosition)
    {
        Player thisPlayer = Manager.instance.playersInOrder[playerPosition];
        Player prevPlayer = (playerPosition == 0) ? Manager.instance.playersInOrder[^1] : Manager.instance.playersInOrder[playerPosition - 1];
        thisPlayer.RememberStep(this, StepType.UndoPoint, () => PickOtherPlayersCard(thisPlayer, prevPlayer));
        thisPlayer.PopStack();
    }

    void PickOtherPlayersCard(Player thisPlayer, Player prevPlayer)
    {
        if (prevPlayer.cardsInHand.Count == 1)
            thisPlayer.AutoNewDecision();

        thisPlayer.ChooseCardFromPopup(prevPlayer.cardsInHand.OfType<Card>().ToList(), Vector3.zero, $"Take one of {prevPlayer.name}'s cards.", Resolution);

        void Resolution()
        {
            DoFunction(() => RememberChoice(thisPlayer.playerPosition, thisPlayer.chosenCard.pv.ViewID));
            thisPlayer.RememberStep(this, StepType.UndoPoint, () => thisPlayer.EndTurn());
        }
    }

    [PunRPC]
    void RememberChoice(int playerPosition, int cardID)
    {
        cardIDs[playerPosition] = cardID;
    }

    void DiscardCards()
    {
        for (int i = 0; i<Manager.instance.playersInOrder.Count; i++)
        {
            Player player = Manager.instance.playersInOrder[i];
            if (i == 0)
                DoFunction(() => DiscardChosenCard(i, cardIDs[Manager.instance.playersInOrder.Count-1]), player.realTimePlayer);
            else
                DoFunction(() => DiscardChosenCard(i, cardIDs[i - 1]), player.realTimePlayer);
        }
    }

    [PunRPC]
    void DiscardChosenCard(int playerPosition, int cardID)
    {
        PlayerCard card = PhotonView.Find(cardID).GetComponent<PlayerCard>();
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.DiscardPlayerCard(card, -1);

        player.RememberStep(this, StepType.UndoPoint, () => player.EndTurn());
        player.PopStack();
    }

    void ReceiveCards()
    {
        for (int i = 0; i < Manager.instance.playersInOrder.Count; i++)
        {
            Player player = Manager.instance.playersInOrder[i];
            DoFunction(() => ReceiveChosenCard(i, cardIDs[i]), player.realTimePlayer);
        }
    }

    [PunRPC]
    void ReceiveChosenCard(int playerPosition, int cardID)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.SendPlayerCardToAsker(cardID, 1);

        player.RememberStep(this, StepType.UndoPoint, () => player.EndTurn());
        player.PopStack();
    }
}

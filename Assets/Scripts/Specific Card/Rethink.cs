using Photon.Pun;
using UnityEngine;

public class Rethink : EventCard
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
        DiscardCard(player, GetFile(), logged);
    }

    protected override void PostDiscarding(Player player, bool success, CardData dataFile, int logged)
    {
        if (player.cardsInHand.Count == 0)
            player.DoFunction(() => player.DrawPlayerCards(1, logged), RpcTarget.MasterClient);
    }
}


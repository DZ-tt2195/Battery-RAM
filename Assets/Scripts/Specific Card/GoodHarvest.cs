using Photon.Pun;
using UnityEngine;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;

public class GoodHarvest : EventCard
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
        CardData dataFile = GetFile();

        EndMyTurn ability = null; ability = new(this, true, DrawCards);
        player.NewAbility(ability);
        player.Pivot();

        void DrawCards(int myLogged, object[] parameters)
        {
            List<NextStep> playSteps = player.SearchForSteps("AddToPlay");
            player.PreserveTextRPC($"{player.name} played {playSteps.Count} Card this turn ({this.name}).", myLogged);
            player.DrawPlayerCards(playSteps.Count, myLogged+1);
        }
    }
}

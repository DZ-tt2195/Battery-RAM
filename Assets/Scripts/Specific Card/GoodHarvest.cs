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

        PlayedCard ability = null; ability = new(this, true, AddCrown);
        player.NewAbility(ability);
        player.Pivot();

        void AddCrown(int myLogged, object[] parameters)
        {
            player.ResourceRPC(Resource.Crown, dataFile.crownAmount, logged);
        }
    }
}

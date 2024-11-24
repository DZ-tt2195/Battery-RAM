using Photon.Pun;
using UnityEngine;

public class Capital : EventCard
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

        EndMyTurn ability = null; ability = new(this, true, LoseMoney);
        player.NewAbility(ability);
        AddCoin(player, dataFile, logged);
        player.PopStack();

        void LoseMoney(int myLogged, object[] parameters)
        {
            player.ResourceRPC(Resource.Coin, dataFile.coinAmount * -2, logged, "Capital");
        }
    }
}

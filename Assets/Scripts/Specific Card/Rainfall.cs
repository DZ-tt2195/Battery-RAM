using UnityEngine;
using Photon.Pun;

public class Rainfall : EventCard
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

        int counter = dataFile.miscAmount;
        CanResolveCard ability = null; ability = new(this, true, CantResolve);
        player.NewAbility(ability);
        player.PopStack();

        bool CantResolve(int myLogged, object[] parameters)
        {
            counter--;
            PlayerCard card = (PlayerCard)parameters[0];
            player.PreserveTextRPC($"{player.name} can't resolve {card.name} ({this.name}).", logged);

            if (counter == 0)
                player.AbilityExpired(ability);

            return false;
        }
    }
}

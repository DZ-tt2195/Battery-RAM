using Photon.Pun;
using UnityEngine;

public class PowerOutage : EventCard
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

        CanAddBattery ability = null; ability = new(this, true, NoBattery);
        player.NewAbility(ability);
        player.Pivot();

        bool NoBattery(int myLogged, object[] parameters)
        {
            player.PreserveTextRPC($"{player.name} can't add Battery ({this.name}).", logged);
            return false;
        }
    }
}

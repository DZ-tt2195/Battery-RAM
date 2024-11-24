using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RobotShopkeeper : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        PlayCard(player, GetFile(), logged);
    }

    protected override void PostPlaying(Player player, PlayerCard cardToPlay, CardData dataFile, int logged)
    {
        if (cardToPlay != null)
            cardToPlay.BatteryRPC(player, -1 * cardToPlay.batteryHere, logged, this.name);
        player.Pivot();
    }
}

using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class RobotTaskmaster : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData dataFile = GetFile();
        if (player.SearchForSteps("AddToPlay").Count >= dataFile.miscAmount)
            AddBattery(player, dataFile, logged);
    }
}
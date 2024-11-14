using System.Collections.Generic;
using UnityEngine;

public class GluttonousRobot : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData file = GetFile();
        if (batteryHere >= file.miscAmount)
            AddCrown(player, file, logged);
        else
            LoseCrown(player, file, logged);
    }
}
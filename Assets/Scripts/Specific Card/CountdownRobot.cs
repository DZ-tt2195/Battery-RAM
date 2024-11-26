using UnityEngine;

public class CountdownRobot : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        if (batteryHere == 0)
            DiscardCard(player, GetFile(), logged);
    }
}
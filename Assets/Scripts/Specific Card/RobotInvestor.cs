using UnityEngine;

public class RobotInvestor : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        player.ResourceRPC(Resource.Coin, batteryHere, logged);
    }
}
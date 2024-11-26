using UnityEngine;

public class RobotTaxman : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData file = GetFile();
        DrawCard(player, file, logged);
        if (player.resourceDictionary[Resource.Coin] >= file.miscAmount)
            player.ResourceRPC(Resource.Crown, -1 * file.crownAmount, logged);
    }
}
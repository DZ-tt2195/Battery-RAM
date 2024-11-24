using UnityEngine;

public class BalanceRobot : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        int iteration = 2;
        CardData file = GetFile();
        foreach (PlayerCard card in player.cardsInPlay)
        {
            if (card.batteryHere != this.batteryHere)
            {
                iteration = 1;
                break;
            }
        }

        for (int i = 0; i<iteration; i++)
            player.ResourceRPC(Resource.Crown, file.crownAmount, logged);

        player.Pivot();
    }
}
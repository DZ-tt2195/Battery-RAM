using System.Linq;
using UnityEngine;

public class RobotPeddler : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        player.ResourceRPC(Resource.Coin, -1 * player.cardsInHand.Count, logged);
        DrawCard(player, GetFile(), logged);
        player.PopStack();
    }
}

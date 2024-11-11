using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RobotShopkeeper : PlayerCard
{
    List<PlayerCard> canPlay;

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        canPlay = player.cardsInHand.Where(card => card.CanPayCost(player)).ToList();

        if (canPlay.Count >= 1)
            player.RememberStep(this, StepType.None, () => ChoosePlay(player, logged));
        else
            player.PopStack();
    }

    void ChoosePlay(Player player, int logged)
    {
        player.ChooseCardOnScreen(canPlay.OfType<Card>().ToList(), $"Choose a card to play.", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard cardToPlay = (PlayerCard)player.chosenCard;
                player.PlayCard(cardToPlay, true, logged);
                cardToPlay.BatteryRPC(player, -1 * cardToPlay.batteryHere, logged);
            }
            else
            {
                player.PreserveTextRPC($"{player.name} doesn't play anything with {this.name}.", logged);
            }
            player.PopStack();
        }
    }

}

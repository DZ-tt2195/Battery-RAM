using UnityEngine;
using Photon.Pun;
using System.Linq;

public class AllNighter : EventCard
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

        player.RememberStep(this, StepType.UndoPoint, () => LoseOneBattery(player, logged));
        player.PopStack();
    }

    void LoseOneBattery(Player player, int logged)
    {
        if (player.TotalBattery() == 0)
        {
            player.AutoNewDecision();
            player.PopStack();
        }
        else
        {
            player.ChooseCardOnScreen(player.cardsInPlay.Where(card => card.batteryHere >= 1).OfType<Card>().ToList(), "Choose a card to resolve.", Resolve);

            void Resolve()
            {
                PlayerCard toResolve = (PlayerCard)player.chosenCard;
                player.PreserveTextRPC($"{player.name} resolves {toResolve.name}.", logged);
                toResolve.BatteryRPC(player, -1, logged);
                toResolve.ActivateThis(player, logged+1);
            }
        }
    }
}

using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class RobotChef : PlayerCard
{
    List<Card> withNoBatteries;

    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData dataFile = GetFile();
        withNoBatteries = player.cardsInPlay.Where(card => card.batteryHere == dataFile.miscAmount).OfType<Card>().ToList();

        if (!player.BoolFromAbilities(false, nameof(CanAddBattery), CanAddBattery.CheckParameters(), logged))
            player.RememberStep(this, StepType.UndoPoint, () => ChooseAddBattery(player, dataFile, 1, logged));
    }

    void ChooseAddBattery(Player player, CardData dataFile, int counter, int logged)
    {
        sideCounter = counter;
        string parathentical = $" ({counter}/{dataFile.batteryAmount})";

        if (withNoBatteries.Count == 0)
        {
            player.AutoNewDecision();
            player.PopStack();
        }
        else
        {
            player.ChooseCardOnScreen(withNoBatteries, $"Add a Battery to a Card with 0 Battery{parathentical}.", Next);
        }
        void Next()
        {
            if (player.chosenCard != null)
            {
                withNoBatteries.Remove(player.chosenCard);
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, 1, logged, this.name);

                if (counter != dataFile.batteryAmount)
                    player.RememberStep(this, StepType.UndoPoint, () => ChooseAddBattery(player, dataFile, sideCounter+1, logged));
            }
            else
            {
                player.PreserveTextRPC($"{this.name} can't add any more Battery.", logged);
            }
        }
    }
}
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class RobotPainter : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        CardData file = GetFile();
        player.RememberStep(this, StepType.UndoPoint, () => PainterChoice(player, logged));
    }

    void PainterChoice(Player player, int logged)
    {
        CardData dataFile = GetFile();
        List<string> choices = new() { $"+{dataFile.cardAmount} Card", $"+{dataFile.coinAmount} Coin", $"+{dataFile.batteryAmount} Battery" };
        player.ChooseButton(choices, Vector3.zero, $"Choose one for {this.name}.", Done);

        void Done()
        {
            switch (player.choice)
            {
                case 0:
                    DrawCard(player, dataFile, logged);
                    break;
                case 1:
                    AddCoin(player, dataFile, logged);
                    break;
                case 2:
                    AddBattery(player, dataFile, logged);
                    break;
            }
        }
    }
}
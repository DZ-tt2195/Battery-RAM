using Photon.Pun;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class Tournament : EventCard
{
    int[] amountRemoved = null;

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

        if (Manager.instance.AmMaster() && Manager.instance.playersInOrder.Count >= 2)
        {
            amountRemoved = new int[Manager.instance.playersInOrder.Count];
            Manager.instance.AddStep(HandOutResources, 1);
        }

        if (Manager.instance.playersInOrder.Count == 1)
            Log.instance.AddText("There aren't any other players.", 1);
        else
            player.RememberStep(this, StepType.UndoPoint, () => LoseAnotherBattery(player, 0, logged));

        player.PopStack();
    }

    protected void LoseAnotherBattery(Player player, int counter, int logged)
    {
        sideCounter = counter;
        List<Card> cardsToChoose = player.cardsInPlay.Where(card => card.batteryHere >= 1).OfType<Card>().ToList();
        if (cardsToChoose.Count == 0)
        {
            player.AutoNewDecision();
            DoFunction(() => RememberBattery(player.playerPosition, counter));
        }
        else
        {
            player.ChooseButton(new() { "Done" }, new(0, -250), "", null);
            player.ChooseCardOnScreen(cardsToChoose, $"Lose a Battery to {this.name}({counter} so far).", Next);
        }

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, -1, logged, this.name);
                player.RememberStep(this, StepType.UndoPoint, () => LoseAnotherBattery(player, counter+1, logged));
            }
            else
            {
                player.PreserveTextRPC($"{player.name} removed {counter} Battery.", logged);
                DoFunction(() => RememberBattery(player.playerPosition, counter));
            }
        }
    }

    [PunRPC]
    void RememberBattery(int playerPosition, int amount)
    {
        amountRemoved[playerPosition] = amount;
    }

    void HandOutResources()
    {
        int lowest = amountRemoved.Min();

        Log.instance.DoFunction(() => Log.instance.AddText($"", 0));
        Log.instance.DoFunction(() => Log.instance.AddText($"Least Battery removed: {lowest}", 1));

        for (int i = 0; i < Manager.instance.playersInOrder.Count; i++)
        {
            Player player = Manager.instance.playersInOrder[i];
            int playerChoice = amountRemoved[i];
            int amount = 0;

            if (playerChoice == lowest)
                amount-=GetFile().crownAmount;

            DoFunction(() => TournamentResourcesToAll(player.playerPosition, amount), player.realTimePlayer);
        }
    }

    [PunRPC]
    void TournamentResourcesToAll(int playerPosition, int crownChange)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.ResourceRPC(Resource.Crown, crownChange, 1);

        player.RememberStep(this, StepType.UndoPoint, () => player.EndTurn());
        player.PopStack();
    }
}

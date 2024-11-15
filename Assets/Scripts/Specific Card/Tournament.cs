using Photon.Pun;
using UnityEngine;
using System.Linq;

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

        if (Manager.instance.AmMaster())
        {
            amountRemoved = new int[Manager.instance.playersInOrder.Count];
            Manager.instance.AddStep(HandOutResources, 1);
        }
        player.RememberStep(this, (player.TotalBattery() == 0) ? StepType.None : StepType.UndoPoint, () => LoseAnotherBattery(player, 0, logged));
    }

    protected void LoseAnotherBattery(Player player, int counter, int logged)
    {
        sideCounter = counter;
        player.ChooseButton(new() { "Done" }, new(0, -250), "", null);
        player.ChooseCardOnScreen(player.cardsInPlay.Where(card => card.batteryHere >= 1).OfType<Card>().ToList(), $"Lose a Battery to {this.name}({counter} so far).", Next);

        void Next()
        {
            if (player.chosenCard != null)
            {
                PlayerCard playerCard = (PlayerCard)player.chosenCard;
                playerCard.BatteryRPC(player, -1, logged, this.name);
                player.RememberStep(this, (player.TotalBattery() == 0) ? StepType.None : StepType.UndoPoint, () => LoseAnotherBattery(player, counter+1, logged));
            }
            else
            {
                player.PreserveTextRPC($"{player.name} removed {counter} Battery.", logged);
                DoFunction(() => RememberBattery(player.playerPosition, counter));
                player.PopStack();
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
        int highest = amountRemoved.Max();
        int lowest = amountRemoved.Min();

        Log.instance.DoFunction(() => Log.instance.AddText($"", 0));
        Log.instance.DoFunction(() => Log.instance.AddText($"Most Battery removed: {highest}", 1));
        Log.instance.DoFunction(() => Log.instance.AddText($"Least Battery removed: {lowest}", 1));

        for (int i = 0; i < Manager.instance.playersInOrder.Count; i++)
        {
            Player player = Manager.instance.playersInOrder[i];
            int playerChoice = amountRemoved[i];
            int amount = 0;

            if (playerChoice == lowest)
                amount-=GetFile().crownAmount;
            if (playerChoice == highest)
                amount+=GetFile().crownAmount;

            DoFunction(() => TournamentResourcesToAll(player.playerPosition, amount), player.realTimePlayer);
        }
    }

    [PunRPC]
    void TournamentResourcesToAll(int playerPosition, int crownChange)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.ResourceRPC(Resource.Crown, crownChange, 1);
        player.EndTurn();
    }
}

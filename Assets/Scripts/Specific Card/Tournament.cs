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
        player.RememberStep(this, StepType.UndoPoint, () => Loop(player, logged));
    }

    void Loop(Player player, int logged)
    {
        player.ChooseSlider(0, player.resourceDictionary[Resource.Coin], "Pay any amount of Coin to Tournament.", Vector3.zero, Done);

        void Done()
        {
            int paidMoney = player.choice;
            player.ResourceRPC(Resource.Coin, -1 * paidMoney, logged);
            DoFunction(() => RememberMoney(player.playerPosition, paidMoney));
            player.PopStack();
        }
    }

    [PunRPC]
    void RememberMoney(int playerPosition, int amount)
    {
        amountRemoved[playerPosition] = amount;
    }

    void HandOutResources()
    {
        int highest = amountRemoved.Max();
        int lowest = amountRemoved.Min();

        Log.instance.DoFunction(() => Log.instance.AddText($"", 0));
        Log.instance.DoFunction(() => Log.instance.AddText($"Most Coin paid: {highest}", 1));
        Log.instance.DoFunction(() => Log.instance.AddText($"Least Coin paid: {lowest}", 1));

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

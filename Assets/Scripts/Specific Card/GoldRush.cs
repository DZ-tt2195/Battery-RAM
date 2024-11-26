using Photon.Pun;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GoldRush : EventCard
{
    int[] moneyGained = null;

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
            moneyGained = new int[Manager.instance.playersInOrder.Count];
            Manager.instance.AddStep(HandOutResources, 1);
        }

        if (Manager.instance.playersInOrder.Count >= 2)
        {
            EndMyTurn ability = null; ability = new(this, true, LoseMoney);
            player.NewAbility(ability);
        }
        else
        {
            Log.instance.AddText("There aren't any other players.", 1);
        }

        player.PopStack();

        void LoseMoney(int myLogged, object[] parameters)
        {
            List<NextStep> listOfSteps = player.SearchForSteps("ChangeResource");
            int thisMoney = 0;

            foreach (NextStep step in listOfSteps)
            {
                (string instruction, object[] stepParameters) = step.source.TranslateFunction(step.action);
                if ((int)stepParameters[1] == (int)Resource.Coin)
                {
                    if ((int)stepParameters[2] > 0)
                        thisMoney += (int)stepParameters[2];
                }    
            }
            player.PreserveTextRPC($"{player.name} gained {thisMoney} Coin this turn.", logged);
            DoFunction(() => RememberMoney(player.playerPosition, thisMoney));
        }
    }

    [PunRPC]
    void RememberMoney(int playerPosition, int amount)
    {
        moneyGained[playerPosition] = amount;
    }

    void HandOutResources()
    {
        int highest = moneyGained.Max();
        int lowest = moneyGained.Min();

        Log.instance.DoFunction(() => Log.instance.AddText($"", 0));
        Log.instance.DoFunction(() => Log.instance.AddText($"Most Coin gained: {highest}", 1));
        Log.instance.DoFunction(() => Log.instance.AddText($"Least Coin gained: {lowest}", 1));

        for (int i = 0; i < Manager.instance.playersInOrder.Count; i++)
        {
            Player player = Manager.instance.playersInOrder[i];
            int playerChoice = moneyGained[i];
            int amount = 0;

            if (playerChoice == highest)
                amount += GetFile().crownAmount;
            if (playerChoice == lowest)
                amount -= GetFile().crownAmount;

            DoFunction(() => GoldRushResourcesToAll(player.playerPosition, amount), player.realTimePlayer);
        }
    }

    [PunRPC]
    void GoldRushResourcesToAll(int playerPosition, int crownChange)
    {
        Player player = Manager.instance.playersInOrder[playerPosition];
        player.ResourceRPC(Resource.Crown, crownChange, 1);

        player.RememberStep(this, StepType.UndoPoint, () => player.EndTurn());
        player.PopStack();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    [SerializeField] int numCards;
    [SerializeField] int numCoins;
    [SerializeField] int defaultCost;
    [SerializeField] int defaultCrowns;
    int gameLength = 12;
    int playedCards;

    void Start()
    {
        RunLoop();
    }

    void RunLoop()
    {
        for (int i = 0; i<gameLength; i++)
        {
            if (numCards == 0)
            {
                numCards += 2;
            }
            else if (numCoins < defaultCost)
            {
                numCoins += 4;
            }
            else
            {
                numCards -= 1;
                numCoins -= defaultCost;
                playedCards++;
            }
        }

        Debug.Log($"Cards played: {playedCards}" +
            $"\nCrowns: {playedCards * defaultCrowns}" +
            $"\nLeftover cards: {numCards}" +
            $"\nLeftover coins: {numCoins}");
    }
}

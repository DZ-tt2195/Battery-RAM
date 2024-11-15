using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    [SerializeField] int numCards;
    [SerializeField] int numCoins;
    [SerializeField] int gameLength;
    int playedCards;

    void Start()
    {
        RunLoop();
    }

    void RunLoop()
    {
        //default card: $4, 4 Crowns, no abilities/batteries
        for (int i = 0; i<gameLength; i++)
        {
            if (numCards == 0)
            {
                numCards += 2;
            }
            else if (numCoins < 4)
            {
                numCoins += 4;
            }
            else
            {
                numCards -= 1;
                numCoins -= 4;
                playedCards++;
            }
        }

        Debug.Log($"Cards played: {playedCards}" +
            $"\nCrowns: {playedCards * 4}" +
            $"\nLeftover cards: {numCards}" +
            $"\nLeftover coins: {numCoins}");
    }
}

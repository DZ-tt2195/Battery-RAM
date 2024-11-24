using System;
using UnityEngine;

[Serializable]
public class TriggeredAbility
{
    public PhotonCompatible source { get; protected set; }
    protected string comparison;
    protected Func<string, object[], bool> CanBeTriggered;
    public bool justThisTurn;

    protected Action<int, object[]> WhenTriggered;
    protected Func<int, object[], int> GetNumber;
    protected Func<int, object[], bool> GetBool;

    protected TriggeredAbility(PhotonCompatible source, Action<int, object[]> voidAbility, bool justThisTurn, Func<string, object[], bool> condition = null)
    {
        this.source = source;
        this.justThisTurn = justThisTurn;
        CanBeTriggered = condition;
        WhenTriggered = voidAbility;
    }

    protected TriggeredAbility(PhotonCompatible source, Func<int, object[], int> numberAbility, bool justThisTurn, Func<string, object[], bool> condition = null)
    {
        this.source = source;
        this.justThisTurn = justThisTurn;
        CanBeTriggered = condition;
        GetNumber = numberAbility;
    }

    protected TriggeredAbility(PhotonCompatible source, Func<int, object[], bool> boolAbility, bool justThisTurn, Func<string, object[], bool> condition = null)
    {
        this.source = source;
        this.justThisTurn = justThisTurn;
        CanBeTriggered = condition;
        GetBool = boolAbility;
    }

    public bool CheckAbility(string condition, object[] parameters = null)
    {
        try
        {
            if (CanBeTriggered != null)
                return CanBeTriggered(condition, parameters) && comparison == condition;
            else
                return comparison == condition;
        }
        catch
        {
            return false;
        }
    }

    public void ResolveAbility(int logged, object[] parameters = null)
    {
        WhenTriggered(logged, parameters);
    }

    public bool BoolAbility(int logged, object[] parameters = null)
    {
        return GetBool(logged, parameters);
    }

    public int NumberAbility(int logged, object[] parameters = null)
    {
        return GetNumber(logged, parameters);
    }
}

public class EndMyTurn : TriggeredAbility
{
    public EndMyTurn(PhotonCompatible source, bool justThisTurn, Action<int, object[]> ability, Func<string, object[], bool> condition = null) : base(source, ability, justThisTurn, condition)
    {
        comparison = nameof(EndMyTurn);
    }

    public static object[] CheckParameters()
    {
        return new object[0];
    }
}

public class ChangeCoinCost : TriggeredAbility
{
    public ChangeCoinCost(PhotonCompatible source, bool justThisTurn, Func<int, object[], int> numberAbility, Func<string, object[], bool> condition = null) : base(source, numberAbility, justThisTurn, condition)
    {
        comparison = nameof(ChangeCoinCost);
    }

    public static object[] CheckParameters(PlayerCard card)
    {
        return new object[1] { card };
    }

    public static PlayerCard ConvertParameters(object[] parameters)
    {
        return (PlayerCard)parameters[0];
    }
}

public class CanAddBattery : TriggeredAbility
{
    public CanAddBattery(PhotonCompatible source, bool justThisTurn, Func<int, object[], bool> boolAbility, Func<string, object[], bool> condition = null) : base(source, boolAbility, justThisTurn, condition)
    {
        comparison = nameof(CanAddBattery);
    }

    public static object[] CheckParameters()
    {
        return new object[0];
    }
}

public class CanResolveCard : TriggeredAbility
{
    public CanResolveCard(PhotonCompatible source, bool justThisTurn, Func<int, object[], bool> boolAbility, Func<string, object[], bool> condition = null) : base(source, boolAbility, justThisTurn, condition)
    {
        comparison = nameof(CanResolveCard);
    }

    public static object[] CheckParameters(PlayerCard card)
    {
        return new object[1] {card};
    }
}
using System;
using UnityEngine;

public class TriggeredAbility
{
    public PhotonCompatible source { get; protected set; }
    protected string comparison;
    protected Func<string, object[], bool> CanBeTriggered;

    protected Action<int, object[]> WhenTriggered;
    protected Func<int, object[], int> GetNumber;
    protected Func<int, object[], bool> GetBool;

    protected TriggeredAbility(PhotonCompatible source, Action<int, object[]> voidAbility, Func<string, object[], bool> condition = null)
    {
        this.source = source;
        CanBeTriggered = condition;
        WhenTriggered = voidAbility;
    }

    protected TriggeredAbility(PhotonCompatible source, Func<int, object[], int> numberAbility, Func<string, object[], bool> condition = null)
    {
        this.source = source;
        CanBeTriggered = condition;
        GetNumber = numberAbility;
    }

    protected TriggeredAbility(PhotonCompatible source, Func<int, object[], bool> boolAbility, Func<string, object[], bool> condition = null)
    {
        this.source = source;
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
    public EndMyTurn(PhotonCompatible source, Action<int, object[]> ability, Func<string, object[], bool> condition = null) : base(source, ability, condition)
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
    public ChangeCoinCost(PhotonCompatible source, Func<int, object[], int> numberAbility, Func<string, object[], bool> condition = null) : base(source, numberAbility, condition)
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
    public CanAddBattery(PhotonCompatible source, Func<int, object[], bool> boolAbility, Func<string, object[], bool> condition = null) : base(source, boolAbility, condition)
    {
        comparison = nameof(CanAddBattery);
    }

    public static object[] CheckParameters()
    {
        return new object[0];
    }
}
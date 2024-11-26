using UnityEngine;

public class RobotAristocrat : PlayerCard
{
    protected override void Awake()
    {
        base.Awake();
        this.bottomType = this.GetType();
    }

    public override void ActivateThis(Player player, int logged)
    {
        PlayCard(player, GetFile(), logged);
    }

    protected override void PostPlaying(Player player, PlayerCard cardToPlay, CardData dataFile, int logged)
    {
        if (cardToPlay != null)
        {
            RobotData theirData = cardToPlay.GetFile() as RobotData;
            if (theirData.startingBattery >= this.GetFile().miscAmount)
                cardToPlay.BatteryRPC(player, dataFile.batteryAmount, logged, this.name);
        }
    }
}

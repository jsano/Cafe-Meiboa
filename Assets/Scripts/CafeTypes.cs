public enum DayPhase
{
    Shop,
    Shift,
    CloseOfDay,
    Summary
}

public enum DrinkType
{
    Coffee,
    Latte,
    SyrupCoffee
}

public enum PastryType
{
    None = 0,
    Plain = 1,
    Chocolate = 2,
    Cheese = 3
}

public enum CustomerKind
{
    Regular,
    Milk,
    Sweet,
    Thief
}

public enum UpgradeType
{
    None,
    FasterBrew,
    LongerPatience,
    CheaperRent
}

public enum PrepType
{
    None,
    Espresso,
    SteamedMilk
}

public enum BrewStep
{
    Beans,
    Brew,
    Milk,
    Syrup
}

public class TrayCup
{
    public bool Occupied;
    public bool Hot;
    public bool Straw;
    public bool Ice;
    public int Water;
    public int Syrup;
    public bool Coffee;
    public bool Shot;

    public void Clear()
    {
        Occupied = false;
        Hot = false;
        Straw = false;
        Ice = false;
        Water = 0;
        Syrup = 0;
        Coffee = false;
        Shot = false;
    }

    public void CopyFrom(TrayCup other)
    {
        Occupied = other.Occupied;
        Hot = other.Hot;
        Straw = other.Straw;
        Ice = other.Ice;
        Water = other.Water;
        Syrup = other.Syrup;
        Coffee = other.Coffee;
        Shot = other.Shot;
    }
}

public enum TipGrade
{
    None,
    Okay,
    Perfect
}

public static class CafeBalance
{
    public const int StartingCash = 8;
    public const int CoffeePay = 10;
    public const int LattePay = 14;
    public const int SyrupPay = 16;
    public const int PastryMatch = 6;
    public const int PastryMismatch = 2;
    public const int TipPerfect = 5;
    public const int TipOkay = 2;
    public const int BeanPackCost = 8;
    public const int BeanPackSize = 4;
    public const int MilkPackCost = 8;
    public const int MilkPackSize = 4;
    public const int SyrupCost = 15;
    public const int MechanicCost = 20;
    public const int UpgradeCost = 25;
    public const int RentCost = 30;
    public const int CheapRentCost = 20;
    public const int QuestBonus = 15;
    public const int QuestGoal = 5;
    public const int ThiefSteal = 12;

    public const float PatienceSeconds = 16f;
    public const float LongPatienceSeconds = 23f;
    public const float ThiefWindow = 4f;
    public const float RepairHoldSeconds = 1.6f;
    public const float MechanicRepairSeconds = 0.8f;
    public const float MarkerSpeed = 0.62f;
    public const float FastMarkerSpeed = 0.95f;
    public const float GreenHalf = 0.11f;
    public const float FastGreenHalf = 0.16f;
    public const float GrindSeconds = 4f;
    public const float EspressoSeconds = 4f;
    public const float CoffeeSeconds = 4f;
    public const int CoffeeBatch = 4;
    public const float ToastSeconds = 10f;
}

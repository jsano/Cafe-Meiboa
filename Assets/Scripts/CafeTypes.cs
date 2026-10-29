using System;
using System.Collections.Generic;

public enum DayPhase
{
    Shop,
    Shift,
    CloseOfDay,
    Summary
}

public enum DrinkType
{
    HotChoco,
    IcedCoffee,
    IcedMocha,
    Americano,
    Latte,
    Mocha,
    IcedAmericano,
    IcedLatte,
    BlackCoffee,
    IcedVanillaLatte,
    IcedStrawberryLatte,
    StrawberryLatte,
    VanillaLatte,
    AlmondLatte,
    IcedCoconutMocha,
    CoconutLatte,
    IcedCoconutLatte,
    IcedAlmondLatte,
    WhiteTea,
    BlackTea,
    IcedMatcha,
    IcedCaramelLatte,
    WhiteTeaLatte,
    BlackTeaLatte,
    CaramelLatte,
    CoconutMocha,
    WhiteChocolateLatte,
    IcedCoconutMatcha,
    StrawberryMatcha,
    IcedMatchaLatte,
    ChocolateMatchaLatte,
    CaramelMatcha,
    IcedWhiteChocolateLatte,
    IcedWhiteTeaLatte,
    IcedBlackTeaLatte,
    VanillaMatchaLatte,
    IcedCaramelMatcha,
    AlmondMatchaLatte,
    IcedChocolateMatcha
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

public enum BreakStation
{
    None,
    CoffeePuck,
    Coffee,
    Toaster,
    Ice,
    Espresso
}

[Flags]
public enum CupBits
{
    None = 0,
    Hot = 1 << 0,
    Cold = 1 << 1,
    Shot = 1 << 2,
    Coffee = 1 << 3,
    Ice = 1 << 4,
    Straw = 1 << 5,
    HotWater = 1 << 6,
    ColdWater = 1 << 7,
    Milk = 1 << 8,
    Steamed = 1 << 9
}

[Flags]
public enum SyrupBits
{
    None = 0,
    Chocolate = 1 << 0,
    Vanilla = 1 << 1,
    Strawberry = 1 << 2,
    Caramel = 1 << 3,
    WhiteChocolate = 1 << 4,
    Coconut = 1 << 5,
    Almond = 1 << 6
}

[Flags]
public enum TeaBits
{
    None = 0,
    Black = 1 << 0,
    Green = 1 << 1,
    Matcha = 1 << 2,
    White = 1 << 3
}

public class TrayCup
{
    public bool Occupied;
    public bool Hot;
    public bool Straw;
    public bool Ice;
    public int Water;
    public SyrupBits Syrup;
    public TeaBits Tea;
    public bool Milk;
    public bool SteamedMilk;
    public bool Coffee;
    public bool Shot;

    public void Clear()
    {
        Occupied = false;
        Hot = false;
        Straw = false;
        Ice = false;
        Water = 0;
        Syrup = SyrupBits.None;
        Tea = TeaBits.None;
        Milk = false;
        SteamedMilk = false;
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
        Tea = other.Tea;
        Milk = other.Milk;
        SteamedMilk = other.SteamedMilk;
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

public struct DrinkRecipe
{
    public string Name;
    public string Steps;
    public CupBits Parts;
    public SyrupBits Syrups;
    public TeaBits Teas;

    public bool Matches(TrayCup cup)
    {
        if (cup == null || !cup.Occupied)
            return false;

        CupBits have = cup.Hot ? CupBits.Hot : CupBits.Cold;
        if (cup.Shot)
            have |= CupBits.Shot;
        if (cup.Coffee)
            have |= CupBits.Coffee;
        if (cup.Ice)
            have |= CupBits.Ice;
        if (cup.Straw)
            have |= CupBits.Straw;
        if (cup.Water == 1)
            have |= CupBits.HotWater;
        if (cup.Water == 2)
            have |= CupBits.ColdWater;
        if (cup.Milk)
            have |= CupBits.Milk;
        if (cup.SteamedMilk)
            have |= CupBits.Steamed;
        return have == Parts && cup.Syrup == Syrups && cup.Tea == Teas;
    }

    public CustomerKind Kind
    {
        get
        {
            if (Syrups != SyrupBits.None)
                return CustomerKind.Sweet;
            if ((Parts & (CupBits.Milk | CupBits.Steamed)) != 0)
                return CustomerKind.Milk;
            return CustomerKind.Regular;
        }
    }

    public int Pay
    {
        get
        {
            bool dairy = (Parts & (CupBits.Milk | CupBits.Steamed)) != 0;
            if (Syrups != SyrupBits.None || (Teas != TeaBits.None && dairy))
                return CafeBalance.SyrupPay;
            if (dairy)
                return CafeBalance.LattePay;
            return CafeBalance.CoffeePay;
        }
    }

    public int StepCount
    {
        get
        {
            int count = 1;
            count += BitCount((int)(Parts & ~(CupBits.Hot | CupBits.Cold)));
            count += BitCount((int)Syrups);
            count += BitCount((int)Teas);
            return count;
        }
    }

    static int BitCount(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }
}

public static class DrinkRecipes
{
    public static DrinkRecipe Get(DrinkType drink)
    {
        return table[(int)drink];
    }

    public static bool Matches(TrayCup cup, DrinkType drink)
    {
        return Get(drink).Matches(cup);
    }

    public static DrinkType[] AtMostSteps(int steps)
    {
        if (shortMenu != null && shortMenuSteps == steps)
            return shortMenu;

        DrinkType[] menu = (DrinkType[])Enum.GetValues(typeof(DrinkType));
        var fits = new List<DrinkType>();
        for (int i = 0; i < menu.Length; i++)
        {
            if (Get(menu[i]).StepCount <= steps)
                fits.Add(menu[i]);
        }

        shortMenuSteps = steps;
        shortMenu = fits.ToArray();
        return shortMenu;
    }

    static DrinkType[] shortMenu;
    static int shortMenuSteps = -1;

    static DrinkRecipe Make(string name, CupBits parts, SyrupBits syrups = SyrupBits.None, TeaBits teas = TeaBits.None)
    {
        DrinkRecipe recipe = new DrinkRecipe();
        recipe.Name = name;
        recipe.Parts = parts;
        recipe.Syrups = syrups;
        recipe.Teas = teas;
        recipe.Steps = Describe(parts, syrups, teas);
        return recipe;
    }

    static string Describe(CupBits parts, SyrupBits syrups, TeaBits teas)
    {
        string text = (parts & CupBits.Hot) != 0 ? "Hot cup" : "Cold cup";
        text = Add(text, (int)parts, (int)CupBits.Shot, "Espresso");
        text = Add(text, (int)parts, (int)CupBits.Coffee, "Coffee");
        text = Add(text, (int)parts, (int)CupBits.Milk, "Milk");
        text = Add(text, (int)parts, (int)CupBits.Steamed, "Steamed milk");
        text = Add(text, (int)syrups, (int)SyrupBits.Chocolate, "Chocolate");
        text = Add(text, (int)syrups, (int)SyrupBits.Vanilla, "Vanilla");
        text = Add(text, (int)syrups, (int)SyrupBits.Strawberry, "Strawberry");
        text = Add(text, (int)syrups, (int)SyrupBits.Caramel, "Caramel");
        text = Add(text, (int)syrups, (int)SyrupBits.WhiteChocolate, "White chocolate");
        text = Add(text, (int)syrups, (int)SyrupBits.Coconut, "Coconut");
        text = Add(text, (int)syrups, (int)SyrupBits.Almond, "Almond");
        text = Add(text, (int)teas, (int)TeaBits.Black, "Black tea");
        text = Add(text, (int)teas, (int)TeaBits.Green, "Green tea");
        text = Add(text, (int)teas, (int)TeaBits.Matcha, "Matcha");
        text = Add(text, (int)teas, (int)TeaBits.White, "White tea");
        text = Add(text, (int)parts, (int)CupBits.HotWater, "Hot water");
        text = Add(text, (int)parts, (int)CupBits.ColdWater, "Cold water");
        text = Add(text, (int)parts, (int)CupBits.Ice, "Ice");
        text = Add(text, (int)parts, (int)CupBits.Straw, "Straw");
        return text;
    }

    static string Add(string text, int bits, int flag, string label)
    {
        if ((bits & flag) == 0)
            return text;
        return text + "\n" + label;
    }

    static readonly DrinkRecipe[] table =
    {
        Make("Hot choco", CupBits.Hot | CupBits.Steamed, SyrupBits.Chocolate),
        Make("Iced coffee", CupBits.Cold | CupBits.Ice | CupBits.Coffee | CupBits.Straw),
        Make("Iced mocha", CupBits.Cold | CupBits.Shot | CupBits.Milk | CupBits.Ice | CupBits.Straw, SyrupBits.Chocolate),
        Make("Americano", CupBits.Hot | CupBits.Shot | CupBits.HotWater),
        Make("Latte", CupBits.Hot | CupBits.Shot | CupBits.Steamed),
        Make("Mocha", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.Chocolate),
        Make("Iced americano", CupBits.Cold | CupBits.Shot | CupBits.ColdWater | CupBits.Straw),
        Make("Iced latte", CupBits.Cold | CupBits.Shot | CupBits.Milk | CupBits.Ice | CupBits.Straw),
        Make("Black coffee", CupBits.Hot | CupBits.Coffee),
        Make("Iced vanilla latte", CupBits.Cold | CupBits.Shot | CupBits.Milk | CupBits.Ice | CupBits.Straw, SyrupBits.Vanilla),
        Make("Iced strawberry latte", CupBits.Cold | CupBits.Shot | CupBits.Milk | CupBits.Ice | CupBits.Straw, SyrupBits.Strawberry),
        Make("Strawberry latte", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.Strawberry),
        Make("Vanilla latte", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.Vanilla),
        Make("Almond latte", CupBits.Hot | CupBits.Steamed, SyrupBits.Almond),
        Make("Iced coconut mocha", CupBits.Cold | CupBits.Ice | CupBits.Shot | CupBits.Steamed | CupBits.Straw, SyrupBits.Chocolate | SyrupBits.Coconut),
        Make("Coconut latte", CupBits.Hot | CupBits.Steamed, SyrupBits.Coconut),
        Make("Iced coconut latte", CupBits.Cold | CupBits.Ice | CupBits.Steamed | CupBits.Straw, SyrupBits.Coconut),
        Make("Iced almond latte", CupBits.Cold | CupBits.Ice | CupBits.Steamed | CupBits.Straw, SyrupBits.Almond),
        Make("White tea", CupBits.Hot | CupBits.HotWater, teas: TeaBits.White),
        Make("Black tea", CupBits.Hot | CupBits.HotWater, teas: TeaBits.Black),
        Make("Iced matcha", CupBits.Cold | CupBits.Ice | CupBits.ColdWater | CupBits.Straw, teas: TeaBits.Matcha),
        Make("Iced caramel latte", CupBits.Cold | CupBits.Shot | CupBits.Milk | CupBits.Ice | CupBits.Straw, SyrupBits.Caramel),
        Make("White tea latte", CupBits.Hot | CupBits.Steamed, teas: TeaBits.White),
        Make("Black tea latte", CupBits.Hot | CupBits.Steamed, teas: TeaBits.Black),
        Make("Caramel latte", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.Caramel),
        Make("Coconut mocha", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.Coconut | SyrupBits.Chocolate),
        Make("White chocolate latte", CupBits.Hot | CupBits.Shot | CupBits.Steamed, SyrupBits.WhiteChocolate),
        Make("Iced coconut matcha", CupBits.Cold | CupBits.Ice | CupBits.Steamed | CupBits.Straw, SyrupBits.Coconut, TeaBits.Matcha),
        Make("Strawberry matcha", CupBits.Hot | CupBits.Steamed, SyrupBits.Strawberry, TeaBits.Matcha),
        Make("Iced matcha latte", CupBits.Cold | CupBits.Milk | CupBits.Ice | CupBits.Straw, teas: TeaBits.Matcha),
        Make("Chocolate matcha latte", CupBits.Hot | CupBits.Steamed, SyrupBits.Chocolate, TeaBits.Matcha),
        Make("Caramel matcha", CupBits.Hot | CupBits.Steamed, SyrupBits.Caramel, TeaBits.Matcha),
        Make("Iced white chocolate latte", CupBits.Cold | CupBits.Ice | CupBits.Shot | CupBits.Steamed | CupBits.Straw, SyrupBits.WhiteChocolate),
        Make("Iced white tea latte", CupBits.Cold | CupBits.Ice | CupBits.Milk | CupBits.Straw, teas: TeaBits.White),
        Make("Iced black tea latte", CupBits.Cold | CupBits.Ice | CupBits.Milk | CupBits.Straw, teas: TeaBits.Black),
        Make("Vanilla matcha latte", CupBits.Hot | CupBits.Steamed, SyrupBits.Vanilla, TeaBits.Matcha),
        Make("Iced caramel matcha", CupBits.Cold | CupBits.Milk | CupBits.Ice | CupBits.Straw, SyrupBits.Caramel, TeaBits.Matcha),
        Make("Almond matcha latte", CupBits.Hot | CupBits.Steamed, SyrupBits.Almond, TeaBits.Matcha),
        Make("Iced chocolate matcha", CupBits.Cold | CupBits.Ice | CupBits.Milk | CupBits.Straw, SyrupBits.Chocolate, TeaBits.Matcha)
    };
}

public static class CafeBalance
{
    public const int StartingCash = 16;
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
    public const int UpgradeBaseCost = 25;
    public const int UpgradeCostStep = 15;
    public const int RentCost = 30;
    public const int RentStep = 5;
    public const int RentMin = 10;
    public const int RentDayStep = 5;
    public const int QuestBonus = 15;
    public const int QuestGoal = 5;
    public const int ThiefSteal = 12;

    public const float PatienceSeconds = 23f;
    public const float PatienceStep = 3f;
    public const float PatienceMax = 35f;
    public const float PatienceDayStep = 2f;
    public const float ThiefWindow = 5f;
    public const float ThiefHoldSeconds = 3f;
    public const float DogWindow = 8f;
    public const float DogDelayMin = 6f;
    public const float DogDelayMax = 32f;
    public const float RepairHoldSeconds = 1.6f;
    public const float MarkerSpeed = 0.62f;
    public const float GreenHalf = 0.11f;
    public const float MachineTimeStep = 0.10f;
    public const float MachineTimeMin = 0.60f;
    public const float MachineDayStep = 0.5f;
    public const int BaseCustomers = 4;
    public const int CustomerStep = 2;
    public const int EndlessHealth = 5;
    public const float GrindSeconds = 4f;
    public const float EspressoSeconds = 4f;
    public const float CoffeeSeconds = 4f;
    public const int CoffeeBatch = 4;
    public const float IceSeconds = 8f;
    public const int IceBatch = 4;
    public const float ToastSeconds = 10f;
}

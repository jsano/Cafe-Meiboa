using System;
using System.Collections.Generic;
using UnityEngine;

public class Customer
{
    public bool Occupied;
    public bool IsThief;
    public CustomerKind Kind;
    public DrinkType Drink;
    public PastryType WantedPastry;
    public bool Accepted;
    public float Patience;
    public float PatienceMax;
    public bool DrinkReady;
    public float ThiefTime;
    public float ThiefHold;

    public void Clear()
    {
        Occupied = false;
        IsThief = false;
        Kind = CustomerKind.Regular;
        Drink = DrinkType.BlackCoffee;
        WantedPastry = PastryType.None;
        Accepted = false;
        Patience = 0f;
        PatienceMax = 0f;
        DrinkReady = false;
        ThiefTime = 0f;
        ThiefHold = 0f;
    }
}

public class QueuedOrder
{
    public CustomerKind Kind;
    public DrinkType Drink;
    public PastryType Pastry;
}

public class CafeSession
{
    public DayPhase Phase = DayPhase.Shop;
    public int Day = 1;
    public int Cash = CafeBalance.StartingCash;
    public int Beans;
    public int Milk;
    public bool SyrupUnlocked;
    public UpgradeType Upgrade = UpgradeType.None;
    public PrepType Prep = PrepType.None;
    public bool DogPresent;
    public float DogTime;
    public bool DogVisited;
    public bool DogPaidThisShift;
    public bool RentKept = true;

    public BreakStation BrokenTarget;
    public bool MachineBroken;

    public string BrokenName => StationName(BrokenTarget);
    public float Repair01;
    public bool BrewTiming;
    public float Marker01;
    public int Focus = -1;
    public bool BrewLocked;
    public bool FocusReady;

    public readonly Customer[] Slots = { new Customer(), new Customer() };
    public string Status = "";

    public int ShiftEarned;
    public int ShiftTips;
    public int ShiftWalkouts;
    public int ThiefLostThisShift;
    public bool ThiefAppeared;
    public bool ThiefCaught;
    public int RunTips;
    public int RunWalkouts;
    public int RunEarned;

    public bool QuestComplete;
    public bool QuestRewarded;
    public int CleanServes;

    public readonly int[] PastryChoice = new int[3];
    public readonly int[] PastryStock = new int[4];

    readonly Queue<QueuedOrder> queue = new Queue<QueuedOrder>();
    readonly List<float> timingScores = new List<float>();

    int markerDir = 1;
    int customersSpawned;
    int stationUses;
    bool dayBreakUsed;
    bool thiefInserted;
    bool dogResolved = true;
    float dogDelay;
    int heldSlot = -1;
    bool repairHeld;

    public int RentDue => Upgrade == UpgradeType.CheaperRent ? CafeBalance.CheapRentCost : CafeBalance.RentCost;

    public int DrinksToday => Day == 1 ? 4 : Day == 2 ? 6 : 8;

    public float GreenHalfWidth => Upgrade == UpgradeType.FasterBrew ? CafeBalance.FastGreenHalf : CafeBalance.GreenHalf;

    public bool CanOpen => true;

    public int EspressoShots;
    public bool GrinderBusy;
    public float GrinderTimer;
    public bool PuckReady;
    public bool EspressoBusy;
    public float EspressoTimer;
    public bool ToasterBusy;
    public float ToasterTimer;
    public PastryType ToasterReady;
    public PastryType Plated;
    public PastryType ToasterHeld => ToasterBusy ? toasting : ToasterReady;
    public bool CoffeeBusy;
    public float CoffeeTimer;
    public int CoffeeUses;
    public readonly TrayCup WaterCup = new TrayCup();
    public int SteamedServings;
    public int ActiveTray = -1;
    public readonly TrayCup[] Trays = { new TrayCup(), new TrayCup(), new TrayCup() };

    bool brewScored;
    bool waterCupWasActive;
    PastryType toasting;

    public int PastryCount
    {
        get
        {
            int total = 0;
            for (int i = 1; i < PastryStock.Length; i++)
                total += PastryStock[i];
            if (Plated != PastryType.None)
                total++;
            if (ToasterReady != PastryType.None)
                total++;
            return total;
        }
    }

    public void Begin()
    {
        Day = 1;
        Cash = CafeBalance.StartingCash;
        Beans = 0;
        Milk = 0;
        SyrupUnlocked = false;
        Upgrade = UpgradeType.None;
        Prep = PrepType.None;
        DogPresent = false;
        DogTime = 0f;
        DogVisited = false;
        DogPaidThisShift = false;
        RentKept = true;
        dogResolved = true;
        dogDelay = 0f;
        heldSlot = -1;
        MachineBroken = false;
        Repair01 = 0f;
        BrewTiming = false;
        Marker01 = 0f;
        Focus = -1;
        BrewLocked = false;
        FocusReady = false;
        ShiftEarned = 0;
        ShiftTips = 0;
        ShiftWalkouts = 0;
        ThiefLostThisShift = 0;
        ThiefAppeared = false;
        ThiefCaught = false;
        RunTips = 0;
        RunWalkouts = 0;
        RunEarned = 0;
        QuestComplete = false;
        QuestRewarded = false;
        CleanServes = 0;
        BrokenTarget = BreakStation.None;
        stationUses = 0;
        customersSpawned = 0;
        dayBreakUsed = false;
        thiefInserted = false;
        repairHeld = false;
        markerDir = 1;
        queue.Clear();
        timingScores.Clear();
        Array.Clear(PastryChoice, 0, PastryChoice.Length);
        Array.Clear(PastryStock, 0, PastryStock.Length);
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].Clear();

        EnterShop(rewardIfReady: false);
        ClearStation();
        Status = "Night before day 1. Buy beans, then open when you want.";
    }

    public void Tick(float dt)
    {
        if (Phase != DayPhase.Shift)
            return;

        TickRepair(dt);
        TickMachines(dt);
        TickTiming(dt);
        TickCustomers(dt);
        TickDog(dt);

        if (ShiftWorkDone() && dogResolved)
            EndShift();
    }

    public void SetRepairHeld(bool held) => repairHeld = held;

    public void SetSlotHeld(int index) => heldSlot = index;

    public void BuyBeans()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (Cash < CafeBalance.BeanPackCost)
        {
            Status = "Not enough cash for beans.";
            return;
        }

        Cash -= CafeBalance.BeanPackCost;
        Beans += CafeBalance.BeanPackSize;
        Status = "Bought 4 beans.";
    }

    public void BuyMilk()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (Cash < CafeBalance.MilkPackCost)
        {
            Status = "Not enough cash for milk.";
            return;
        }

        Cash -= CafeBalance.MilkPackCost;
        Milk += CafeBalance.MilkPackSize;
        Status = "Bought 4 milk. Lattes can show up from day 2.";
    }

    public void BuySyrup()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (SyrupUnlocked)
        {
            Status = "Syrup is already unlocked.";
            return;
        }
        if (Cash < CafeBalance.SyrupCost)
        {
            Status = "Not enough cash for syrup.";
            return;
        }

        Cash -= CafeBalance.SyrupCost;
        SyrupUnlocked = true;
        Status = "Syrup unlocked. Sweet tooths arrive on day 3.";
    }

    public void BuyUpgrade(UpgradeType type)
    {
        if (Phase != DayPhase.Shop || type == UpgradeType.None)
            return;
        if (Upgrade != UpgradeType.None)
        {
            Status = "You already picked an upgrade.";
            return;
        }
        if (Cash < CafeBalance.UpgradeCost)
        {
            Status = "Not enough cash for that upgrade.";
            return;
        }

        Cash -= CafeBalance.UpgradeCost;
        Upgrade = type;
        if (Prep != PrepType.None)
        {
            Prep = PrepType.None;
            Status = UpgradeName(type) + " installed. Stored prep was cleared.";
        }
        else
        {
            Status = UpgradeName(type) + " installed.";
        }
    }

    public void CyclePastry(int index)
    {
        if (Phase != DayPhase.Shop || index < 0 || index >= PastryChoice.Length)
            return;

        PastryChoice[index] = PastryChoice[index] % 3 + 1;
        Status = "Croissant " + (index + 1) + ": " + PastryName((PastryType)PastryChoice[index]) + ".";
    }

    public void PrepEspresso()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (Prep != PrepType.None)
        {
            Status = "The prep slot already holds something.";
            return;
        }
        if (Beans <= 0)
        {
            Status = "Need a bean to pull a spare shot.";
            return;
        }

        Beans--;
        Prep = PrepType.Espresso;
        Status = "Spare shot stored. It skips beans and brew on the next drink.";
    }

    public void PrepMilk()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (Prep != PrepType.None)
        {
            Status = "The prep slot already holds something.";
            return;
        }
        if (Milk <= 0)
        {
            Status = "Need milk to steam a spare.";
            return;
        }

        Milk--;
        Prep = PrepType.SteamedMilk;
        Status = "Steamed milk stored. It skips the milk step on the next latte.";
    }

    public void PayRent()
    {
        if (Phase != DayPhase.Shift || !DogPresent)
            return;
        if (Cash < RentDue)
        {
            Status = "Not enough cash for the dog.";
            return;
        }

        Cash -= RentDue;
        DogPresent = false;
        dogResolved = true;
        DogPaidThisShift = true;
        Status = "Rent paid. The dog leaves.";
    }

    public void OpenCafe()
    {
        if (Phase != DayPhase.Shop)
            return;

        int[] baked = new int[PastryStock.Length];
        for (int i = 0; i < PastryStock.Length; i++)
            baked[i] = PastryStock[i];
        Array.Clear(PastryStock, 0, PastryStock.Length);
        for (int i = 0; i < PastryChoice.Length; i++)
        {
            if (PastryChoice[i] != 0)
                PastryStock[PastryChoice[i]]++;
        }
        for (int i = 1; i < PastryStock.Length; i++)
            PastryStock[i] += baked[i];

        BuildQueue();
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].Clear();

        Focus = -1;
        ResetBrew();
        MachineBroken = false;
        Repair01 = 0f;
        dayBreakUsed = false;
        stationUses = 0;
        customersSpawned = 0;
        thiefInserted = false;
        ShiftEarned = 0;
        ShiftTips = 0;
        ShiftWalkouts = 0;
        ThiefLostThisShift = 0;
        ThiefAppeared = false;
        ThiefCaught = false;
        DogPresent = false;
        DogTime = 0f;
        DogVisited = false;
        DogPaidThisShift = false;
        heldSlot = -1;
        repairHeld = false;
        if (Day == 3)
        {
            BrokenTarget = DayBreakTargets[UnityEngine.Random.Range(0, DayBreakTargets.Length)];
            dogResolved = false;
            dogDelay = UnityEngine.Random.Range(CafeBalance.DogDelayMin, CafeBalance.DogDelayMax);
            Status = "Ring the bell for a customer. The " + BrokenName + " may break, and the dog may visit.";
        }
        else
        {
            BrokenTarget = BreakStation.None;
            dogResolved = true;
            dogDelay = 0f;
            Status = Day == 1
                ? "Ring the bell for a customer. Build their drink on a tray, then drag the cup to them."
                : "Ring the bell for a customer. A thief may grab the till.";
        }

        ClearStation();
        Phase = DayPhase.Shift;
    }

    public void InsertBeans()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (GrinderBusy)
        {
            Status = "The grinder is running.";
            return;
        }
        if (PuckReady)
        {
            Status = "Move the puck to the espresso machine.";
            return;
        }
        if (Beans <= 0)
        {
            Status = "Out of beans.";
            return;
        }

        Beans--;
        GrinderBusy = true;
        GrinderTimer = CafeBalance.GrindSeconds;
        Status = "Grinding beans.";
    }

    public void LoadPuck()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (!PuckReady)
        {
            Status = "Grind beans before you load a puck.";
            return;
        }
        if (EspressoBusy)
        {
            Status = "The espresso machine is running.";
            return;
        }
        if (EspressoShots > 0)
        {
            Status = "Take the shot first.";
            return;
        }
        if (RefuseBroken(BreakStation.CoffeePuck) || RefuseBroken(BreakStation.Espresso))
            return;

        PuckReady = false;
        EspressoBusy = true;
        EspressoTimer = CafeBalance.EspressoSeconds;
        NoteStationUse(BreakStation.CoffeePuck);
        NoteStationUse(BreakStation.Espresso);
        Status = "Pulling a shot.";
    }

    public void StartCoffee()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (CoffeeBusy || CoffeeUses > 0)
            return;
        if (RefuseBroken(BreakStation.Coffee))
            return;

        CoffeeBusy = true;
        CoffeeTimer = CafeBalance.CoffeeSeconds;
        NoteStationUse(BreakStation.Coffee);
        Status = "Brewing coffee.";
    }

    public void PourShot(int tray)
    {
        if (!CupReady(tray))
            return;
        if (!PourShotInto(Trays[tray]))
            return;

        TryFinishDrink();
    }

    public void PourShotOnWater()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (!WaterCup.Occupied)
        {
            Status = "The water tray is empty.";
            return;
        }

        PourShotInto(WaterCup);
    }

    public void PourCoffee(int tray)
    {
        if (!CupReady(tray))
            return;
        if (!PourInto(Trays[tray]))
            return;

        TryFinishDrink();
    }

    public void PourCoffeeOnWater()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (!WaterCup.Occupied)
        {
            Status = "The water tray is empty.";
            return;
        }

        PourInto(WaterCup);
    }

    public void InsertWaterCup(bool hot)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (WaterCup.Occupied)
        {
            Status = "Only one cup fits in the water tray.";
            return;
        }

        WaterCup.Clear();
        WaterCup.Occupied = true;
        WaterCup.Hot = hot;
        Status = (hot ? "Hot" : "Cold") + " cup in the water tray.";
    }

    public void FillWaterCup(bool hot)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (!WaterCup.Occupied)
        {
            Status = "The water tray is empty.";
            return;
        }

        WaterCup.Water = hot ? 1 : 2;
        Status = (hot ? "Hot" : "Cold") + " water added.";
    }

    public void PlaceWaterCup(int tray)
    {
        if (!WaterCup.Occupied)
            return;
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (tray < 0 || tray >= Trays.Length)
            return;
        if (Trays[tray].Occupied)
        {
            Status = "That tray already has a cup.";
            return;
        }

        Trays[tray].CopyFrom(WaterCup);
        WaterCup.Clear();
        if (waterCupWasActive)
            ActiveTray = tray;
        waterCupWasActive = false;
        Status = "Cup set on tray " + (tray + 1) + ".";
        TryFinishDrink();
    }

    public void MoveCupToWater(int tray)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (tray < 0 || tray >= Trays.Length || !Trays[tray].Occupied)
            return;
        if (WaterCup.Occupied)
        {
            Status = "Only one cup fits in the water tray.";
            return;
        }

        WaterCup.CopyFrom(Trays[tray]);
        Trays[tray].Clear();
        waterCupWasActive = ActiveTray == tray;
        if (waterCupWasActive)
            ActiveTray = -1;
        Status = "Moved the cup to the water tray.";
    }

    public void TrashWaterCup()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (!WaterCup.Occupied)
            return;

        bool cancel = waterCupWasActive || brewScored || FocusReady;
        WaterCup.Clear();
        waterCupWasActive = false;
        if (!cancel)
        {
            Status = "Tossed the cup.";
            return;
        }

        if (Focus >= 0 && Focus < Slots.Length && Slots[Focus].Occupied)
            Slots[Focus].DrinkReady = false;
        BrewTiming = false;
        brewScored = false;
        timingScores.Clear();
        FocusReady = false;
        BrewLocked = false;
        ActiveTray = -1;
        Status = "Tossed the cup. That order is reset.";
    }

    public void PlaceCup(bool hot, int tray)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (tray < 0 || tray >= Trays.Length)
            return;
        if (Trays[tray].Occupied)
        {
            Status = "That tray already has a cup.";
            return;
        }

        Trays[tray].Clear();
        Trays[tray].Occupied = true;
        Trays[tray].Hot = hot;
        Status = (hot ? "Hot" : "Cold") + " cup set on tray " + (tray + 1) + ".";
        TryFinishDrink();
    }

    public void AddStraw(int tray)
    {
        if (!CupReady(tray))
            return;

        if (Trays[tray].Straw)
        {
            Status = "That cup already has a straw.";
            return;
        }

        Trays[tray].Straw = true;
        Status = "Straw added.";
        TryFinishDrink();
    }

    public void AddIce(int tray)
    {
        if (!CupReady(tray))
            return;

        if (Trays[tray].Ice)
        {
            Status = "That cup already has ice.";
            return;
        }
        if (RefuseBroken(BreakStation.Ice))
            return;

        Trays[tray].Ice = true;
        NoteStationUse(BreakStation.Ice);
        Status = "Ice added.";
        TryFinishDrink();
    }

    public void AddWater(int tray, bool hot)
    {
        if (!CupReady(tray))
            return;

        Trays[tray].Water = hot ? 1 : 2;
        Status = (hot ? "Hot" : "Cold") + " water added.";
        TryFinishDrink();
    }

    public void SteamMilk()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (Milk <= 0)
        {
            Status = "Out of milk.";
            return;
        }

        Milk--;
        SteamedServings++;
        Status = "Milk steamed.";
        TryFinishDrink();
    }

    public void AddSyrup(int tray, int syrup)
    {
        if (!CupReady(tray))
            return;

        SyrupBits bit = SyrupBit(syrup);
        if (bit == SyrupBits.None)
            return;
        if ((Trays[tray].Syrup & bit) != 0)
        {
            Status = "That cup already has " + SyrupName(syrup) + ".";
            return;
        }

        Trays[tray].Syrup |= bit;
        Status = SyrupName(syrup) + " added.";
        TryFinishDrink();
    }

    public void AddTea(int tray, int tea)
    {
        if (!CupReady(tray))
            return;

        TeaBits bit = TeaBit(tea);
        if (bit == TeaBits.None)
            return;
        if ((Trays[tray].Tea & bit) != 0)
        {
            Status = "That cup already has " + TeaName(tea) + ".";
            return;
        }

        Trays[tray].Tea |= bit;
        Status = TeaName(tea) + " added.";
        TryFinishDrink();
    }

    public void AddMilk(int tray)
    {
        if (!CupReady(tray))
            return;
        if (Trays[tray].Milk)
        {
            Status = "That cup already has milk.";
            return;
        }
        if (Milk <= 0)
        {
            Status = "Out of milk.";
            return;
        }

        Milk--;
        Trays[tray].Milk = true;
        Status = "Milk added.";
        TryFinishDrink();
    }

    public void AddSteamedMilk(int tray)
    {
        if (!CupReady(tray))
            return;
        if (Trays[tray].SteamedMilk)
        {
            Status = "That cup already has steamed milk.";
            return;
        }
        if (SteamedServings <= 0)
        {
            Status = "Steam some milk first.";
            return;
        }

        SteamedServings--;
        Trays[tray].SteamedMilk = true;
        Status = "Steamed milk added.";
        TryFinishDrink();
    }

    public void TrashCup(int tray)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (tray < 0 || tray >= Trays.Length || !Trays[tray].Occupied)
        {
            Status = "Drag a cup from a tray into the trash.";
            return;
        }

        bool cancel = tray == ActiveTray || BrewTiming || brewScored || FocusReady;
        Trays[tray].Clear();
        if (ActiveTray == tray)
            ActiveTray = -1;
        if (!cancel)
        {
            Status = "Tossed the cup.";
            return;
        }

        if (Focus >= 0 && Focus < Slots.Length && Slots[Focus].Occupied)
            Slots[Focus].DrinkReady = false;
        BrewTiming = false;
        brewScored = false;
        timingScores.Clear();
        FocusReady = false;
        BrewLocked = false;
        Status = "Tossed the cup. That order is reset.";
    }

    public void MoveCup(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Trays.Length || to >= Trays.Length)
            return;
        if (!Trays[from].Occupied)
            return;
        if (Trays[to].Occupied)
        {
            Status = "That tray already has a cup.";
            return;
        }

        Trays[to].CopyFrom(Trays[from]);
        Trays[from].Clear();
        if (ActiveTray == from)
            ActiveTray = to;
        Status = "Moved the cup.";
    }

    public void HeatCroissant(PastryType type)
    {
        if (type == PastryType.None)
            return;
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (ToasterBusy || ToasterReady != PastryType.None)
        {
            Status = ToasterReady != PastryType.None
                ? "Take the croissant out first."
                : "The toaster is running.";
            return;
        }
        if (RefuseBroken(BreakStation.Toaster))
            return;

        ToasterBusy = true;
        NoteStationUse(BreakStation.Toaster);
        ToasterTimer = CafeBalance.ToastSeconds;
        toasting = type;
        Status = "Toasting a " + PastryName(type).ToLower() + " croissant.";
    }

    public void PlateCroissant()
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return;
        }
        if (ToasterReady == PastryType.None)
        {
            Status = "The toaster is empty.";
            return;
        }
        if (Plated != PastryType.None)
        {
            Status = "The plate already has a croissant.";
            return;
        }

        Plated = ToasterReady;
        ToasterReady = PastryType.None;
        Status = PastryName(Plated) + " croissant is on the plate.";
    }

    public void Continue()
    {
        if (Phase != DayPhase.CloseOfDay)
            return;

        if (Day >= 3)
        {
            PayQuestBonus();
            Phase = DayPhase.Summary;
            Status = "Three days done.";
            return;
        }

        Day++;
        EnterShop(rewardIfReady: true);
    }

    public void ClickSlot(int index)
    {
        if (Phase != DayPhase.Shift || index < 0 || index >= Slots.Length)
            return;

        Customer cat = Slots[index];
        if (!cat.Occupied)
            return;

        if (cat.IsThief)
        {
            Status = "Hold the thief.";
            return;
        }

        if (!cat.Accepted)
        {
            cat.Accepted = true;
            cat.PatienceMax = MaxPatience();
            cat.Patience = cat.PatienceMax;
            if (cat.WantedPastry != PastryType.None)
            {
                Focus = index;
                Status = "Order taken. Toast a " + PastryName(cat.WantedPastry).ToLower() + " croissant.";
            }
            else if (!BrewLocked)
            {
                Focus = index;
                if (!TryFinishDrink())
                    Status = "Order taken. Drag the drink together.";
            }
            else
            {
                Status = "Order taken. Finish the drink you already started.";
            }
            return;
        }

        if (cat.DrinkReady && Focus == index)
        {
            Status = "Drag the cup to the cat.";
            return;
        }

        if (!BrewLocked)
        {
            Focus = index;
            if (!TryFinishDrink())
                Status = "Working on this cat.";
            return;
        }

        Status = "Finish the current drink first.";
    }

    public void PressStation()
    {
        if (Phase != DayPhase.Shift)
            return;
        if (!BrewTiming)
        {
            Status = "The timer is not running.";
            return;
        }
        if (MachineBroken)
        {
            Status = "Hold Repair until the " + BrokenName + " is fixed.";
            return;
        }

        BrewTiming = false;
        float score = ScoreMarker();
        timingScores.Add(score);
        brewScored = true;
        Status = score > 0f ? "Brew timed." : "Missed the green zone.";
        if (!TryFinishDrink())
        {
            string missing = MissingHint();
            if (missing.Length > 0)
                Status = Status + " " + missing;
        }
    }

    public string QuestLabel()
    {
        if (QuestComplete)
            return QuestRewarded ? "Quest paid" : "Quest ready";
        return "Quest " + CleanServes + "/" + CafeBalance.QuestGoal;
    }

    public string PrepLabel()
    {
        if (Prep == PrepType.Espresso)
            return "spare shot";
        if (Prep == PrepType.SteamedMilk)
            return "steamed milk";
        return "none";
    }

    public string UpgradeLabel()
    {
        if (Upgrade == UpgradeType.None)
            return "none";
        return UpgradeName(Upgrade);
    }

    public string PastryChoiceLabel(int index)
    {
        if (index < 0 || index >= PastryChoice.Length || PastryChoice[index] == 0)
            return "Choose";
        return PastryName((PastryType)PastryChoice[index]);
    }

    public string PastryStockLabel()
    {
        return "plain " + PastryStock[(int)PastryType.Plain]
            + "   chocolate " + PastryStock[(int)PastryType.Chocolate]
            + "   cheese " + PastryStock[(int)PastryType.Cheese];
    }

    public string StockAdvice()
    {
        string text = "This shift is " + DrinksToday + " drinks. You have " + Beans + " beans and " + Milk + " milk.";
        if (Prep == PrepType.Espresso)
            text += " The spare shot is ready to pour.";
        if (Prep == PrepType.SteamedMilk)
            text += " Steamed milk is ready to pour.";
        return text;
    }

    public string StepsLabel()
    {
        string machines = MachineLine();
        if (FocusReady)
            return "Drag the cup to the cat.  " + machines;
        if (BrewTiming)
            return "Stop in the green.  " + machines;
        return machines;
    }

    string MachineLine()
    {
        string grind = GrinderBusy
            ? "Grinding"
            : (PuckReady ? "Puck ready" : "Grind idle");
        string shot = EspressoBusy
            ? "Pulling"
            : "Shots " + EspressoShots;
        string coffee = CoffeeBusy
            ? "Brewing"
            : "Pot x" + CoffeeUses;
        string line = grind + "   " + shot + "   " + coffee;
        if (ToasterBusy)
            line += "   Toasting";
        else if (ToasterReady != PastryType.None)
            line += "   Toast ready";
        if (Plated != PastryType.None)
            line += "   Plate full";
        line += "   Steamed " + SteamedServings;
        return line;
    }

    public string OrderBubble(Customer cat)
    {
        if (cat.IsThief)
            return "THIEF\nHold!";

        if (cat.WantedPastry != PastryType.None)
            return PastryName(cat.WantedPastry) + " croissant";
        return DrinkName(cat.Drink);
    }

    public static string DrinkName(DrinkType drink)
    {
        return DrinkRecipes.Get(drink).Name;
    }

    public static string PastryName(PastryType pastry)
    {
        if (pastry == PastryType.Chocolate)
            return "Chocolate";
        if (pastry == PastryType.Cheese)
            return "Cheese";
        if (pastry == PastryType.Plain)
            return "Plain";
        return "None";
    }

    bool PayQuestBonus()
    {
        if (!QuestComplete || QuestRewarded)
            return false;

        Cash += CafeBalance.QuestBonus;
        QuestRewarded = true;
        return true;
    }

    void EnterShop(bool rewardIfReady)
    {
        Phase = DayPhase.Shop;
        for (int i = 0; i < PastryChoice.Length; i++)
            PastryChoice[i] = 0;
        Array.Clear(PastryStock, 0, PastryStock.Length);

        if (rewardIfReady && PayQuestBonus())
            Status = "Quest bonus +15. " + ShopIntro();
        else
        {
            Status = ShopIntro();
        }
    }

    string ShopIntro()
    {
        if (Day == 1)
            return "Night before day 1. Buy beans, then open when you want.";
        if (Day == 2)
            return "Night before day 2. Any drink can be ordered. A thief may grab the till.";
        return "Night before day 3. A dog may visit, and a machine may break.";
    }

    void BuildQueue()
    {
        queue.Clear();
        int count = DrinksToday;
        for (int i = 0; i < count; i++)
        {
            PastryType pastry = PastryRequest(Day, i);
            DrinkType drink = DrinkType.BlackCoffee;
            CustomerKind kind = CustomerKind.Regular;
            if (pastry == PastryType.None)
            {
                drink = DrinkFor(i);
                kind = DrinkRecipes.Get(drink).Kind;
            }

            queue.Enqueue(new QueuedOrder
            {
                Kind = kind,
                Drink = drink,
                Pastry = pastry
            });
        }
    }

    static PastryType PastryRequest(int day, int index)
    {
        if (day == 1 && index == 1)
            return PastryType.Chocolate;
        if (day == 1 && index == 3)
            return PastryType.Plain;
        if (day == 2 && index == 0)
            return PastryType.Cheese;
        if (day == 2 && index == 2)
            return PastryType.Plain;
        if (day == 2 && index == 4)
            return PastryType.Chocolate;
        if (day == 3 && index == 1)
            return PastryType.Plain;
        if (day == 3 && index == 3)
            return PastryType.Chocolate;
        if (day == 3 && index == 6)
            return PastryType.Cheese;
        return PastryType.None;
    }

    void TickRepair(float dt)
    {
        if (!MachineBroken)
        {
            Repair01 = 0f;
            return;
        }

        if (repairHeld)
        {
            Repair01 += dt / CafeBalance.RepairHoldSeconds;
            if (Repair01 >= 1f)
            {
                MachineBroken = false;
                Repair01 = 0f;
                Status = "The " + BrokenName + " is repaired.";
            }
        }
        else
        {
            Repair01 = 0f;
        }
    }

    void TickMachines(float dt)
    {
        if (GrinderBusy)
        {
            GrinderTimer -= dt;
            if (GrinderTimer <= 0f)
            {
                GrinderBusy = false;
                GrinderTimer = 0f;
                PuckReady = true;
                Status = "The puck is ready.";
            }
        }

        if (EspressoBusy)
        {
            EspressoTimer -= dt;
            if (EspressoTimer <= 0f)
            {
                EspressoBusy = false;
                EspressoTimer = 0f;
                EspressoShots++;
                Status = "Espresso is ready.";
                TryFinishDrink();
            }
        }

        if (ToasterBusy)
        {
            ToasterTimer -= dt;
            if (ToasterTimer <= 0f)
            {
                ToasterBusy = false;
                ToasterTimer = 0f;
                ToasterReady = toasting;
                toasting = PastryType.None;
                Status = ToasterReady == PastryType.None
                    ? "The toaster is empty."
                    : "Croissant is ready. Drag it to the plate.";
            }
        }

        if (CoffeeBusy)
        {
            CoffeeTimer -= dt;
            if (CoffeeTimer <= 0f)
            {
                CoffeeBusy = false;
                CoffeeTimer = 0f;
                CoffeeUses = CafeBalance.CoffeeBatch;
                Status = "Coffee is ready. Four pours.";
            }
        }
    }

    void TickTiming(float dt)
    {
        if (!BrewTiming || MachineBroken)
            return;

        float speed = Upgrade == UpgradeType.FasterBrew ? CafeBalance.FastMarkerSpeed : CafeBalance.MarkerSpeed;
        Marker01 += markerDir * speed * dt;
        if (Marker01 >= 1f)
        {
            Marker01 = 1f;
            markerDir = -1;
        }
        else if (Marker01 <= 0f)
        {
            Marker01 = 0f;
            markerDir = 1;
        }
    }

    void TickCustomers(float dt)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            Customer cat = Slots[i];
            if (!cat.Occupied)
                continue;

            if (cat.IsThief)
            {
                if (heldSlot == i)
                    cat.ThiefHold += dt;
                else
                    cat.ThiefHold = 0f;

                if (cat.ThiefHold >= CafeBalance.ThiefHoldSeconds)
                {
                    DismissThief(i);
                    continue;
                }

                cat.ThiefTime -= dt;
                if (cat.ThiefTime <= 0f)
                    ThiefEscapes(i);
                continue;
            }

            if (!cat.Accepted)
                continue;

            cat.Patience -= dt;
            if (cat.Patience <= 0f)
                Walkout(i);
        }
    }

    public void RingBell()
    {
        if (Phase != DayPhase.Shift)
            return;

        if (FreeSlot() < 0)
        {
            Status = "Both seats are taken.";
            return;
        }

        if (!HasPendingSpawn())
        {
            Status = "No one else is waiting.";
            return;
        }

        SpawnNext();
    }

    bool HasPendingSpawn()
    {
        if (queue.Count > 0)
            return true;
        return Day == 2 && !thiefInserted && customersSpawned >= 2;
    }

    void SpawnNext()
    {
        int slot = FreeSlot();
        if (slot < 0)
            return;

        if (Day == 2 && !thiefInserted && customersSpawned >= 2)
        {
            Customer thief = Slots[slot];
            thief.Clear();
            thief.Occupied = true;
            thief.IsThief = true;
            thief.Kind = CustomerKind.Thief;
            thief.ThiefTime = CafeBalance.ThiefWindow;
            thiefInserted = true;
            ThiefAppeared = true;
            Status = "A thief is at the counter. Hold them.";
            return;
        }

        if (queue.Count == 0)
            return;

        QueuedOrder order = queue.Dequeue();
        Customer cat = Slots[slot];
        cat.Clear();
        cat.Occupied = true;
        cat.Kind = order.Kind;
        cat.Drink = order.Drink;
        cat.WantedPastry = order.Pastry;
        customersSpawned++;
    }

    float ScoreMarker()
    {
        float dist = Math.Abs(Marker01 - 0.5f);
        float half = GreenHalfWidth;
        if (dist > half)
            return 0f;
        return 1f - (dist / half) * 0.85f;
    }

    void Serve(int index)
    {
        Customer cat = Slots[index];
        TipGrade grade = ComputeGrade(cat);
        int tip = grade == TipGrade.Perfect ? CafeBalance.TipPerfect : grade == TipGrade.Okay ? CafeBalance.TipOkay : 0;
        int basePay = BasePay(cat.Drink);
        int total = basePay + tip;
        Cash += total;
        ShiftEarned += total;
        ShiftTips += tip;
        RunTips += tip;
        RunEarned += total;

        if (!QuestComplete)
        {
            CleanServes++;
            if (CleanServes >= CafeBalance.QuestGoal)
                QuestComplete = true;
        }

        string tipName = grade == TipGrade.Perfect ? "perfect" : grade == TipGrade.Okay ? "okay" : "none";
        Status = DrinkName(cat.Drink) + " " + basePay + " + tip " + tip + " (" + tipName + ").";
        ClearServedCup();
        cat.Clear();
        if (Focus == index)
        {
            Focus = -1;
            ResetBrew();
        }
    }

    TipGrade ComputeGrade(Customer cat)
    {
        float patienceRatio = cat.PatienceMax <= 0f ? 0f : Mathf.Clamp01(cat.Patience / cat.PatienceMax);
        float score;
        if (timingScores.Count == 0)
        {
            score = patienceRatio;
        }
        else
        {
            float accuracy = 0f;
            for (int i = 0; i < timingScores.Count; i++)
                accuracy += timingScores[i];
            accuracy /= timingScores.Count;
            score = accuracy * 0.65f + patienceRatio * 0.35f;
        }

        if (score >= 0.82f)
            return TipGrade.Perfect;
        if (score >= 0.40f)
            return TipGrade.Okay;
        return TipGrade.None;
    }

    void Walkout(int index)
    {
        bool wasFocus = Focus == index;
        Slots[index].Clear();
        ShiftWalkouts++;
        RunWalkouts++;
        if (wasFocus)
        {
            Focus = -1;
            ResetBrew();
        }

        if (!QuestComplete)
        {
            CleanServes = 0;
            Status = "Walkout. Quest progress reset.";
        }
        else
        {
            Status = "Walkout.";
        }
    }

    void DismissThief(int index)
    {
        Slots[index].Clear();
        thiefInserted = true;
        ThiefCaught = true;
        Status = "The thief gives up.";
    }

    void ThiefEscapes(int index)
    {
        int lost = CafeBalance.ThiefSteal;
        if (lost > ShiftEarned)
            lost = ShiftEarned;
        if (lost > Cash)
            lost = Cash;
        Cash -= lost;
        ThiefLostThisShift += lost;
        Slots[index].Clear();
        thiefInserted = true;
        Status = lost > 0 ? "The thief took " + lost + " from this shift." : "The thief found an empty till.";
    }

    void EndShift()
    {
        Phase = DayPhase.CloseOfDay;
        Focus = -1;
        ResetBrew();
        ClearStation();
        repairHeld = false;
        DogPresent = false;
        heldSlot = -1;
        Status = "Day " + Day + " is closed.";
    }

    bool ShiftWorkDone()
    {
        if (OccupiedCount() > 0 || queue.Count > 0)
            return false;
        return Day != 2 || thiefInserted;
    }

    void TickDog(float dt)
    {
        if (dogResolved)
            return;

        if (!DogPresent)
        {
            dogDelay -= dt;
            if (dogDelay <= 0f || ShiftWorkDone())
                ArriveDog();
            return;
        }

        DogTime -= dt;
        if (DogTime <= 0f)
            DismissDogUnpaid();
    }

    void ArriveDog()
    {
        DogPresent = true;
        DogTime = CafeBalance.DogWindow;
        DogVisited = true;
        Status = "The black dog is here. Pay $" + RentDue + ".";
    }

    void DismissDogUnpaid()
    {
        DogPresent = false;
        dogResolved = true;
        DogPaidThisShift = false;
        RentKept = false;
        if (MachineBroken)
            Status = "The dog left. The " + BrokenName + " is still broken.";
        else if (!dayBreakUsed && BrokenTarget != BreakStation.None)
            BreakMachine("The dog broke the " + BrokenName + ".");
        else
            Status = "The dog left.";
    }

    DrinkType DrinkFor(int index)
    {
        if (Day == 1)
        {
            DrinkType[] shortMenu = DrinkRecipes.AtMostSteps(4);
            return shortMenu[index % shortMenu.Length];
        }

        DrinkType[] menu = (DrinkType[])Enum.GetValues(typeof(DrinkType));
        return menu[((Day - 1) * DrinksToday + index) % menu.Length];
    }

    bool RefuseBroken(BreakStation station)
    {
        if (BrokenTarget != station)
            return false;
        if (MachineBroken)
        {
            Status = "Hold Repair until the " + StationName(station) + " is fixed.";
            return true;
        }
        if (dayBreakUsed || stationUses != 1)
            return false;

        BreakMachine("The " + StationName(station) + " broke.");
        return true;
    }

    void NoteStationUse(BreakStation station)
    {
        if (BrokenTarget == station && !dayBreakUsed && !MachineBroken)
            stationUses++;
    }

    static readonly BreakStation[] DayBreakTargets =
    {
        BreakStation.CoffeePuck,
        BreakStation.Coffee,
        BreakStation.Toaster,
        BreakStation.Ice,
        BreakStation.Espresso
    };

    void ResetBrew()
    {
        BrewLocked = false;
        FocusReady = false;
        BrewTiming = false;
        brewScored = false;
        ActiveTray = -1;
        timingScores.Clear();
        Marker01 = 0f;
    }

    void ClearStation()
    {
        EspressoShots = 0;
        SteamedServings = 0;
        ActiveTray = -1;
        brewScored = false;
        GrinderBusy = false;
        GrinderTimer = 0f;
        PuckReady = false;
        EspressoBusy = false;
        EspressoTimer = 0f;
        ToasterBusy = false;
        ToasterTimer = 0f;
        if (ToasterReady != PastryType.None)
            PastryStock[(int)ToasterReady]++;
        if (Plated != PastryType.None)
            PastryStock[(int)Plated]++;
        ToasterReady = PastryType.None;
        Plated = PastryType.None;
        toasting = PastryType.None;
        CoffeeBusy = false;
        CoffeeTimer = 0f;
        CoffeeUses = 0;
        WaterCup.Clear();
        waterCupWasActive = false;
        for (int i = 0; i < Trays.Length; i++)
            Trays[i].Clear();
    }

    void BreakMachine(string status)
    {
        MachineBroken = true;
        dayBreakUsed = true;
        Repair01 = 0f;
        BrewTiming = false;
        Status = status;
    }

    static string StationName(BreakStation station)
    {
        if (station == BreakStation.CoffeePuck)
            return "coffee puck";
        if (station == BreakStation.Coffee)
            return "coffee machine";
        if (station == BreakStation.Toaster)
            return "toaster";
        if (station == BreakStation.Ice)
            return "ice";
        if (station == BreakStation.Espresso)
            return "espresso machine";
        return "machine";
    }

    bool PourShotInto(TrayCup cup)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return false;
        }
        if (EspressoBusy || EspressoShots <= 0)
        {
            Status = "Pull a shot first.";
            return false;
        }
        if (cup == null || !cup.Occupied)
        {
            Status = "That needs a cup.";
            return false;
        }
        if (cup.Shot)
        {
            Status = "That cup already has a shot.";
            return false;
        }

        cup.Shot = true;
        EspressoShots--;
        Status = "Shot poured.";
        return true;
    }

    bool PourInto(TrayCup cup)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return false;
        }
        if (CoffeeBusy || CoffeeUses <= 0)
        {
            Status = "Brew coffee first.";
            return false;
        }
        if (cup == null || !cup.Occupied)
        {
            Status = "That needs a cup.";
            return false;
        }
        if (cup.Coffee)
        {
            Status = "That cup already has coffee.";
            return false;
        }

        cup.Coffee = true;
        CoffeeUses--;
        Status = CoffeeUses > 0
            ? "Coffee poured. " + CoffeeUses + " left."
            : "Coffee poured. Click the pot to brew more.";
        return true;
    }

    void ClearServedCup()
    {
        int tray = ActiveTray;
        if (tray < 0)
        {
            for (int i = 0; i < Trays.Length; i++)
            {
                if (!Trays[i].Occupied)
                    continue;
                tray = i;
                break;
            }
        }
        if (tray >= 0 && tray < Trays.Length)
            Trays[tray].Clear();
    }

    bool CupReady(int tray)
    {
        if (Phase != DayPhase.Shift)
        {
            Status = "Open the cafe first.";
            return false;
        }
        if (tray < 0 || tray >= Trays.Length || !Trays[tray].Occupied)
        {
            Status = "Put a cup on a tray first.";
            return false;
        }
        return true;
    }

    bool AnyOccupiedTray()
    {
        for (int i = 0; i < Trays.Length; i++)
        {
            if (Trays[i].Occupied)
                return true;
        }
        return false;
    }

    bool TryFinishDrink()
    {
        if (Phase != DayPhase.Shift || Focus < 0)
            return false;

        Customer cat = Slots[Focus];
        if (!cat.Occupied || cat.IsThief || !cat.Accepted || cat.WantedPastry != PastryType.None)
        {
            cat.DrinkReady = false;
            FocusReady = false;
            return false;
        }

        int tray = -1;
        for (int i = 0; i < Trays.Length; i++)
        {
            if (!DrinkRecipes.Matches(Trays[i], cat.Drink))
                continue;
            tray = i;
            break;
        }

        if (tray < 0)
        {
            cat.DrinkReady = false;
            FocusReady = false;
            ActiveTray = -1;
            return false;
        }

        bool newlyReady = !cat.DrinkReady;
        cat.DrinkReady = true;
        FocusReady = true;
        ActiveTray = tray;
        if (newlyReady)
            Status = "Ready. Drag the cup to the cat.";
        return true;
    }

    public void OfferCup(int index, int tray)
    {
        if (Phase != DayPhase.Shift || index < 0 || index >= Slots.Length)
            return;
        if (tray < 0 || tray >= Trays.Length || !Trays[tray].Occupied)
            return;

        Customer cat = Slots[index];
        if (!cat.Occupied || cat.IsThief)
        {
            Status = "They don't want a drink.";
            return;
        }
        if (!cat.Accepted)
        {
            Status = "Take the order before you hand it over.";
            return;
        }
        if (cat.WantedPastry != PastryType.None)
        {
            Status = "They don't want a drink.";
            return;
        }
        if (!DrinkRecipes.Matches(Trays[tray], cat.Drink))
        {
            Status = "They don't want that.";
            return;
        }

        ActiveTray = tray;
        Serve(index);
    }

    public bool OfferPastry(int index, bool fromPlate)
    {
        if (Phase != DayPhase.Shift || index < 0 || index >= Slots.Length)
            return false;

        PastryType pastry = fromPlate ? Plated : ToasterReady;
        if (pastry == PastryType.None)
            return false;

        Customer cat = Slots[index];
        if (!cat.Occupied || cat.IsThief)
        {
            Status = "They don't want a croissant.";
            return false;
        }
        if (!cat.Accepted)
        {
            Status = "Take the order before you hand it over.";
            return false;
        }
        if (cat.WantedPastry == PastryType.None)
        {
            Status = "They don't want a croissant.";
            return false;
        }
        if (pastry != cat.WantedPastry)
        {
            Status = "They don't want that.";
            return false;
        }

        if (fromPlate)
            Plated = PastryType.None;
        else
            ToasterReady = PastryType.None;

        int pay = CafeBalance.PastryMatch;
        Cash += pay;
        ShiftEarned += pay;
        RunEarned += pay;
        if (!QuestComplete)
        {
            CleanServes++;
            if (CleanServes >= CafeBalance.QuestGoal)
                QuestComplete = true;
        }

        Status = PastryName(pastry) + " croissant " + pay + ".";
        cat.Clear();
        if (Focus == index)
        {
            Focus = -1;
            ResetBrew();
        }
        return true;
    }

    string MissingHint()
    {
        if (Focus < 0 || !Slots[Focus].Accepted || Slots[Focus].IsThief)
            return "Take an order.";
        if (!AnyOccupiedTray())
            return "Set a cup on a tray.";
        return "The cup is still missing something.";
    }

    static string SyrupName(int syrup)
    {
        if (syrup == 1)
            return "Chocolate";
        if (syrup == 2)
            return "Vanilla";
        if (syrup == 3)
            return "Strawberry";
        if (syrup == 4)
            return "Caramel";
        if (syrup == 5)
            return "White chocolate";
        if (syrup == 6)
            return "Coconut";
        if (syrup == 7)
            return "Almond";
        return "Syrup";
    }

    static SyrupBits SyrupBit(int syrup)
    {
        if (syrup < 1 || syrup > 7)
            return SyrupBits.None;
        return (SyrupBits)(1 << (syrup - 1));
    }

    static string TeaName(int tea)
    {
        if (tea == 1)
            return "Black tea";
        if (tea == 2)
            return "Green tea";
        if (tea == 3)
            return "Matcha";
        if (tea == 4)
            return "White tea";
        return "Tea";
    }

    static TeaBits TeaBit(int tea)
    {
        if (tea < 1 || tea > 4)
            return TeaBits.None;
        return (TeaBits)(1 << (tea - 1));
    }

    int OccupiedCount()
    {
        int count = 0;
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].Occupied)
                count++;
        }
        return count;
    }

    int FreeSlot()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            if (!Slots[i].Occupied)
                return i;
        }
        return -1;
    }

    float MaxPatience()
    {
        return Upgrade == UpgradeType.LongerPatience
            ? CafeBalance.LongPatienceSeconds
            : CafeBalance.PatienceSeconds;
    }

    static int BasePay(DrinkType drink)
    {
        return DrinkRecipes.Get(drink).Pay;
    }

    static string UpgradeName(UpgradeType type)
    {
        if (type == UpgradeType.FasterBrew)
            return "Faster brew";
        if (type == UpgradeType.LongerPatience)
            return "Longer patience";
        if (type == UpgradeType.CheaperRent)
            return "Cheaper rent";
        return "None";
    }

}

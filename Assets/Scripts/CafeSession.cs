using System;
using System.Collections.Generic;

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

    public void Clear()
    {
        Occupied = false;
        IsThief = false;
        Kind = CustomerKind.Regular;
        Drink = DrinkType.Coffee;
        WantedPastry = PastryType.None;
        Accepted = false;
        Patience = 0f;
        PatienceMax = 0f;
        DrinkReady = false;
        ThiefTime = 0f;
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
    public bool HasMechanic;
    public UpgradeType Upgrade = UpgradeType.None;
    public PrepType Prep = PrepType.None;
    public bool RentPaid;
    public bool RentSkipped;

    public bool MachineBroken;
    public float Repair01;
    public bool BrewTiming;
    public float Marker01;
    public int Focus = -1;
    public bool BrewLocked;
    public bool FocusReady;
    public BrewStep CurrentStep;

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
    readonly List<BrewStep> steps = new List<BrewStep>();
    readonly List<float> timingScores = new List<float>();

    int stepIndex;
    int markerDir = 1;
    int customersSpawned;
    int brewsStarted;
    bool dayBreakUsed;
    bool thiefInserted;
    bool repairHeld;
    float mechanicTimer;
    float spawnTimer;

    public int RentDue => Upgrade == UpgradeType.CheaperRent ? CafeBalance.CheapRentCost : CafeBalance.RentCost;

    public int DrinksToday => Day == 1 ? 4 : Day == 2 ? 6 : 8;

    public float GreenHalfWidth => Upgrade == UpgradeType.FasterBrew ? CafeBalance.FastGreenHalf : CafeBalance.GreenHalf;

    public bool CanOpen
    {
        get
        {
            for (int i = 0; i < PastryChoice.Length; i++)
            {
                if (PastryChoice[i] == 0)
                    return false;
            }
            return true;
        }
    }

    public int PastryCount
    {
        get
        {
            int total = 0;
            for (int i = 1; i < PastryStock.Length; i++)
                total += PastryStock[i];
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
        HasMechanic = false;
        Upgrade = UpgradeType.None;
        Prep = PrepType.None;
        RentPaid = false;
        RentSkipped = false;
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
        brewsStarted = 0;
        customersSpawned = 0;
        dayBreakUsed = false;
        thiefInserted = false;
        repairHeld = false;
        mechanicTimer = 0f;
        spawnTimer = 0f;
        stepIndex = 0;
        markerDir = 1;
        queue.Clear();
        steps.Clear();
        timingScores.Clear();
        Array.Clear(PastryChoice, 0, PastryChoice.Length);
        Array.Clear(PastryStock, 0, PastryStock.Length);
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].Clear();

        EnterShop(rewardIfReady: false);
        Status = "Night before day 1. Buy beans, bake 3 croissants, then open.";
    }

    public void Tick(float dt)
    {
        if (Phase != DayPhase.Shift)
            return;

        TickRepair(dt);
        TickTiming(dt);
        TickCustomers(dt);
        TickSpawn(dt);

        if (OccupiedCount() == 0 && queue.Count == 0 && !(Day == 2 && !thiefInserted))
            EndShift();
    }

    public void SetRepairHeld(bool held) => repairHeld = held;

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

    public void HireMechanic()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (HasMechanic)
        {
            Status = "The mechanic is already hired.";
            return;
        }
        if (Cash < CafeBalance.MechanicCost)
        {
            Status = "Not enough cash to hire the mechanic.";
            return;
        }

        Cash -= CafeBalance.MechanicCost;
        HasMechanic = true;
        Status = "Mechanic hired. The next breakdown repairs itself.";
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
        if (Phase != DayPhase.Shop || Day != 3 || RentPaid)
            return;
        if (Cash < RentDue)
        {
            Status = "Not enough cash for the dog. Open anyway and the brewer starts broken.";
            return;
        }

        Cash -= RentDue;
        RentPaid = true;
        Status = "Rent paid. The dog leaves the machines alone.";
    }

    public void OpenCafe()
    {
        if (Phase != DayPhase.Shop)
            return;
        if (!CanOpen)
        {
            Status = "Bake exactly 3 croissants before you open.";
            return;
        }

        if (Day == 3 && !RentPaid)
            RentSkipped = true;

        Array.Clear(PastryStock, 0, PastryStock.Length);
        for (int i = 0; i < PastryChoice.Length; i++)
            PastryStock[PastryChoice[i]]++;

        BuildQueue();
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].Clear();

        Focus = -1;
        ResetBrew();
        MachineBroken = false;
        Repair01 = 0f;
        mechanicTimer = 0f;
        dayBreakUsed = false;
        brewsStarted = 0;
        customersSpawned = 0;
        thiefInserted = false;
        spawnTimer = 0.35f;
        ShiftEarned = 0;
        ShiftTips = 0;
        ShiftWalkouts = 0;
        ThiefLostThisShift = 0;
        ThiefAppeared = false;
        ThiefCaught = false;
        repairHeld = false;

        if (Day == 3 && RentSkipped)
        {
            MachineBroken = true;
            dayBreakUsed = true;
            Status = "The dog broke the brewer. Repair it before you brew.";
        }
        else if (Day == 1)
        {
            Status = "Click a cat, add beans, stop in the green, then click the cat again.";
        }
        else if (Day == 2)
        {
            Status = "Day 2 is open. The brewer can break, and a thief may grab the till.";
        }
        else
        {
            Status = SyrupUnlocked
                ? "Day 3 is open. Sweet tooths want syrup coffee."
                : "Day 3 is open. Syrup is locked, so no sweet tooths.";
        }

        Phase = DayPhase.Shift;
        SpawnNext();
        SpawnNext();
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
            cat.Clear();
            thiefInserted = true;
            ThiefCaught = true;
            Status = "You caught the thief.";
            return;
        }

        if (!cat.Accepted)
        {
            cat.Accepted = true;
            cat.PatienceMax = MaxPatience();
            cat.Patience = cat.PatienceMax;
            if (!BrewLocked)
            {
                Focus = index;
                Status = "Order taken. Use the station.";
            }
            else
            {
                Status = "Order taken. Finish the drink you already started.";
            }
            return;
        }

        if (cat.DrinkReady && Focus == index)
        {
            Serve(index);
            return;
        }

        if (!BrewLocked)
        {
            Focus = index;
            Status = "Station set to this cat.";
            return;
        }

        Status = "Finish the current drink first.";
    }

    public void PressStation()
    {
        if (Phase != DayPhase.Shift)
            return;
        if (Focus < 0 || !Slots[Focus].Occupied || Slots[Focus].IsThief || !Slots[Focus].Accepted)
        {
            Status = "Take an order first.";
            return;
        }

        Customer cat = Slots[Focus];
        if (cat.DrinkReady)
        {
            Status = "Click the cat to serve.";
            return;
        }
        if (MachineBroken)
        {
            Status = HasMechanic
                ? "The mechanic is repairing the brewer."
                : "Hold Repair until the brewer is fixed.";
            return;
        }

        if (!BrewLocked)
        {
            if (ShouldBreak())
            {
                MachineBroken = true;
                dayBreakUsed = true;
                mechanicTimer = 0f;
                Status = "The brewer broke.";
                return;
            }

            StartBrew(cat);
            return;
        }

        AdvanceStep(cat);
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
        bool milkReady = Milk > 0 || Prep == PrepType.SteamedMilk;
        string text = "This shift is " + DrinksToday + " drinks. You have " + Beans + " beans. Each drink needs one bean.";
        if (Prep == PrepType.Espresso)
            text += " The spare shot covers one drink.";
        if (Day >= 2 && !milkReady)
            text += " No milk, so no lattes.";
        else if (Day >= 2)
            text += " Milk is stocked, so some cats order lattes.";
        if (Day == 3 && SyrupUnlocked)
            text += " Sweet tooths will order syrup coffee.";
        else if (Day == 3)
            text += " Syrup is locked, so those cats order coffee.";
        return text;
    }

    public string StepsLabel()
    {
        if (!BrewLocked)
            return "Station idle";
        if (FocusReady)
            return "Ready to serve";

        string label = "";
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0)
                label += "   ";
            string name = StepName(steps[i]);
            label += i == stepIndex ? "[" + name + "]" : name;
        }
        return label;
    }

    public string OrderBubble(Customer cat)
    {
        if (cat.IsThief)
            return "THIEF\nClick!";

        string bubble = DrinkName(cat.Drink);
        if (cat.WantedPastry != PastryType.None)
            bubble += "\n+ " + PastryName(cat.WantedPastry);
        return bubble;
    }

    public static string DrinkName(DrinkType drink)
    {
        if (drink == DrinkType.Latte)
            return "Latte";
        if (drink == DrinkType.SyrupCoffee)
            return "Syrup coffee";
        return "Coffee";
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

    public static string KindName(CustomerKind kind)
    {
        if (kind == CustomerKind.Milk)
            return "Milk cat";
        if (kind == CustomerKind.Sweet)
            return "Sweet tooth";
        if (kind == CustomerKind.Thief)
            return "Thief";
        return "Regular";
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
            return "Night before day 1. Buy beans and bake 3 croissants.";
        if (Day == 2)
            return "Night before day 2. Milk unlocks lattes. The brewer may break.";
        return "Night before day 3. The black dog is here for rent.";
    }

    void BuildQueue()
    {
        queue.Clear();
        int count = DrinksToday;
        for (int i = 0; i < count; i++)
        {
            bool milkReady = Milk > 0 || Prep == PrepType.SteamedMilk;
            DrinkType drink = DrinkType.Coffee;
            CustomerKind kind = CustomerKind.Regular;
            if (Day == 2)
            {
                if (milkReady && i % 2 == 0)
                {
                    drink = DrinkType.Latte;
                    kind = CustomerKind.Milk;
                }
            }
            else if (Day == 3)
            {
                if (SyrupUnlocked && (i == 1 || i == 4 || i == 6))
                {
                    drink = DrinkType.SyrupCoffee;
                    kind = CustomerKind.Sweet;
                }
                else if (milkReady && (i == 0 || i == 3 || i == 5))
                {
                    drink = DrinkType.Latte;
                    kind = CustomerKind.Milk;
                }
            }

            queue.Enqueue(new QueuedOrder
            {
                Kind = kind,
                Drink = drink,
                Pastry = PastryRequest(Day, i)
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
            mechanicTimer = 0f;
            return;
        }

        if (HasMechanic)
        {
            mechanicTimer += dt;
            Repair01 = mechanicTimer / CafeBalance.MechanicRepairSeconds;
            if (mechanicTimer >= CafeBalance.MechanicRepairSeconds)
            {
                MachineBroken = false;
                Repair01 = 0f;
                mechanicTimer = 0f;
                Status = "The mechanic repaired the brewer.";
            }
            return;
        }

        if (repairHeld)
        {
            Repair01 += dt / CafeBalance.RepairHoldSeconds;
            if (Repair01 >= 1f)
            {
                MachineBroken = false;
                Repair01 = 0f;
                Status = "Brewer repaired.";
            }
        }
        else
        {
            Repair01 = 0f;
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

    void TickSpawn(float dt)
    {
        if (OccupiedCount() >= 2 || !HasPendingSpawn())
            return;

        spawnTimer -= dt;
        if (spawnTimer > 0f)
            return;

        SpawnNext();
        spawnTimer = 0.45f;
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
            Status = "A thief is at the counter. Click them.";
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

    void StartBrew(Customer cat)
    {
        brewsStarted++;
        BrewLocked = true;
        FocusReady = false;
        BrewTiming = false;
        stepIndex = 0;
        timingScores.Clear();
        steps.Clear();
        steps.Add(BrewStep.Beans);
        steps.Add(BrewStep.Brew);
        if (cat.Drink == DrinkType.Latte)
            steps.Add(BrewStep.Milk);
        else if (cat.Drink == DrinkType.SyrupCoffee)
            steps.Add(BrewStep.Syrup);

        string prepNote = "";
        if (Prep == PrepType.Espresso)
        {
            steps.Remove(BrewStep.Beans);
            steps.Remove(BrewStep.Brew);
            Prep = PrepType.None;
            prepNote = "Used the spare shot. ";
        }
        else if (Prep == PrepType.SteamedMilk && cat.Drink == DrinkType.Latte)
        {
            steps.Remove(BrewStep.Milk);
            Prep = PrepType.None;
            prepNote = "Used the steamed milk. ";
        }

        if (stepIndex >= steps.Count)
        {
            cat.DrinkReady = true;
            FocusReady = true;
            Status = prepNote + "Drink is ready. Click the cat.";
            return;
        }

        CurrentStep = steps[stepIndex];
        Status = prepNote + StepPrompt(CurrentStep);
    }

    void AdvanceStep(Customer cat)
    {
        if (stepIndex >= steps.Count)
            return;

        BrewStep step = steps[stepIndex];
        if (step == BrewStep.Beans)
        {
            if (Beans <= 0)
            {
                Status = "Out of beans. This cat will leave if you can't finish.";
                return;
            }

            Beans--;
            stepIndex++;
            Status = "Beans added.";
        }
        else if (step == BrewStep.Syrup)
        {
            stepIndex++;
            Status = "Syrup added.";
        }
        else if (step == BrewStep.Milk)
        {
            if (!BrewTiming)
            {
                if (Milk <= 0)
                {
                    Status = "Out of milk. This cat will leave if you can't finish.";
                    return;
                }

                Milk--;
                BeginTiming();
                Status = "Stop in the green zone.";
                return;
            }

            BrewTiming = false;
            float milkScore = ScoreMarker();
            timingScores.Add(milkScore);
            stepIndex++;
            Status = milkScore > 0f ? "Milk steamed." : "Missed the green zone.";
        }
        else if (step == BrewStep.Brew)
        {
            if (!BrewTiming)
            {
                BeginTiming();
                Status = "Stop in the green zone.";
                return;
            }

            BrewTiming = false;
            float brewScore = ScoreMarker();
            timingScores.Add(brewScore);
            stepIndex++;
            Status = brewScore > 0f ? "Brew timed." : "Missed the green zone.";
        }

        CurrentStep = stepIndex < steps.Count ? steps[stepIndex] : step;
        if (stepIndex >= steps.Count)
        {
            cat.DrinkReady = true;
            FocusReady = true;
            BrewTiming = false;
            Status = "Ready. Click the cat to serve.";
        }
        else if (!BrewTiming)
        {
            Status = StepPrompt(steps[stepIndex]);
        }
    }

    void BeginTiming()
    {
        BrewTiming = true;
        Marker01 = 0f;
        markerDir = 1;
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
        int pastryPay = 0;
        string pastryNote = "";
        if (cat.WantedPastry != PastryType.None)
        {
            if (!TryTakePastry(cat.WantedPastry, out bool exact))
            {
                Status = "That order needs a croissant, and the case is empty.";
                return;
            }

            pastryPay = exact ? CafeBalance.PastryMatch : CafeBalance.PastryMismatch;
            pastryNote = exact ? " + croissant 6" : " + wrong croissant 2";
        }

        TipGrade grade = ComputeGrade(cat);
        int tip = grade == TipGrade.Perfect ? CafeBalance.TipPerfect : grade == TipGrade.Okay ? CafeBalance.TipOkay : 0;
        int basePay = BasePay(cat.Drink);
        int total = basePay + tip + pastryPay;
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
        Status = DrinkName(cat.Drink) + " " + basePay + " + tip " + tip + " (" + tipName + ")" + pastryNote + ".";
        cat.Clear();
        if (Focus == index)
        {
            Focus = -1;
            ResetBrew();
        }
    }

    bool TryTakePastry(PastryType wanted, out bool exact)
    {
        if (PastryStock[(int)wanted] > 0)
        {
            PastryStock[(int)wanted]--;
            exact = true;
            return true;
        }

        for (int i = 1; i < PastryStock.Length; i++)
        {
            if (PastryStock[i] <= 0)
                continue;
            PastryStock[i]--;
            exact = false;
            return true;
        }

        exact = false;
        return false;
    }

    TipGrade ComputeGrade(Customer cat)
    {
        float patienceRatio = cat.PatienceMax <= 0f ? 0f : Clamp01(cat.Patience / cat.PatienceMax);
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
        repairHeld = false;
        Status = "Day " + Day + " is closed.";
    }

    bool ShouldBreak()
    {
        if (dayBreakUsed || Day == 1)
            return false;
        if (Day == 3 && RentSkipped)
            return false;
        return brewsStarted == 1;
    }

    void ResetBrew()
    {
        BrewLocked = false;
        FocusReady = false;
        BrewTiming = false;
        stepIndex = 0;
        steps.Clear();
        timingScores.Clear();
        Marker01 = 0f;
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
        if (drink == DrinkType.Latte)
            return CafeBalance.LattePay;
        if (drink == DrinkType.SyrupCoffee)
            return CafeBalance.SyrupPay;
        return CafeBalance.CoffeePay;
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

    static string StepName(BrewStep step)
    {
        if (step == BrewStep.Beans)
            return "Beans";
        if (step == BrewStep.Brew)
            return "Brew";
        if (step == BrewStep.Milk)
            return "Milk";
        return "Syrup";
    }

    static string StepPrompt(BrewStep step)
    {
        if (step == BrewStep.Beans)
            return "Add beans.";
        if (step == BrewStep.Brew)
            return "Start the brew, then stop in the green.";
        if (step == BrewStep.Milk)
            return "Steam the milk, then stop in the green.";
        return "Add syrup.";
    }

    static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        if (value > 1f)
            return 1f;
        return value;
    }
}

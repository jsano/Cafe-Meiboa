using UnityEngine;

public enum CafeSpot
{
    Beans,
    Coffee,
    Espresso,
    Milk,
    SteamedMilk,
    CupHot,
    CupCold,
    Straw,
    Ice,
    WaterHot,
    WaterCold,
    SyrupChocolate,
    SyrupVanilla,
    SyrupStrawberry,
    SyrupHoney,
    Tray,
    Trash,
    Toaster,
    CroissantPlain,
    CroissantChocolate,
    CroissantCheese
}

public class CafeHotspot : MonoBehaviour
{
    public CafeSpot Spot;
    public int Index;
    public CafeBoard Board;
}

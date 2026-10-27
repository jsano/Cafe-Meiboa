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
    SyrupCaramel,
    Tray,
    Trash,
    Toaster,
    CroissantPlain,
    CroissantChocolate,
    CroissantCheese,
    Grinder,
    CoffeePuck,
    WaterTray,
    Plate,
    SyrupWhiteChocolate,
    SyrupCoconut,
    SyrupAlmond,
    TeaBlack,
    TeaGreen,
    TeaMatcha,
    TeaWhite
}

public class CafeHotspot : MonoBehaviour
{
    public CafeSpot Spot;
    public int Index;
    public CafeBoard Board;
}

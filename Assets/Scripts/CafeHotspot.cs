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
    TeaMatcha,
    TeaLavender,
    TeaWhite
}

public class CafeHotspot : MonoBehaviour
{
    public CafeSpot Spot;
    public int Index;
    public CafeBoard Board;
}

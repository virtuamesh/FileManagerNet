namespace FileManagerNet.Models;

/// <summary>View layouts offered by the View tab.</summary>
public enum ViewLayout
{
    /// <summary>Extra large icon grid.</summary>
    ExtraLargeIcons,

    /// <summary>Large icon grid.</summary>
    LargeIcons,

    /// <summary>Medium icon grid.</summary>
    MediumIcons,

    /// <summary>Small icon grid.</summary>
    SmallIcons,

    /// <summary>Single-column list.</summary>
    List,

    /// <summary>Multi-column details table with sortable headers.</summary>
    Details,

    /// <summary>Tile view combining icon, name and metadata.</summary>
    Tiles
}

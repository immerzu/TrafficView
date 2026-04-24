namespace TrafficView
{
    internal enum AdapterAvailabilityState
    {
        Automatic,
        Available,
        Inactive,
        Missing
    }

    internal enum PopupDisplayMode
    {
        Standard,
        MiniGraph,
        MiniSoft,
        Simple,
        SimpleBlue
    }

    internal enum PopupSectionMode
    {
        Both,
        LeftOnly,
        RightOnly
    }

    internal enum AppBarEdge : uint
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3
    }
}

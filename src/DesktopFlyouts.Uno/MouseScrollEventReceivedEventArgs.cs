namespace DesktopFlyouts;

public enum ScrollOrientation
{
    Vertical,
    Horizontal
}

public class MouseScrollEventReceivedEventArgs : EventArgs
{
    public int Delta { get; }
    public ScrollOrientation Orientation { get; }

    internal MouseScrollEventReceivedEventArgs(int delta, string orientation)
    {
        Delta = delta;
        Orientation = orientation == "horizontal" ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
    }
}

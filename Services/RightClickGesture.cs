namespace BetterCM.Services;

// Once a down event is owned, its up event must be swallowed, even if the pointer,
// focus, setting or popup visibility changes. Pointer movement never replays input.
public sealed class RightClickGesture
{
    private TriggerRequest? _request;
    public bool Owned { get; private set; }
    public void Begin(bool owned, TriggerRequest? request = null) { Owned = owned; _request = owned ? request : null; }
    public TriggerRequest? End() { var request = _request; _request = null; Owned = false; return request; }
}

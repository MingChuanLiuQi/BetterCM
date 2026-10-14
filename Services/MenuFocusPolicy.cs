namespace BetterCM.Services;

public static class MenuFocusPolicy
{
    // A bounded retry must never reactivate over a different application chosen by the user.
    public static bool CanRetry(nint source, nint menu, nint foreground) => foreground == menu || source != 0 && foreground == source;
}

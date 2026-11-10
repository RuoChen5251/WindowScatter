namespace WindowScatter
{
    // Input policy is independent of WPF so key-repeat and deferred dismissal
    // can be verified without running the Windows overlay.
    internal sealed class ScatterInteraction
    {
        private bool activationKeyHeld;
        private bool dismissRequested;

        public bool ActivationKeyDown(bool modifiersMatch)
        {
            bool activate = modifiersMatch && !activationKeyHeld;
            activationKeyHeld = true;
            return activate;
        }

        public void ActivationKeyUp() => activationKeyHeld = false;
        public void RequestDismiss() => dismissRequested = true;

        public bool TakeDismissRequest(bool isBusy)
        {
            if (isBusy || !dismissRequested) return false;
            dismissRequested = false;
            return true;
        }

        public void ClearDismissRequest() => dismissRequested = false;
    }
}

using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= WsExLayered;
                return createParams;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return this.ShouldUsePassiveDesktopOverlayBehavior() || this.IsTaskbarIntegratedMode(); }
        }

        private bool ShouldUsePassiveDesktopOverlayBehavior()
        {
            return false;
        }

        private bool ShouldPassMouseToUnderlyingDesktop()
        {
            return this.ShouldUsePassiveDesktopOverlayBehavior() &&
                (Control.ModifierKeys & Keys.Shift) != Keys.Shift;
        }

        private bool IsTaskbarIntegratedMode()
        {
            return this.settings != null && this.settings.TaskbarIntegrationEnabled;
        }

        private bool ShouldUseGlobalTopMost()
        {
            return this.ShouldUseDesktopGlobalTopMost();
        }

        private bool ShouldUseDesktopGlobalTopMost()
        {
            return !this.IsTaskbarIntegratedMode();
        }

        private bool ShouldUseTaskbarLocalZOrder()
        {
            return this.IsTaskbarIntegratedMode();
        }

        private void ApplyWindowZOrderMode()
        {
            if (this.ShouldUseDesktopGlobalTopMost() && !this.TopMost)
            {
                this.TopMost = true;
            }
            else if (this.ShouldUseTaskbarLocalZOrder() && this.TopMost)
            {
                this.TopMost = false;
            }
        }
    }
}

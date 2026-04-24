using System;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private void PanelSkinMenuItem_Click(object sender, EventArgs e)
        {
            this.EnsureValidSelectedSkin();

            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string panelSkinId = item != null ? item.Tag as string : null;
            if (string.IsNullOrEmpty(panelSkinId))
            {
                return;
            }

            this.ApplyPanelSkin(panelSkinId);
        }

        private void DeleteSkinItem_Click(object sender, EventArgs e)
        {
            if (this.TryBeginInvokePopupForm(new Action(this.ConfirmAndDeleteCurrentSkin)))
            {
                return;
            }

            this.ConfirmAndDeleteCurrentSkin();
        }

        private void ConfirmAndDeleteCurrentSkin()
        {
            PanelSkinDefinition currentSkin = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            if (currentSkin == null)
            {
                return;
            }

            bool pausePopupTopMost = this.popupForm.Visible;
            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                this.ConfirmAndDeleteCurrentSkinCore(currentSkin);
            }
            finally
            {
                if (pausePopupTopMost)
                {
                    this.popupForm.ResumeTopMostEnforcement(false);
                }
            }
        }

        private void ConfirmAndDeleteCurrentSkinCore(PanelSkinDefinition currentSkin)
        {
            string originalSkinId = currentSkin.Id;
            IWin32Window owner = this.popupForm.Visible && !this.popupForm.IsDisposed
                ? (IWin32Window)this.popupForm
                : null;

            if (PanelSkinCatalog.IsProtectedSkinId(currentSkin.Id))
            {
                ShowMessageBox(
                    owner,
                    UiLanguage.Get(
                        "Menu.DeleteSkinProtected",
                        "Der Standardskin 'Normal' kann nicht gelöscht werden."),
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                this.RebuildPanelSkinMenuItems();
                this.UpdateMenuState();
                return;
            }

            PanelSkinDefinition[] availableSkins = PanelSkinCatalog.GetAvailableSkins();
            if (availableSkins.Length <= 1)
            {
                ShowMessageBox(
                    owner,
                    UiLanguage.Get(
                        "Menu.DeleteSkinLastBlocked",
                        "Der letzte verbliebene Skin kann nicht gelöscht werden."),
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string currentSkinName = this.GetPanelSkinDisplayName(currentSkin.Id);
            DialogResult confirmation = ShowMessageBox(
                owner,
                UiLanguage.Format(
                    "Menu.DeleteSkinConfirm",
                    "Soll der aktuelle Skin '{0}' wirklich gelöscht werden?",
                    currentSkinName),
                UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            string fallbackSkinId = PanelSkinCatalog.GetDefaultOrFirstSkinId();
            if (!string.Equals(this.settings.PanelSkinId, fallbackSkinId, StringComparison.OrdinalIgnoreCase))
            {
                this.settings = this.settings.WithPanelSkinId(fallbackSkinId);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.UpdateMenuState();
                Application.DoEvents();
            }

            TrafficPopupForm.ReleasePanelBackgroundAssetCache(currentSkin.DirectoryPath);
            string errorMessage;
            if (!PanelSkinCatalog.TryDeleteSkin(currentSkin.Id, out errorMessage))
            {
                string restoredSkinId = fallbackSkinId;
                PanelSkinDefinition restoredSkin = PanelSkinCatalog.GetSkinById(originalSkinId);
                if (restoredSkin != null &&
                    string.Equals(restoredSkin.Id, originalSkinId, StringComparison.OrdinalIgnoreCase))
                {
                    restoredSkinId = restoredSkin.Id;
                }

                ShowMessageBox(
                    owner,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? UiLanguage.Get("Menu.DeleteSkinFailed", "Der Skin konnte nicht gelöscht werden.")
                        : errorMessage,
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.settings = this.settings.WithPanelSkinId(restoredSkinId);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.RebuildPanelSkinMenuItems();
                this.UpdateMenuState();
                return;
            }

            this.settings = this.settings.WithPanelSkinId(fallbackSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.RebuildPanelSkinMenuItems();
            this.UpdateMenuState();
        }

        private static DialogResult ShowMessageBox(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            return owner == null
                ? MessageBox.Show(text, caption, buttons, icon, defaultButton)
                : MessageBox.Show(owner, text, caption, buttons, icon, defaultButton);
        }

        private void ApplyPanelSkin(string panelSkinId)
        {
            string normalizedPanelSkinId = PanelSkinCatalog.NormalizeSkinId(panelSkinId);
            if (string.Equals(this.settings.PanelSkinId, normalizedPanelSkinId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.settings = this.settings.WithPanelSkinId(normalizedPanelSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private bool EnsureValidSelectedSkin()
        {
            string normalizedPanelSkinId = PanelSkinCatalog.NormalizeSkinId(this.settings.PanelSkinId);
            if (string.Equals(this.settings.PanelSkinId, normalizedPanelSkinId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            this.settings = this.settings.WithPanelSkinId(normalizedPanelSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            return true;
        }

        private string GetPanelSkinDisplayName(string panelSkinId)
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(panelSkinId);
            if (definition == null)
            {
                return UiLanguage.Get("Menu.Skins", "Skins");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayNameKey))
            {
                return string.IsNullOrWhiteSpace(definition.DisplayNameFallback)
                    ? definition.Id
                    : definition.DisplayNameFallback;
            }

            string displayName = UiLanguage.Get(
                definition.DisplayNameKey,
                string.IsNullOrWhiteSpace(definition.DisplayNameFallback) ? definition.Id : definition.DisplayNameFallback);

            if (PanelSkinCatalog.IsProtectedSkinId(definition.Id))
            {
                displayName += UiLanguage.Get(
                    "Menu.SkinProtectedSuffix",
                    " (nicht löschbar)");
            }

            return displayName;
        }

        private void RebuildPanelSkinMenuItems()
        {
            this.skinItem.DropDownItems.Clear();
            this.panelSkinMenuItems.Clear();

            string[] panelSkinIds = MonitorSettings.GetSupportedPanelSkinIds();
            for (int i = 0; i < panelSkinIds.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(string.Empty, null, this.PanelSkinMenuItem_Click);
                item.Tag = panelSkinIds[i];
                this.panelSkinMenuItems[panelSkinIds[i]] = item;
                this.skinItem.DropDownItems.Add(item);
            }

            if (this.panelSkinMenuItems.Count > 0)
            {
                this.skinItem.DropDownItems.Add(new ToolStripSeparator());
                this.skinItem.DropDownItems.Add(this.deleteSkinItem);
            }
        }

        private string GetFallbackSkinId(string deletedSkinId)
        {
            PanelSkinDefinition[] definitions = PanelSkinCatalog.GetAvailableSkins();

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!string.Equals(definitions[i].Id, deletedSkinId, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i].Id;
                }
            }

            return "08";
        }
    }
}

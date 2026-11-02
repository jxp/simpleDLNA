using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using NMaier.SimpleDlna.GUI.Properties;
using NMaier.SimpleDlna.Server;
using Form = NMaier.Windows.Forms.Form;
using SystemInformation = NMaier.SimpleDlna.Utilities.SystemInformation;

namespace NMaier.SimpleDlna.GUI
{
  public partial class FormSettings : Form
  {
    private const string APP_KEY_NAME = "SimpleDLNA";

    private readonly StartupUtilities startUpUtilities;

    public FormSettings()
    {
      InitializeComponent();
      Icon = Resources.preferencesIcon;

      if (!SystemInformation.IsRunningOnMono()) {
        startUpUtilities = new StartupUtilities(StartupUtilities.StartupUserScope.CurrentUser);
        checkAutoStart.Checked = startUpUtilities.CheckIfRunAtWinBoot(APP_KEY_NAME);
      }
      else {
        checkAutoStart.Visible = false;
      }

      LoadClients();
    }

    private void LoadClients()
    {
      gridClients.AutoGenerateColumns = false;
      gridClients.DataSource = new BindingList<ClientInfo>(
        ClientRegistry.Instance.Clients
          .OrderByDescending(c => c.LastSeen)
          .ToList());
    }

    private void DeleteClients(IList<ClientInfo> selected)
    {
      foreach (var client in selected) {
        ClientRegistry.Instance.Remove(client.UserAgent);
      }
    }

    private IList<ClientInfo> SelectedClients()
    {
      return gridClients.SelectedRows
        .Cast<DataGridViewRow>()
        .Select(r => r.DataBoundItem as ClientInfo)
        .Where(c => c != null)
        .ToList();
    }

    private void buttonDeleteClient_Click(object sender, EventArgs e)
    {
      var selected = SelectedClients();
      if (selected.Count == 0) {
        return;
      }
      var prompt = selected.Count == 1
        ? $"Delete the client \"{selected[0].Description}\"?"
        : $"Delete {selected.Count} clients?";
      if (MessageBox.Show(
        this, prompt, "Delete clients", MessageBoxButtons.YesNo,
        MessageBoxIcon.Question) != DialogResult.Yes) {
        return;
      }
      DeleteClients(selected);
      var list = (BindingList<ClientInfo>)gridClients.DataSource;
      foreach (var client in selected) {
        list.Remove(client);
      }
    }

    private void gridClients_CurrentCellDirtyStateChanged(object sender,
      EventArgs e)
    {
      // Apply checkbox changes straight away, not when the cell loses focus
      if (gridClients.CurrentCell is DataGridViewCheckBoxCell) {
        gridClients.CommitEdit(DataGridViewDataErrorContexts.Commit);
      }
    }

    private void gridClients_SelectionChanged(object sender, EventArgs e)
    {
      buttonDeleteClient.Enabled = gridClients.SelectedRows.Count != 0;
    }

    private void gridClients_UserDeletingRow(object sender,
      DataGridViewRowCancelEventArgs e)
    {
      // Delete key: the grid removes the row itself
      var client = e.Row.DataBoundItem as ClientInfo;
      if (client != null) {
        DeleteClients(new[] {client});
      }
    }

    private void buttonBrowseCacheFile_Click(object sender, EventArgs e)
    {
      if (folderBrowserDialog.ShowDialog() == DialogResult.OK) {
        textCacheFile.Text = folderBrowserDialog.SelectedPath;
      }
    }

    private void checkAutoStart_CheckedChanged(object sender, EventArgs e)
    {
      if (checkAutoStart.Checked) {
        startUpUtilities.InstallAutoRun(APP_KEY_NAME);
      }
      else {
        startUpUtilities.UninstallAutoRun(APP_KEY_NAME);
      }
    }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            Utilities.MKVTools.Initialise(textMKVToolsDirectory.Text);
        }
    }
}

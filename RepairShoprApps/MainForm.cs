﻿using Microsoft.Win32;
using RepairShoprCore;
using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RepairShoprApps
{
    public partial class MainForm : Form
    {
        private bool _exportCustomer = false;
        private bool _exportTicket = false;
        private string _remoteHost = "repairshopr.com";
        private string _statusMessage = string.Empty;
        private string _path = null;
        private int? _defaultLocationId;
        private BackgroundWorker _bgw;

        public MainForm()
        {
            InitializeComponent();
            label1.Text = string.Empty;
            this.Text = "CommitCRM Exporter";
            buttonExport.Enabled = false;
            progressBar1.Visible = false;
            label3.Text = "";

            RepairShoprUtils.LogWriteLineinHTML("Initializing System with Default value", MessageSource.Initialization, "", messageType.Information);
            CommitCRMHandle();
            DBhandler();
        }

        private void CommitCRMHandle()
        {
            try
            {
                bool folderExist = false;

                if (!string.IsNullOrEmpty(Properties.Settings.Default.InstalledLocation))
                    folderExist = Directory.Exists(Properties.Settings.Default.InstalledLocation);

                if (!folderExist)
                {
                    string p_name = "CommitCRM";
                    string installedLocation = null;

                    RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    RepairShoprUtils.LogWriteLineinHTML("Reading CommitCRM Install Location from RegistryKey", MessageSource.Initialization, "", messageType.Information);
                    foreach (var keyName in key.GetSubKeyNames())
                    {
                        RegistryKey subkey = key.OpenSubKey(keyName);
                        var displayName = subkey.GetValue("DisplayName") as string;
                        if (p_name.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            installedLocation = subkey.GetValue("InstallLocation") as string;
                            RepairShoprUtils.LogWriteLineinHTML(string.Format("CommitCRM Install Directiory is '{0}'", installedLocation), MessageSource.Initialization, "", messageType.Information);
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(installedLocation))
                    {
                        if (MessageBox.Show("CommitCRM location is not found. Would you like to browse it now?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            var dialog = new FolderBrowserDialog();
                            dialog.ShowNewFolderButton = false;
                            if (dialog.ShowDialog() == DialogResult.OK)
                                installedLocation = dialog.SelectedPath;
                        }
                    }

                    Properties.Settings.Default.InstalledLocation = installedLocation;
                    Properties.Settings.Default.Save();

                    if (string.IsNullOrEmpty(Properties.Settings.Default.InstalledLocation))
                    {
                        RepairShoprUtils.LogWriteLineinHTML("CommitCRM is not configured", MessageSource.Initialization, "", messageType.Information);
                        return;
                    }
                }

                RepairShoprUtils.LogWriteLineinHTML("Setting CommitCRM Parameter such as DLL folder and DB Folder", MessageSource.Initialization, "", messageType.Information);
                var config = new CommitCRM.Config();
                config.AppName = "RepairShopr";
                config.CommitDllFolder = Path.Combine(Properties.Settings.Default.InstalledLocation, "ThirdParty", "UserDev");
                config.CommitDbFolder = Path.Combine(Properties.Settings.Default.InstalledLocation, "db");
                CommitCRM.Application.Initialize(config);
                RepairShoprUtils.LogWriteLineinHTML("Successfully configure CommitCRM", MessageSource.Initialization, "", messageType.Information);
            }
            catch (CommitCRM.Exception exc)
            {
                RepairShoprUtils.LogWriteLineinHTML("Failed to Configure CommitCRM", MessageSource.Initialization, exc.Message, messageType.Error);
            }
        }

        private void DBhandler()
        {
            _path = Path.Combine(RepairShoprUtils.folderPath, "RepairShopr.db3");
            FileInfo fileInfo = new FileInfo(_path);
            if (!fileInfo.Exists)
            {
                SQLiteConnection.CreateFile(_path);
                try
                {
                    using (SQLiteConnection con = new SQLiteConnection("data source=" + _path + ";PRAGMA journal_mode=WAL;"))
                    {
                        con.SetPassword("shyam");
                        con.Open();
                        SQLiteCommand cmdTableAccount = new SQLiteCommand("CREATE TABLE Account (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT ,AccountId nvarchar(50),CustomerId nvarchar(50))", con);
                        cmdTableAccount.ExecuteNonQuery();
                        SQLiteCommand cmdTableTicket = new SQLiteCommand("CREATE TABLE Ticket (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,TicketId nvarchar(50),RTicketId nvarchar(30))", con);
                        cmdTableTicket.ExecuteNonQuery();
                        SQLiteCommand cmdTableInvoice = new SQLiteCommand("CREATE TABLE Invoice (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,InvoiceId nvarchar(50),RinvoiceId nvarchar(50))", con);
                        cmdTableInvoice.ExecuteNonQuery();
                        con.Close();
                    }
                }
                catch (Exception ex)
                {
                    RepairShoprUtils.LogWriteLineinHTML("Failed to Create Datebase Setting", MessageSource.Initialization, ex.StackTrace, messageType.Error);
                }
            }
        }

        private void buttonLogin_Click(object sender, EventArgs e)
        {
            label1.ForeColor = Color.Green;
            label1.Text = "Login in progress";
            if (string.IsNullOrEmpty(textBoxUserName.Text))
            {
                errorProvider1.SetError(textBoxUserName, "User Name is Required field");
                label1.ForeColor = Color.Red;
                label1.Text = "User Name is Required field";
                RepairShoprUtils.LogWriteLineinHTML("User Name is is Required ", MessageSource.Login, "", messageType.Error);
                return;
            }
            else
            {
                errorProvider1.Clear();
                label1.ForeColor = Color.Green;
            }
            if (string.IsNullOrEmpty(textBoxPassWord.Text))
            {
                label1.ForeColor = Color.Red;
                errorProvider2.SetError(textBoxPassWord, "Password is Required field");
                label1.Text = "Password is Required field";
                RepairShoprUtils.LogWriteLineinHTML("Password is Required field ", MessageSource.Login, "", messageType.Error);
                return;
            }
            else
            {
                errorProvider2.Clear();
                label1.ForeColor = Color.Green;
            }
            RepairShoprUtils.LogWriteLineinHTML("Sending User Name and Password for Authentication", MessageSource.Login, "", messageType.Information);
            LoginResponse result = RepairShoprUtils.GetLoginResquest(textBoxUserName.Text.Trim(), textBoxPassWord.Text.Trim(), _remoteHost);
            if (result != null)
            {
                label1.Text = "Login Successful";
                buttonExport.Enabled = true;

                _defaultLocationId = result.DefaulLocationId;
            }
            else
            {
                label1.ForeColor = Color.Red;
                label1.Text = "Failed to Authenticate";

                _defaultLocationId = null;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            RepairShoprUtils.LogWriteLineinHTML("Export Button Clicked", MessageSource.Login, "", messageType.Information);

            if (string.IsNullOrEmpty(Properties.Settings.Default.InstalledLocation))
                CommitCRMHandle();
            if (string.IsNullOrEmpty(Properties.Settings.Default.InstalledLocation))
            {
                MessageBox.Show("CommitCRM location is not browsed. The export operation is terminated", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_bgw == null)
            {
                _bgw = new BackgroundWorker();
                _bgw.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
                _bgw.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);
                _bgw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
                _bgw.WorkerReportsProgress = true;
                _bgw.WorkerSupportsCancellation = true;
            }

            _exportTicket = checkBoxExportTicket.Checked;
            _exportCustomer = checkBoxExportCustomer.Checked;

            if (radioButtonRepairShopr.Checked)
                _remoteHost = "repairshopr.com";
            else
                _remoteHost = "syncromsp.com";

            progressBar1.Value = 0;
            progressBar1.Visible = true;
            progressBar1.Enabled = true;

            _bgw.RunWorkerAsync();
            buttonStop.Enabled = true;
        }

        private DateTime GetCreationDate()
        {
            var defaultAccounts = new CommitCRM.ObjectQuery<CommitCRM.Account>(CommitCRM.LinkEnum.linkAND, 1);
            defaultAccounts.AddSortExpression(CommitCRM.Account.Fields.CreationDate, CommitCRM.SortDirectionEnum.sortASC);
            var defaultAccountResult = defaultAccounts.FetchObjects();

            var date = Directory.GetCreationTime(Properties.Settings.Default.InstalledLocation);

//            if (defaultAccountResult != null && defaultAccountResult.Count > 0)
//                date = defaultAccountResult[0].CreationDate;

            return date;
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var connectionString = "data source=" + _path + ";PRAGMA journal_mode=WAL;Password=shyam;";

                DateTime customerExport;
                DateTime ticketExport;
                ticketExport = customerExport = GetCreationDate();

                #region Customer Export
                if (_exportCustomer)
                {
                    var exporter = new CustomerExporter(connectionString, () => ((BackgroundWorker)sender).CancellationPending);
                    exporter.ReportStatusEvent += (message) =>
                    {
                        _statusMessage = message;
                    };
                    exporter.ReportProgressEvent += (index, percentage, cancelled) =>
                    {
                        ((BackgroundWorker)sender).ReportProgress((int)percentage, index);
                        if (cancelled)
                            RepairShoprUtils.LogWriteLineinHTML("Contact Exporting Process is Stoped or Cancelled by User", MessageSource.Customer, "", messageType.Warning);
                    };
                    exporter.ReportCustomerErrorEvent += (customer, message, exception) =>
                    {
                        RepairShoprUtils.LogWriteLineinHTML(message, MessageSource.Customer, exception?.ToString(), messageType.Error);
                    };

                    var task = exporter.Export(customerExport, _remoteHost);
                    task.Wait();
                }
                #endregion

                #region Customer Export
                if (_exportTicket)
                {
                    if (Properties.Settings.Default.TicketExport != null && Properties.Settings.Default.TicketExport > ticketExport)
                        ticketExport = Properties.Settings.Default.TicketExport;

                    var exporter = new TicketExporter(_defaultLocationId, connectionString, () => ((BackgroundWorker)sender).CancellationPending);
                    exporter.ReportStatusEvent += (message) =>
                    {
                        _statusMessage = message;
                    };
                    exporter.ReportProgressEvent += (index, percentage, cancelled) =>
                    {
                        ((BackgroundWorker)sender).ReportProgress((int)percentage, index);
                        if (cancelled)
                            RepairShoprUtils.LogWriteLineinHTML("Contact Exporting Process is Stoped or Cancelled by User", MessageSource.Customer, "", messageType.Warning);
                    };
                    exporter.ReportTicketErrorEvent += (customer, message, exception) =>
                    {
                        RepairShoprUtils.LogWriteLineinHTML(message, MessageSource.Customer, exception?.ToString(), messageType.Error);
                    };

                    var task = exporter.Export(ticketExport, _remoteHost);
                    task.Wait();
                }
                #endregion
            }
            catch (CommitCRM.Exception exc)
            {
                RepairShoprUtils.LogWriteLineinHTML("Failed to Export Data", MessageSource.Initialization, exc.Message, messageType.Error);
            }
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
            buttonExport.Enabled = false;
            label3.Text = _statusMessage;
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar1.Value = 100;

            buttonStop.Enabled = false;
            buttonExport.Enabled = true;
            label3.Text = "Exporting Process is Completed";
            RepairShoprUtils.LogWriteLineinHTML("Exporting Process is Completed ", MessageSource.Complete, "", messageType.Information);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void viewLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(RepairShoprUtils.path);
        }

        private void textBoxUserName_TextChanged(object sender, EventArgs e)
        {
            errorProvider1.Clear();
            label1.Text = string.Empty;
        }

        private void textBoxPassWord_TextChanged(object sender, EventArgs e)
        {
            errorProvider2.Clear();
            label1.Text = string.Empty;
        }

        private void checkBoxExportCustomer_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxExportTicket.Enabled = checkBoxExportCustomer.Checked;
            checkBoxExportTicket.Checked = checkBoxExportCustomer.Checked;
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            if (_bgw.IsBusy)
            {
                _bgw.CancelAsync();
                progressBar1.Value = 100;
                label3.Text = "Exporting process is cancelled";
                buttonStop.Enabled = false;
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox1().ShowDialog();
        }

        private void resetConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int result = -1;
            int rsult = -1;
            RepairShoprUtils.LogWriteLineinHTML("Reseting Configuration including Database.. ", MessageSource.Initialization, "", messageType.Information);
            using (SQLiteConnection conn = new SQLiteConnection("data source=" + _path + ";PRAGMA journal_mode=WAL;Password=shyam;"))
            {
                conn.Open();
                using (SQLiteCommand cmdAccountDelete = new SQLiteCommand("DELETE From Account", conn))
                {
                    result = cmdAccountDelete.ExecuteNonQuery();
                }
                using (SQLiteCommand cmdTicketDelete = new SQLiteCommand("DELETE From Ticket", conn))
                {
                    rsult = cmdTicketDelete.ExecuteNonQuery();
                }

                var defaultTickets = new CommitCRM.ObjectQuery<CommitCRM.Ticket>(CommitCRM.LinkEnum.linkAND, 1);
                defaultTickets.AddSortExpression(CommitCRM.Ticket.Fields.UpdateDate, CommitCRM.SortDirectionEnum.sortASC);

                var defaultTicketResult = defaultTickets.FetchObjects();
                string ticketNumber = string.Empty;
                if (defaultTicketResult != null && defaultTicketResult.Count > 0)
                    ticketNumber = defaultTicketResult[0].TicketNumber;
                Properties.Settings.Default.TicketNumber = ticketNumber;

                var creationDate = GetCreationDate();
                Properties.Settings.Default.CustomerExport = creationDate;
                Properties.Settings.Default.TicketExport = creationDate;
                Properties.Settings.Default.Save();

                if (result != -1 && rsult != -1)
                {
                    RepairShoprUtils.LogWriteLineinHTML("Reset Configuration including Database successfull.", MessageSource.Initialization, "", messageType.Information);

                    MessageBox.Show("Configuration is reset successfull", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                conn.Close();
            }
        }

        private void supportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://feedback.repairshopr.com/knowledgebase/articles/796074");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitCRM.Application.Terminate();
            base.OnFormClosing(e);
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonRepairShopr.Checked)
            {
                _remoteHost = "repairshopr.com";
            }
        }

        private void radioButtonSyncro_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonSyncro.Checked)
            {
                _remoteHost = "syncromsp.com";
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
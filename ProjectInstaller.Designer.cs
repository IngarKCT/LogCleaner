
namespace LogCleaner
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.logCleanerServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.logCleanerServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // logCleanerServiceProcessInstaller
            // 
            this.logCleanerServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.logCleanerServiceProcessInstaller.Password = null;
            this.logCleanerServiceProcessInstaller.Username = null;
            // 
            // logCleanerServiceInstaller
            // 
            this.logCleanerServiceInstaller.Description = "Clean up log file directories";
            this.logCleanerServiceInstaller.DisplayName = "LogCleaner";
            this.logCleanerServiceInstaller.ServiceName = "LogCleaner";
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.logCleanerServiceProcessInstaller,
            this.logCleanerServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller logCleanerServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller logCleanerServiceInstaller;
    }
}
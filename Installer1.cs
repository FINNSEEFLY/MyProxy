using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace MyProxy
{
    [RunInstaller(true)]
    public partial class Installer1 : Installer
    {
        ServiceInstaller serviceInstaller;
        ServiceProcessInstaller processInstaller;
        public Installer1()
        {
            InitializeComponent();
            InitializeComponent();
            serviceInstaller = new ServiceInstaller();
            processInstaller = new ServiceProcessInstaller();
            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.Description = "Simple Proxy Server.";
            serviceInstaller.DisplayName = "MyProxy";
            serviceInstaller.StartType = ServiceStartMode.Manual;
            serviceInstaller.ServiceName = "MyProxy";
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}

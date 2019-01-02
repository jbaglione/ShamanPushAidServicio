using System.ComponentModel;
using System.Configuration.Install;

namespace ShamanPushAidServicioNoShaman
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}

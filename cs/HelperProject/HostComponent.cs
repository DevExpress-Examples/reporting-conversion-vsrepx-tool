using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelperProject {
    public class HostComponent : Component {
        private ConverterComponent converterComponent1;

        private void InitializeComponent() {
            this.converterComponent1 = new HelperProject.ConverterComponent();

        }
    }
}

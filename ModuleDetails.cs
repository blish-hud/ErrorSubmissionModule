using System.Collections.Generic;

namespace BhModule.Community.ErrorSubmissionModule {

    public class ModuleDetails {

        public string Id { get; set; } = string.Empty;

        public List<string> ModuleNamespaces { get; set; } = new List<string>();

        public string Dsn { get; set; } = string.Empty;

    }

}
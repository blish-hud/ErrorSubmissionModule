using Blish_HUD.Contexts;
using Sentry;

namespace BhModule.Community.ErrorSubmissionModule {
    public class EtmContext : Context {

        public PerformanceTransaction StartPerformanceTransaction(string name, string operation, string description = null) {
            return this.State != ContextState.Expired && SentrySdk.IsEnabled
                       ? new PerformanceTransaction(SentrySdk.StartTransaction(name, operation, description))
                       : null;
        }

    }
}

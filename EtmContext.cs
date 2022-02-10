using Blish_HUD.Contexts;
using Etm.Sdk;
using Sentry;

namespace BhModule.Community.ErrorSubmissionModule {
    public class EtmContext : Context, IEtmContext {

        public IPerformanceTransaction StartPerformanceTransaction(string name, string operation, string description = null) {
            return this.State != ContextState.Expired && SentrySdk.IsEnabled
                       ? new PerformanceTransaction(SentrySdk.StartTransaction(name, operation, description))
                       : null;
        }

    }
}

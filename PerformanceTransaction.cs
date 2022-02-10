using Etm.Sdk;
using Sentry;

namespace BhModule.Community.ErrorSubmissionModule {
    public class PerformanceTransaction : IPerformanceTransaction {

        private readonly ISpan _transaction;

        public bool IsFinished => _transaction.IsFinished;

        public PerformanceTransaction(ISpan transaction) {
            _transaction = transaction;
        }

        public IPerformanceTransaction StartChildPerformanceTransaction(string operation, string description = null) {
            return !_transaction.IsFinished 
                       ? new PerformanceTransaction(_transaction.StartChild(operation, description))
                       : null;
        }

        public void Finish() {
            if (!_transaction.IsFinished) {
                _transaction.Finish();
            }
        }

        public void SetTag(string tagName, string tagValue) {
            if (!_transaction.IsFinished) {
                _transaction.SetTag(tagName, tagValue);
            }
        }

        public void SetExtra(string extraName, object extraValue) {
            if (!_transaction.IsFinished) {
                _transaction.SetExtra(extraName, extraValue);
            }
        }

    }
}

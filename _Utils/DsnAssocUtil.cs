using System;
using System.Collections.Generic;
using System.Diagnostics;
using Blish_HUD;
using Sentry;

namespace BhModule.Community.ErrorSubmissionModule {
    public static class DsnAssocUtil {

        private static bool _stackFramesFailed = false;

        private static void SetReleaseFromId(SentryEvent sentryEvent, string id) {
            if (!GameService.Module.Loaded) return;

            sentryEvent.SetTag("bh.release", Program.OverlayVersion.ToString());

            foreach (var module in GameService.Module.Modules) {
                if (string.Equals(module.Manifest.Namespace, id, StringComparison.InvariantCultureIgnoreCase)) {
                    sentryEvent.Release = module.Manifest.Version.ToString();
                    return;
                }
            }
        }

        public static string GetDsnFromEvent(SentryEvent sentryEvent, EtmConfig config) {
            var stackNamespaces = new List<string>();
            
            if (!_stackFramesFailed && sentryEvent.Exception != null) {
                try {
                    var stacktrace = new StackTrace(sentryEvent.Exception);

                    if (stacktrace.FrameCount > 0) {
                        foreach (var frame in stacktrace.GetFrames()) {
                            stackNamespaces.Add(frame.GetMethod().DeclaringType?.Namespace);
                        }
                    }
                } catch (Exception) {
                    // We don't dwell on this.  Avoid bothering in the future, too.
                    _stackFramesFailed = true;
                }
            }

            foreach (var moduleDetails in config.Modules) {
                foreach (var moduleNamespace in moduleDetails.ModuleNamespaces) {
                    // Easy catch on the logger itself - great if the module reports the issue itself.
                    if (sentryEvent.Logger != null && sentryEvent.Logger.StartsWith(moduleNamespace, StringComparison.InvariantCultureIgnoreCase)) {
                        SetReleaseFromId(sentryEvent, moduleDetails.Id);
                        return moduleDetails.Dsn;
                    }

                    // Check the stack trace itself.
                    foreach (string stackNamespace in stackNamespaces) {
                        if (stackNamespace.StartsWith(moduleNamespace, StringComparison.InvariantCultureIgnoreCase)) {
                            SetReleaseFromId(sentryEvent, moduleDetails.Id);
                            return moduleDetails.Dsn;
                        }
                    }
                }
            }

            // No module matches - we report the base DSN.
            return config.BaseDsn;
        }

    }
}

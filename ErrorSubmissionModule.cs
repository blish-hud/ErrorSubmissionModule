using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using NLog.Config;
using Sentry.NLog;
using Sentry;
using NLog;
using Module = Blish_HUD.Modules.Module;
using System.Threading.Tasks;
using Flurl.Http;
using Sentry.Protocol;
using Microsoft.Xna.Framework;

namespace BhModule.Community.ErrorSubmissionModule {
    [Export(typeof(Module))]
    public class ErrorSubmissionModule : Module {

        private static readonly Blish_HUD.Logger Logger = Blish_HUD.Logger.GetLogger<ErrorSubmissionModule>();

        private const string SENTRY_DSN = "https://a3aeb0597daa404199a7dedba9e6fe87@sentry.blishhud.com:2083/2";

        private const string ETMCONFIG_URL = "https://etm.blishhud.com/config.json";

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        private SettingEntry<string> _userDiscordId;

        private ContextsService.ContextHandle<EtmContext> _etmContextHandle;

        private EtmConfig _config;

        private SentryNLogOptions _loggerOptions;

        [ImportingConstructor]
        public ErrorSubmissionModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override async Task LoadAsync() {
            try {
                _config = await ETMCONFIG_URL.GetJsonAsync<EtmConfig>();
            } catch (Exception ex) {
                Logger.Warn(ex, "Failed to download ETM config from {etmConfigUrl}. Using defaults.", ETMCONFIG_URL);
                _config = new EtmConfig();
            }

            if (_loggerOptions != null) {
                ApplyEtmConfig();
            }

            HookApi();
        }

        protected override void DefineSettings(SettingCollection settings) {
            _userDiscordId = settings.DefineSetting(nameof(_userDiscordId), "",   "Discord Username",   "Your full Discord username (yourname#1234) so that we can let you know when the issue is resolved.");

            _userDiscordId.SettingChanged += UpdateUser;

            UpdateUser(_userDiscordId, new ValueChangedEventArgs<string>(_userDiscordId.Value, _userDiscordId.Value));
        }

        private void UpdateUser(object sender, ValueChangedEventArgs<string> e) {
            SentrySdk.ConfigureScope(scope => {
                                         scope.User = string.IsNullOrWhiteSpace(e.NewValue) 
                                                           ? null 
                                                           : new User {
                                                               Username = e.NewValue
                                                           };
                                     });
        }

        private void HookLogger() {
            var logConfig = typeof(DebugService).GetField("_logConfiguration", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as LoggingConfiguration;

            logConfig.AddSentry(ConfigureSentry);
            LogManager.ReconfigExistingLoggers();
        }

        private WebHooks.WebApi _webApiHook;

        private void HookApi() {
            if (_config.ApiHookEnabled) {
                _webApiHook = new WebHooks.WebApi(_config, this);
            }
        }

        protected override void Update(GameTime gameTime) {
            _webApiHook?.Update(gameTime);
        }

        private int _maxReports = 10;
        private int _reports    = 0;

        private void ApplyEtmConfig() {
            _loggerOptions.Dsn              = _config.BaseDsn;
            _loggerOptions.TracesSampleRate = _config.TracesSampleRate;

            if (_config.DisableTaskExceptions) {
                _loggerOptions.DisableTaskUnobservedTaskExceptionCapture();
            }

            _maxReports = _config.MaxReports;
        }

        private void ConfigureSentry(SentryNLogOptions sentry) {
            _loggerOptions = sentry;

            sentry.Dsn                 = SENTRY_DSN;
            sentry.Environment         = string.IsNullOrEmpty(Program.OverlayVersion.PreRelease) ? "Release" : Program.OverlayVersion.PreRelease;
            sentry.Debug               = true;
            sentry.BreadcrumbLayout    = "${logger}: ${message}";
            sentry.MaxBreadcrumbs      = 10;
            sentry.TracesSampleRate    = 0.2;
            sentry.AutoSessionTracking = true;

            // Keep the amount of data collected to a minimum.
            sentry.DetectStartupTime    = StartupTimeDetectionMode.None;
            sentry.ReportAssembliesMode = ReportAssembliesMode.None;

            sentry.DisableNetFxInstallationsIntegration();

            sentry.MinimumBreadcrumbLevel = LogLevel.Debug;

            sentry.BeforeSend = delegate (SentryEvent d) {
                if (this.RunState != ModuleRunState.Loaded) { return null; }

                // Limit how much we send per session.
                if (_reports++ > _maxReports) {
                    return null;
                }

                // Don't bother sending in data from versions of Blish HUD that are simply too old.
                if (_config != null) {
                    if (!SemVer.Range.IsSatisfied(_config.SupportedBlishHUD, Program.OverlayVersion.BaseVersion().ToString())) {
                        return null;
                    }
                }

                // Detect if truly fatal - core will suppress these if we don't get it from the logger.
                if (string.Equals(d.Message?.Message ?? "", "Blish HUD encountered a fatal crash!", StringComparison.InvariantCultureIgnoreCase)) {
                    foreach (var sentryException in d.SentryExceptions ?? Enumerable.Empty<SentryException>()) {
                        if (sentryException.Mechanism != null) {
                            sentryException.Mechanism.Handled = false;
                            sentryException.Mechanism.Type    = "AppDomain.UnhandledException";
                        }
                    }
                    d.SetTag("handled",   "no");
                    d.SetTag("mechanism", "AppDomain.UnhandledException");
                }

                sentry.Dsn = DsnAssocUtil.GetDsnFromEvent(d, _config);

                d.SetExtra("launch-options", Environment.GetCommandLineArgs().Select(FilterUtil.FilterAll).ToArray());

                try {
                    if (GameService.Module != null && GameService.Module.Loaded) {
                        var moduleDetails = GameService.Module.Modules.Select(
                                                                              m => new {
                                                                                  m.Manifest.Name,
                                                                                  m.Manifest.Namespace,
                                                                                  Version = m.Manifest.Version.ToString(),
                                                                                  m.Enabled
                                                                              });

                        d.SetExtra("Modules", moduleDetails.ToArray());
                    }
                } catch (Exception unknownException) {
                    d.SetExtra("Modules", $"Exception: {unknownException.Message}");
                }

                return d;
            };

            Logger.Info("Sentry hook enabled.");
        }

        private void EnableSdkContext() {
            _etmContextHandle = GameService.Contexts.RegisterContext(new EtmContext());

            Logger.Info("etm.sdk context registered.");
        }

        protected override void Initialize() {
            if (Program.OverlayVersion.BaseVersion() != new SemVer.Version(0, 0, 0)) {
                HookLogger();
                EnableSdkContext();
            }
        }

        protected override void Unload() {
            _etmContextHandle?.Expire();
            _webApiHook?.UnloadHooks();

            if (_userDiscordId != null) {
                _userDiscordId.SettingChanged -= UpdateUser;
            }
        }

    }

}

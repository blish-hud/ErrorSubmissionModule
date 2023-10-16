using Blish_HUD;
using Blish_HUD.Gw2WebApi;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BhModule.Community.ErrorSubmissionModule.WebHooks {
    internal class WebApi {

        private static readonly Logger Logger = Logger.GetLogger<WebApi>();

        private const int HOOK_INTERVAL = 50;

        private FieldInfo _cachedAnonConnectionField;
        private FieldInfo _cachedPrivConnectionField;
        private FieldInfo _cachedApiManagersField;
        private FieldInfo _cachedManagedConnectionField;

        private bool _active = false;

        private WebStatMiddleware _wsm;

        public WebApi(EtmConfig config) {
            _wsm = new WebStatMiddleware(config);

            try {
                _cachedAnonConnectionField = typeof(Gw2WebApiService).GetField("_anonymousConnection", BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedPrivConnectionField = typeof(Gw2WebApiService).GetField("_privilegedConnection", BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedApiManagersField = typeof(Gw2ApiManager).GetField("_apiManagers", BindingFlags.Static | BindingFlags.NonPublic);
                _cachedManagedConnectionField = typeof(Gw2ApiManager).GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance);

                HookBaseConnections();

                _active = true;
            } catch (Exception e) {
                Logger.Warn(e, "Failed to get field information for Web API hook.");
            }
        }

        private IEnumerable<ManagedConnection> GetBaseConnections() {
            var anonymousConnection = _cachedAnonConnectionField.GetValue(GameService.Gw2WebApi) as ManagedConnection;
            var privilegedConnection = _cachedPrivConnectionField.GetValue(GameService.Gw2WebApi) as ManagedConnection;

            yield return anonymousConnection;
            yield return privilegedConnection;
        }

        private void HookBaseConnections() {
            foreach (var mc in GetBaseConnections()) {
                HookManagedConnection(mc);
            }
        }

        private void HookManagedConnection(ManagedConnection connection) {
            if (!connection.Connection.Middleware.Contains(_wsm)) {
                connection.Connection.Middleware.Add(_wsm);
            }
        }

        private void UnhookManagedConnection(ManagedConnection connection) {
            // Doesn't matter if it's actually in there or not
            connection.Connection.Middleware.Remove(_wsm);
        }

        private void HookAllModules() {
            try {
                var allModuleApiManagers = _cachedApiManagersField.GetValue(null) as List<Gw2ApiManager>;

                if (allModuleApiManagers != null) {
                    foreach (var apiManager in allModuleApiManagers) {
                        var connection = _cachedManagedConnectionField.GetValue(apiManager) as ManagedConnection;
                        HookManagedConnection(connection);
                    }
                }
            } catch (Exception e) {
                Logger.Warn(e, "Failed to hook a module's API manager.");
                _active = false;
            }
        }

        private double _lastCheck = -HOOK_INTERVAL;

        public void Update(GameTime gameTime) {
            if (_active && (gameTime.TotalGameTime.TotalMilliseconds - _lastCheck > HOOK_INTERVAL)) {
                _lastCheck = gameTime.TotalGameTime.TotalMilliseconds;
                HookAllModules();
            }
        }

        public void UnloadHooks() {
            try {
                // Unhook module connections
                var allModuleApiManagers = _cachedApiManagersField.GetValue(null) as List<Gw2ApiManager>;
                if (allModuleApiManagers != null) {
                    foreach (var apiManager in allModuleApiManagers) {
                        var connection = _cachedManagedConnectionField.GetValue(apiManager) as ManagedConnection;
                        UnhookManagedConnection(connection);
                    }
                }

                // Unhook base connections
                foreach (var mc in GetBaseConnections()) {
                    UnhookManagedConnection(mc);
                }
            } catch (Exception e) {
                Logger.Warn(e, "Failed to unload all API middlewares.");
            }

            _active = false;
        }

    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace BhModule.Community.ErrorSubmissionModule {
    public static class FilterUtil {

        // Best effort filters to remove identifying information.

        private const string FILTER_PATTERN = "#{0}#";

        private static readonly List<Func<string, string>> _filters = new List<Func<string, string>> {
            FilterUser
        };

        public static string FilterAll(string data) {
            if (string.IsNullOrEmpty(data))
                return data;

            for (int i = 0; i < _filters.Count; i++) {
                data = _filters[i].Invoke(data);
            }

            return data;
        }

        private static string FilterUser(string data) {
            // Filter out the user's windows username.
            string actualUsername  = Environment.UserName;
            data = data.ReplaceUsingStringComparison(actualUsername, string.Format(FILTER_PATTERN, "USERNAME"), StringComparison.InvariantCultureIgnoreCase);

            // Filter out the user's profile name (which can differ from the username).
            string profileUsername = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            data = data.ReplaceUsingStringComparison(profileUsername, string.Format(FILTER_PATTERN, "PROFILENAME"), StringComparison.InvariantCultureIgnoreCase);

            return data;
        }

    }
}

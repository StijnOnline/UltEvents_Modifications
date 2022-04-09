using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UltEvents.Editor;

// Register a SettingsProvider using IMGUI for the drawing framework:
static class UltEventPreferencesMenu {
    [SettingsProvider]
    public static SettingsProvider CreateMyCustomSettingsProvider() {


        var provider = new SettingsProvider("Preferences/UltEventPreferences", SettingsScope.User) {

            label = "UltEvents",

            guiHandler = ( searchContext ) => {
                BoolPref.DrawGui();
            },

            // Populate the search keywords to enable smart search filtering and label highlighting:
            keywords = BoolPref.GetSearchKeywords()
        };

        return provider;
    }
}
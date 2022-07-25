﻿namespace ChuckDeviceConfigurator
{
    using System.Reflection;

    using ChuckDeviceController.Data;

    public static partial class Strings
    {
        private static readonly AssemblyName StrongAssemblyName = Assembly.GetExecutingAssembly().GetName();

        // File assembly details
        public static readonly string AssemblyName = StrongAssemblyName?.Name ?? "ChuckDeviceConfigurator";
        public static readonly string AssemblyVersion = StrongAssemblyName?.Version?.ToString() ?? "v1.0.0";

        // Folder paths
        public const string WebRoot = "wwwroot";
        public static readonly string DataFolder = Path.Combine(WebRoot, "data");

        // Default user properties
        public const string DefaultUserName = "root";
        public const string DefaultUserPassword = "123Pa$$word.";
        public const string DefaultUserEmail = "admin@gmail.com";
        public const string DefaultSuccessLoginPath = "/Identity/Account/Manage";

        public const string DefaultInstanceStatus = "--";

        // Time properties
        public const ushort TenMinutesS = 600; // 10 minutes
        public const ushort ThirtyMinutesS = TenMinutesS * 3; // 30 minutes (1800)
        public const ushort SixtyMinutesS = ThirtyMinutesS * 2; // 60 minutes (3600)
        public const uint OneDayS = SixtyMinutesS * 24; // 24 hours (86400)

        public const string PokemonImageUrl = "https://raw.githubusercontent.com/WatWowMap/wwm-uicons/main/pokemon/";
        public const string GoogleMapsLinkFormat = "https://maps.google.com/maps?q={0},{1}";

        // Instance constants
        public const ushort SpinRangeM = 80; // NOTE: Revert back to 40m once reverted ingame
        public const ulong DefaultDistance = 10000000000000000;
        public const ushort CooldownLimitS = SixtyMinutesS * 2; // 2 hours (7200)
        public const uint SuspensionTimeLimitS = OneDayS * 30; // 30 days (2592000) - Max account suspension time period

        // Email
        public const string DefaultEmailConfirmationSubject = "Confirm your email";
        public const string DefaultEmailConfirmationMessageHtmlFormat = "Please confirm your account by <a href='{0}'>clicking here</a>.";
        public const string DefaultResetPasswordEmailSubject = "Reset Password";
        public const string DefaultResetPasswordMessageHtmlFormat = "Please reset your password by <a href='{0}'>clicking here</a>.";

        public const ushort DeviceOnlineThresholdS = 15 * 60; // 15 minutes (900)

        public const string DefaultDateTimeFormat = "MM/dd/yyyy hh:mm:ss tt";

        public const string GoogleMapsFormat = "https://maps.google.com/maps?q={0},{1}";

        #region Default Instance Property Values

        // All
        public const ushort DefaultMinimumLevel = 0;
        public const ushort DefaultMaximumLevel = 29;

        // Circle/Dynamic/Bootstrap
        public const CircleInstanceRouteType DefaultCircleRouteType = CircleInstanceRouteType.Smart;
        public const bool DefaultOptimizeDynamicRoute = true;

        // Quests
        public const short DefaultTimeZoneOffset = 0;
        public const string DefaultTimeZone = null;
        public const bool DefaultEnableDst = false;
        public const ushort DefaultSpinLimit = 3500;
        public const bool DefaultIgnoreS2CellBootstrap = false;
        public const bool DefaultUseWarningAccounts = false;
        public const QuestMode DefaultQuestMode = QuestMode.Normal;
        public const byte DefaultMaximumSpinAttempts = 5;
        public const ushort DefaultLogoutDelay = 900;

        // IV
        public const ushort DefaultIvQueueLimit = 100;
        public const string DefaultIvList = null;
        public const bool DefaultEnableLureEncounters = false;

        // Bootstrap
        public const bool DefaultFastBootstrapMode = false;
        public const ushort DefaultCircleSize = 70;
        public const bool DefaultOptimizeBootstrapRoute = true;
        public const string DefaultBootstrapCompleteInstanceName = null;

        // Tth Finder
        public const bool DefaultOnlyUnknownSpawnpoints = true;
        public const bool DefaultOptimizeSpawnpointRoute = true;

        // Leveling
        public const uint DefaultLevelingRadius = 10000;
        public const bool DefaultStoreLevelingData = false;
        public const string DefaultStartingCoordinate = null;

        // All
        public const string DefaultAccountGroup = null;
        public const bool DefaultIsEvent = false;

        #endregion
    }
}
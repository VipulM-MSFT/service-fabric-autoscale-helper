namespace NodeManager
{
    using System;
    using System.Fabric.Description;

    internal class NodeManagerSettings
    {
        private const string SectionName = "NodeManagerSettings";
        
        public NodeManagerSettings(ConfigurationSettings settings)
        {
            settings.Sections.TryGetValue(SectionName, out var configSection);

            this.ScanInterval = TimeSpan.FromSeconds(GetValueFromSection(configSection, "", 60));
            this.ClientOperationTimeout = TimeSpan.FromSeconds(GetValueFromSection(configSection, "", 30));
            this.DownNodeGraceInterval = TimeSpan.FromSeconds(GetValueFromSection(configSection, "", 120));
            this.SkipNodesUnderFabricUpgrade = GetValueFromSection(configSection, "", true);
        }

        public TimeSpan ScanInterval { get; }

        public TimeSpan ClientOperationTimeout { get; }

        public TimeSpan DownNodeGraceInterval { get; }

        public bool SkipNodesUnderFabricUpgrade { get; }

        private int GetValueFromSection(ConfigurationSection configSection, string parameter, int defaultValue)
        {
            string parameterValue = GetValueFromSection(configSection, parameter);

            if (int.TryParse(parameterValue, out var val))
            {
                return val;
            }

            return defaultValue;
        }

        private bool GetValueFromSection(ConfigurationSection configSection, string parameter, bool defaultValue)
        {
            string parameterValue = GetValueFromSection(configSection, parameter);

            if (bool.TryParse(parameterValue, out var boolVal))
            {
                return boolVal;
            }

            return defaultValue;
        }

        private string GetValueFromSection(ConfigurationSection configSection, string parameter, string defaultValue = "")
        {
            if (configSection == null)
            {
                return defaultValue;
            }

            if (configSection.Parameters.TryGetValue(parameter, out var val))
            {
                return val.Value;
            }

            return defaultValue;
        }
    }
}

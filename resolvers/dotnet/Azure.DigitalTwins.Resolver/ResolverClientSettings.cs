namespace Azure.DigitalTwins.Resolver
{
    public class ResolverClientSettings
    {
        public ResolverClientSettings()
        {
            ResolutionSetting = ResolutionSettingOption.FetchDependencies;
        }

        public ResolverClientSettings(ResolutionSettingOption resolutionSetting)
        {
            ResolutionSetting = resolutionSetting;
        }

        public ResolutionSettingOption ResolutionSetting { get; }
    }

    public enum ResolutionSettingOption
    {
        DisableFetchDependencies,
        FetchDependencies,
        FetchDependenciesFromExpanded
    }
}

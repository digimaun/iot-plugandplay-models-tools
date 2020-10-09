namespace Azure.DigitalTwins.Resolver
{
    public class ResolverSettings
    {
        public ResolverSettings()
        {
            IncludeDependencies = true;
            UsePreComputedDependencies = false;
        }

        public ResolverSettings(bool includeDependencies, bool usePreComputedDependencies)
        {
            IncludeDependencies = includeDependencies;
            UsePreComputedDependencies = usePreComputedDependencies;
        }

        public bool IncludeDependencies { get; }
        public bool UsePreComputedDependencies { get; }
    }
}

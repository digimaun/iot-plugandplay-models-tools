namespace Azure.DigitalTwins.Resolver
{
    public class ResolverSettings
    {
        public ResolverSettings()
        {
            CalculateDependencies = true;
            UsePreComputedDependencies = false;
        }

        public ResolverSettings(bool calculateDependencies, bool usePreComputedDependencies)
        {
            CalculateDependencies = calculateDependencies;
            UsePreComputedDependencies = usePreComputedDependencies;
        }

        public bool CalculateDependencies { get; }
        public bool UsePreComputedDependencies { get; }
    }
}

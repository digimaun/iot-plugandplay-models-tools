namespace Azure.IoT.DeviceModelsRepository.CLI.Exceptions
{
    public class MissingRootDTMIException : ValidationException
    {
        public MissingRootDTMIException(string fileName) :
        base($"File '{fileName}' does not have a root \"@id\" element")
        { }
    }
}

namespace Kaleido.Process
{
    public class ProcessorOptions
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Uniquely identifies this running instance of the processor.
        /// Defaults to a new GUID generated at registration time.
        /// </summary>
        public Guid InstanceId { get; set; } = Guid.NewGuid();

        internal Func<Type, bool>? TypeFilter { get; set; }
    }
}

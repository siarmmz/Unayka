namespace Unayka.Core.Test;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedEnvironment
{
    public const string Name = "Environment";
}

namespace Gaffer.Application.Serialization
{
    /// <summary>The current save schema version. Bump it when the save shape changes, and add a migration step.</summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 1;
    }
}

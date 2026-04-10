using System.Text.Json;

namespace OrcaCore.EventDrivenPrototype.Engine;

internal static class PrototypeCloner
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static T Clone<T>(T value)
    {
        if (value is null)
        {
            return value!;
        }

        if (value is ICloneable cloneable)
        {
            return (T)cloneable.Clone();
        }

        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        return element.Deserialize<T>(SerializerOptions)!;
    }

    public static object CloneObject(object value, Type type)
    {
        var element = JsonSerializer.SerializeToElement(value, type, SerializerOptions);
        return element.Deserialize(type, SerializerOptions)!;
    }
}

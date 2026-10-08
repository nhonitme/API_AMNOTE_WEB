using System.Text.Json.Serialization.Metadata;

namespace API_AMNOTE_WEB.Helpers;

/// <summary>Internal audit data excluded only from the MVC HTTP contract.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BackendAuditAttribute : Attribute
{
    public static void ApplyHttpContract(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            var property = typeInfo.Properties[index];
            if (property.AttributeProvider?.IsDefined(typeof(BackendAuditAttribute), true) == true)
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }
}

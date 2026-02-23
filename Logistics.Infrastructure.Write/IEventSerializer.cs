namespace Logistics.Infrastructure.Write;

public interface IEventSerializer
{
    string Serialize(object evt);
    object Deserialize(string eventType, string json);
}
